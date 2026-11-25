// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// Applies PNG filter operators while accumulating the absolute signed residuals used for filter selection.
/// </summary>
internal static class PngFilterEncoder
{
    /// <summary>
    /// Writes a scanline with the None filter: the filter type byte, then the scanline bytes unchanged.
    /// </summary>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="result">The destination including its leading filter byte.</param>
    public static void EncodeNone(ReadOnlySpan<byte> scanline, Span<byte> result)
    {
        result[0] = (byte)FilterType.None;
        result = result[1..];
        scanline[..Math.Min(scanline.Length, result.Length)].CopyTo(result);
    }

    /// <summary>
    /// Subtracts the predictor of a filter operator from each byte of a scanline, writes the residuals, and reduces their total variance.
    /// </summary>
    /// <typeparam name="TOperator">The PNG predictor selected for this closed traversal.</typeparam>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The preceding scanline.</param>
    /// <param name="result">The destination including its leading filter byte.</param>
    /// <param name="bytesPerPixel">
    /// The distance to the corresponding component in the preceding pixel. Not read when the operator does not read
    /// the left pixel.
    /// </param>
    /// <param name="sum">The sum of the absolute signed residuals.</param>
    // Inlining closes every static interface call over TOperator. The JIT can then remove
    // source loads ignored by simpler predictors and specialize the active register widths.
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void Encode<TOperator>(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, int bytesPerPixel, out int sum)
        where TOperator : struct, IPngFilterOperator
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
        DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);

        resultBaseRef = (byte)TOperator.Type;
        sum = 0;

        // The loops index with nuint. The pixel byte count is never negative, so the conversion keeps its value.
        // A predictor that does not read the left pixel has no first-pixel boundary. Thus its offset is 0, and the
        // vector loops start at the first byte with no scalar bytes before them.
        nuint pixelBytes = TOperator.UsesLeft ? (uint)bytesPerPixel : 0;
        nuint x = 0;

        // Components in the first pixel have no left or upper-left neighbor. Supplying
        // zeroes expresses the PNG boundary rule directly through the same predictor.
        for (; x < pixelBytes; x++)
        {
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousBaseRef, x) : (byte)0;

            byte filtered = (byte)(Unsafe.Add(ref scanBaseRef, x) - TOperator.Predict(0, above, 0));

            Unsafe.Add(ref resultBaseRef, x + 1) = filtered;
            sum += Numerics.Abs(unchecked((sbyte)filtered));
        }

        Vector128<uint> sum128 = Vector128<uint>.Zero;

        // A single 512-bit register does not amortize folding its SAD accumulator.
        // Leave short rows to the narrower paths, which have lower fixed reduction cost.
        if (Avx512BW.IsSupported && scanline.Length - (int)x >= Vector512<byte>.Count * 2)
        {
            Vector512<uint> sum512 = Vector512<uint>.Zero;
            int oneRegisterFromEnd = scanline.Length - Vector512<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector512<byte>.Count)
            {
                // Each byte lane represents one independently filtered component. The
                // four input vectors retain the scan/left/above/upper-left PNG layout.
                // Operator usage flags are constants after generic specialization, so
                // unused predictors do not retain even fault-preserving probe loads.
                Vector512<byte> left = TOperator.UsesLeft ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft)) : default;
                Vector512<byte> above = TOperator.UsesAbove ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, x)) : default;
                Vector512<byte> upperLeft = TOperator.UsesUpperLeft ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft)) : default;

                Vector512<byte> filtered = Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, x)) - TOperator.Predict(left, above, upperLeft);

                Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector512<byte>.Count;

                // Vector512.Abs lowers to VPABSB under the surrounding AVX-512BW guard.
                // Reinterpreting the signed result preserves -128's 0x80 bit pattern as
                // the unsigned magnitude 128 consumed by VPSADBW.
                Vector512<byte> absolute = Vector512.Abs(filtered.AsSByte()).AsByte();
                sum512 += Avx512BW.SumAbsoluteDifferences(absolute, Vector512<byte>.Zero).AsUInt32();
            }

            // Fold only widths that processed data. Short rows therefore avoid both
            // wide accumulator initialization and an otherwise empty reduction.
            Vector256<uint> folded512 = sum512.GetLower() + sum512.GetUpper();
            sum128 += folded512.GetLower() + folded512.GetUpper();
        }

        if (Avx2.IsSupported)
        {
            Vector256<uint> sum256 = Vector256<uint>.Zero;
            int oneRegisterFromEnd = scanline.Length - Vector256<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector256<byte>.Count)
            {
                // Thirty-two byte lanes preserve the same scan/left/above/upper-left
                // correspondence as the 512-bit path. Closed operator flags remove
                // unused loads when the predictor does not consume that neighbor.
                Vector256<byte> left = TOperator.UsesLeft ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft)) : default;
                Vector256<byte> above = TOperator.UsesAbove ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x)) : default;
                Vector256<byte> upperLeft = TOperator.UsesUpperLeft ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft)) : default;

                Vector256<byte> filtered = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x)) - TOperator.Predict(left, above, upperLeft);

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector256<byte>.Count;

                // Vector256.Abs lowers to VPABSB under the surrounding AVX2 guard.
                // Reinterpreting the signed result preserves -128's 0x80 bit pattern as
                // the unsigned magnitude 128 consumed by VPSADBW.
                Vector256<byte> absolute = Vector256.Abs(filtered.AsSByte()).AsByte();
                sum256 += Avx2.SumAbsoluteDifferences(absolute, Vector256<byte>.Zero).AsUInt32();
            }

            // Fold both 128-bit halves into the shared four-lane accumulator.
            sum128 += sum256.GetLower() + sum256.GetUpper();
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneRegisterFromEnd = scanline.Length - Vector128<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector128<byte>.Count)
            {
                // The final vector width handles sixteen more components with the
                // same lane-wise neighborhood layout before the scalar remainder.
                Vector128<byte> left = TOperator.UsesLeft ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft)) : default;
                Vector128<byte> above = TOperator.UsesAbove ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, x)) : default;
                Vector128<byte> upperLeft = TOperator.UsesUpperLeft ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft)) : default;

                Vector128<byte> filtered = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x)) - TOperator.Predict(left, above, upperLeft);

                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector128<byte>.Count;

                // AccumulateAbsolute selects the available x86 or portable widening
                // reduction while preserving the same unsigned 32-bit partial sums.
                sum128 = AccumulateAbsolute(sum128, filtered);
            }
        }

        // Reduce the four partial lanes before adding individually scored tail bytes.
        sum += unchecked((int)Vector128.Sum(sum128));

        for (nuint xLeft = x - pixelBytes; x < (uint)scanline.Length; xLeft++, x++)
        {
            byte left = TOperator.UsesLeft ? Unsafe.Add(ref scanBaseRef, xLeft) : (byte)0;
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousBaseRef, x) : (byte)0;
            byte upperLeft = TOperator.UsesUpperLeft ? Unsafe.Add(ref previousBaseRef, xLeft) : (byte)0;

            byte filtered = (byte)(Unsafe.Add(ref scanBaseRef, x) - TOperator.Predict(left, above, upperLeft));

            Unsafe.Add(ref resultBaseRef, x + 1) = filtered;
            sum += Numerics.Abs(unchecked((sbyte)filtered));
        }
    }

    /// <summary>
    /// Finds the filter whose residuals have the smallest sum of absolute signed values. The adaptive filter uses
    /// this sum as its estimate of how well a row compresses, as libpng does.
    /// </summary>
    /// <remarks>
    /// One pass loads each scan, left, above and upper-left vector once and computes the residuals of all four
    /// filters from them. No residual is written. The caller then writes only the chosen filter. Equal sums select
    /// the first filter in the order Sub, Up, Average, Paeth.
    /// </remarks>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The preceding scanline.</param>
    /// <param name="bytesPerPixel">The distance to the corresponding component in the preceding pixel.</param>
    /// <returns>The filter type with the smallest sum.</returns>
    public static FilterType SelectFilter(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, int bytesPerPixel)
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));

        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        int sub = 0;
        int up = 0;
        int average = 0;
        int paeth = 0;

        // The loops index with nuint. The pixel byte count is never negative, so the conversion keeps its value.
        nuint pixelBytes = (uint)bytesPerPixel;
        nuint x = 0;

        // Components in the first pixel have no left or upper-left neighbor. PNG uses 0 for both.
        for (; x < pixelBytes && x < (uint)scanline.Length; x++)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            sub += AbsoluteResidual(scan, SubFilterOperator.Predict(0, above, 0));
            up += AbsoluteResidual(scan, UpFilterOperator.Predict(0, above, 0));
            average += AbsoluteResidual(scan, AverageFilterOperator.Predict(0, above, 0));
            paeth += AbsoluteResidual(scan, PaethFilterOperator.Predict(0, above, 0));
        }

        // Each accumulator holds the partial sums of one filter. The SAD instructions add groups of 8 absolute
        // residuals into 64-bit lanes. Thus the low 32-bit half of each lane holds the sum.
        Vector128<uint> sub128 = Vector128<uint>.Zero;
        Vector128<uint> up128 = Vector128<uint>.Zero;
        Vector128<uint> average128 = Vector128<uint>.Zero;
        Vector128<uint> paeth128 = Vector128<uint>.Zero;

        if (Avx512BW.IsSupported && scanline.Length - (int)x >= Vector512<byte>.Count * 2)
        {
            Vector512<uint> sub512 = Vector512<uint>.Zero;
            Vector512<uint> up512 = Vector512<uint>.Zero;
            Vector512<uint> average512 = Vector512<uint>.Zero;
            Vector512<uint> paeth512 = Vector512<uint>.Zero;
            int oneRegisterFromEnd = scanline.Length - Vector512<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector512<byte>.Count, x += (uint)Vector512<byte>.Count)
            {
                Vector512<byte> scan = Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector512<byte> left = Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector512<byte> above = Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector512<byte> upperLeft = Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft));

                sub512 += SumAbsolute(scan - SubFilterOperator.Predict(left, above, upperLeft));
                up512 += SumAbsolute(scan - UpFilterOperator.Predict(left, above, upperLeft));
                average512 += SumAbsolute(scan - AverageFilterOperator.Predict(left, above, upperLeft));
                paeth512 += SumAbsolute(scan - PaethFilterOperator.Predict(left, above, upperLeft));
            }

            sub128 += Fold(sub512);
            up128 += Fold(up512);
            average128 += Fold(average512);
            paeth128 += Fold(paeth512);
        }

        if (Avx2.IsSupported)
        {
            Vector256<uint> sub256 = Vector256<uint>.Zero;
            Vector256<uint> up256 = Vector256<uint>.Zero;
            Vector256<uint> average256 = Vector256<uint>.Zero;
            Vector256<uint> paeth256 = Vector256<uint>.Zero;
            int oneRegisterFromEnd = scanline.Length - Vector256<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector256<byte>.Count, x += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector256<byte> upperLeft = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft));

                sub256 += SumAbsolute(scan - SubFilterOperator.Predict(left, above, upperLeft));
                up256 += SumAbsolute(scan - UpFilterOperator.Predict(left, above, upperLeft));
                average256 += SumAbsolute(scan - AverageFilterOperator.Predict(left, above, upperLeft));
                paeth256 += SumAbsolute(scan - PaethFilterOperator.Predict(left, above, upperLeft));
            }

            sub128 += sub256.GetLower() + sub256.GetUpper();
            up128 += up256.GetLower() + up256.GetUpper();
            average128 += average256.GetLower() + average256.GetUpper();
            paeth128 += paeth256.GetLower() + paeth256.GetUpper();
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneRegisterFromEnd = scanline.Length - Vector128<byte>.Count;

            for (nuint xLeft = x - pixelBytes; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector128<byte>.Count, x += (uint)Vector128<byte>.Count)
            {
                Vector128<byte> scan = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector128<byte> left = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector128<byte> above = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, x));
                Vector128<byte> upperLeft = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft));

                sub128 = AccumulateAbsolute(sub128, scan - SubFilterOperator.Predict(left, above, upperLeft));
                up128 = AccumulateAbsolute(up128, scan - UpFilterOperator.Predict(left, above, upperLeft));
                average128 = AccumulateAbsolute(average128, scan - AverageFilterOperator.Predict(left, above, upperLeft));
                paeth128 = AccumulateAbsolute(paeth128, scan - PaethFilterOperator.Predict(left, above, upperLeft));
            }
        }

        sub += unchecked((int)Vector128.Sum(sub128));
        up += unchecked((int)Vector128.Sum(up128));
        average += unchecked((int)Vector128.Sum(average128));
        paeth += unchecked((int)Vector128.Sum(paeth128));

        for (nuint xLeft = x - pixelBytes; x < (uint)scanline.Length; xLeft++, x++)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, xLeft);
            byte above = Unsafe.Add(ref previousBaseRef, x);
            byte upperLeft = Unsafe.Add(ref previousBaseRef, xLeft);
            sub += AbsoluteResidual(scan, SubFilterOperator.Predict(left, above, upperLeft));
            up += AbsoluteResidual(scan, UpFilterOperator.Predict(left, above, upperLeft));
            average += AbsoluteResidual(scan, AverageFilterOperator.Predict(left, above, upperLeft));
            paeth += AbsoluteResidual(scan, PaethFilterOperator.Predict(left, above, upperLeft));
        }

        // A strict comparison keeps the first filter of equal sums, in the order Sub, Up, Average, Paeth.
        FilterType type = FilterType.Sub;
        int min = sub;
        if (up < min)
        {
            min = up;
            type = FilterType.Up;
        }

        if (average < min)
        {
            min = average;
            type = FilterType.Average;
        }

        if (paeth < min)
        {
            type = FilterType.Paeth;
        }

        return type;
    }

    /// <summary>
    /// Gets the absolute signed value of the residual of one byte.
    /// </summary>
    /// <param name="scan">The component.</param>
    /// <param name="predictor">The predictor of the component.</param>
    /// <returns>The absolute value of the residual as a signed byte.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static int AbsoluteResidual(byte scan, byte predictor) => Numerics.Abs(unchecked((sbyte)(byte)(scan - predictor)));

    /// <summary>
    /// Sums the absolute signed values of 64 residuals into eight 64-bit lanes.
    /// </summary>
    /// <param name="residuals">The residuals.</param>
    /// <returns>The partial sums, viewed as 32-bit lanes.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector512<uint> SumAbsolute(Vector512<byte> residuals)

        // Reinterpreting the signed absolute value keeps -128's 0x80 bit pattern as the magnitude 128.
        => Avx512BW.SumAbsoluteDifferences(Vector512.Abs(residuals.AsSByte()).AsByte(), Vector512<byte>.Zero).AsUInt32();

    /// <summary>
    /// Sums the absolute signed values of 32 residuals into four 64-bit lanes.
    /// </summary>
    /// <param name="residuals">The residuals.</param>
    /// <returns>The partial sums, viewed as 32-bit lanes.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector256<uint> SumAbsolute(Vector256<byte> residuals)
        => Avx2.SumAbsoluteDifferences(Vector256.Abs(residuals.AsSByte()).AsByte(), Vector256<byte>.Zero).AsUInt32();

    /// <summary>
    /// Folds a 512-bit accumulator to 128 bits.
    /// </summary>
    /// <param name="accumulator">The accumulator.</param>
    /// <returns>The sum of the four 128-bit parts.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector128<uint> Fold(Vector512<uint> accumulator)
    {
        Vector256<uint> folded = accumulator.GetLower() + accumulator.GetUpper();
        return folded.GetLower() + folded.GetUpper();
    }

    /// <summary>
    /// Accumulates the absolute signed values in a 128-bit residual vector.
    /// </summary>
    /// <param name="accumulator">The four-lane unsigned accumulator.</param>
    /// <param name="residuals">The sixteen filtered byte residuals.</param>
    /// <returns>The updated accumulator.</returns>
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static Vector128<uint> AccumulateAbsolute(Vector128<uint> accumulator, Vector128<byte> residuals)
    {
        // The generic absolute-value intrinsic selects PABSB where available and
        // preserves -128's 0x80 bit pattern as the unsigned magnitude 128.
        Vector128<byte> absolute = Vector128.Abs(residuals.AsSByte()).AsByte();

        if (Sse2.IsSupported)
        {
            return accumulator + Sse2.SumAbsoluteDifferences(absolute, Vector128<byte>.Zero).AsUInt32();
        }

        (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(absolute);
        (Vector128<uint> lower0, Vector128<uint> lower1) = Vector128.Widen(lower16);
        (Vector128<uint> upper0, Vector128<uint> upper1) = Vector128.Widen(upper16);

        // Four widening additions keep every byte contribution in a 32-bit lane,
        // matching the x86 accumulator's overflow behavior without scalar reduction.
        return accumulator + lower0 + lower1 + upper0 + upper1;
    }
}
