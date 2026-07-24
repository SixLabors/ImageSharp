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
    /// Maps a scanline through a filter operator, writes the residuals, and reduces their total variance.
    /// </summary>
    /// <typeparam name="TOperator">The PNG predictor selected for this closed traversal.</typeparam>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The preceding scanline.</param>
    /// <param name="result">The destination including its leading filter byte.</param>
    /// <param name="bytesPerPixel">The distance to the corresponding component in the preceding pixel.</param>
    /// <param name="sum">The sum of the absolute signed residuals.</param>
    // Inlining closes every static interface call over TOperator. The JIT can then remove
    // source loads ignored by simpler predictors and specialize the active register widths.
    [MethodImpl(InliningOptions.AlwaysInline)]
    public static void Encode<TOperator>(
        ReadOnlySpan<byte> scanline,
        ReadOnlySpan<byte> previousScanline,
        Span<byte> result,
        uint bytesPerPixel,
        out int sum)
        where TOperator : struct, IPngFilterOperator
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
        DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte previousBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);

        resultBaseRef = (byte)TOperator.Type;
        sum = 0;

        nuint x = 0;

        // Components in the first pixel have no left or upper-left neighbor. Supplying
        // zeroes expresses the PNG boundary rule directly through the same predictor.
        for (; x < bytesPerPixel; x++)
        {
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousBaseRef, x) : (byte)0;

            byte filtered = TOperator.Invoke(
                Unsafe.Add(ref scanBaseRef, x),
                0,
                above,
                0);

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

            for (nuint xLeft = x - bytesPerPixel; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector512<byte>.Count)
            {
                // Each byte lane represents one independently filtered component. The
                // four input vectors retain the scan/left/above/upper-left PNG layout.
                // Operator usage flags are constants after generic specialization, so
                // unused predictors do not retain even fault-preserving probe loads.
                Vector512<byte> left = TOperator.UsesLeft
                    ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft))
                    : default;
                Vector512<byte> above = TOperator.UsesAbove
                    ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, x))
                    : default;
                Vector512<byte> upperLeft = TOperator.UsesUpperLeft
                    ? Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft))
                    : default;

                Vector512<byte> filtered = TOperator.Invoke(
                    Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref scanBaseRef, x)),
                    left,
                    above,
                    upperLeft);

                Unsafe.As<byte, Vector512<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector512<byte>.Count;

                // PNG scores each residual as abs((sbyte)residual). VPSADBW sums eight
                // byte lanes into every other 32-bit lane without widening each byte.
                Vector512<byte> absolute = Avx512BW.Abs(filtered.AsSByte());
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

            for (nuint xLeft = x - bytesPerPixel; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector256<byte>.Count)
            {
                Vector256<byte> left = TOperator.UsesLeft
                    ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft))
                    : default;
                Vector256<byte> above = TOperator.UsesAbove
                    ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, x))
                    : default;
                Vector256<byte> upperLeft = TOperator.UsesUpperLeft
                    ? Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft))
                    : default;

                Vector256<byte> filtered = TOperator.Invoke(
                    Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x)),
                    left,
                    above,
                    upperLeft);

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector256<byte>.Count;

                Vector256<byte> absolute = Avx2.Abs(filtered.AsSByte());
                sum256 += Avx2.SumAbsoluteDifferences(absolute, Vector256<byte>.Zero).AsUInt32();
            }

            sum128 += sum256.GetLower() + sum256.GetUpper();
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneRegisterFromEnd = scanline.Length - Vector128<byte>.Count;

            for (nuint xLeft = x - bytesPerPixel; (int)x <= oneRegisterFromEnd; xLeft += (uint)Vector128<byte>.Count)
            {
                Vector128<byte> left = TOperator.UsesLeft
                    ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft))
                    : default;
                Vector128<byte> above = TOperator.UsesAbove
                    ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, x))
                    : default;
                Vector128<byte> upperLeft = TOperator.UsesUpperLeft
                    ? Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref previousBaseRef, xLeft))
                    : default;

                Vector128<byte> filtered = TOperator.Invoke(
                    Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x)),
                    left,
                    above,
                    upperLeft);

                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = filtered;
                x += (uint)Vector128<byte>.Count;

                sum128 = AccumulateAbsolute(sum128, filtered);
            }
        }

        sum += unchecked((int)Vector128.Sum(sum128));

        for (nuint xLeft = x - bytesPerPixel; x < (uint)scanline.Length; xLeft++, x++)
        {
            byte left = TOperator.UsesLeft ? Unsafe.Add(ref scanBaseRef, xLeft) : (byte)0;
            byte above = TOperator.UsesAbove ? Unsafe.Add(ref previousBaseRef, x) : (byte)0;
            byte upperLeft = TOperator.UsesUpperLeft ? Unsafe.Add(ref previousBaseRef, xLeft) : (byte)0;

            byte filtered = TOperator.Invoke(
                Unsafe.Add(ref scanBaseRef, x),
                left,
                above,
                upperLeft);

            Unsafe.Add(ref resultBaseRef, x + 1) = filtered;
            sum += Numerics.Abs(unchecked((sbyte)filtered));
        }
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
        if (Sse2.IsSupported)
        {
            Vector128<byte> absolute;

            if (Ssse3.IsSupported)
            {
                absolute = Ssse3.Abs(residuals.AsSByte());
            }
            else
            {
                // SSE2 has no packed signed-byte absolute instruction. The sign mask
                // implements (value + mask) XOR mask, including -128 -> 128.
                Vector128<sbyte> mask = Sse2.CompareGreaterThan(Vector128<sbyte>.Zero, residuals.AsSByte());
                absolute = Sse2.Xor(Sse2.Add(residuals.AsSByte(), mask), mask).AsByte();
            }

            return accumulator + Sse2.SumAbsoluteDifferences(absolute, Vector128<byte>.Zero).AsUInt32();
        }

        Vector128<byte> absoluteArm = Vector128.Abs(residuals.AsSByte()).AsByte();
        (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(absoluteArm);
        (Vector128<uint> lower0, Vector128<uint> lower1) = Vector128.Widen(lower16);
        (Vector128<uint> upper0, Vector128<uint> upper1) = Vector128.Widen(upper16);

        // Four widening additions keep every byte contribution in a 32-bit lane,
        // matching the x86 accumulator's overflow behavior without scalar reduction.
        return accumulator + lower0 + lower1 + upper0 + upper1;
    }
}
