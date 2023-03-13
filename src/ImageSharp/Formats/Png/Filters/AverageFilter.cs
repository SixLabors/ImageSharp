// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// The Average filter uses the average of the two neighboring pixels (left and above) to predict
/// the value of a pixel.
/// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
/// </summary>
internal static class AverageFilter
{
    /// <summary>
    /// Decodes a scanline, which was filtered with the average filter.
    /// </summary>
    /// <param name="scanline">The scanline to decode.</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decode(Span<byte> scanline, Span<byte> previousScanline, int bytesPerPixel)
    {
        DebugGuard.MustBeSameSized<byte>(scanline, previousScanline, nameof(scanline));

        // The Avg filter predicts each pixel as the (truncated) average of a and b:
        // Average(x) + floor((Raw(x-bpp)+Prior(x))/2)
        // With pixels positioned like this:
        //  prev:  c b
        //  row:   a d
        if (Sse2.IsSupported && bytesPerPixel is 4)
        {
            DecodeSse2(scanline, previousScanline);
        }
        else if (AdvSimd.IsSupported && bytesPerPixel is 4)
        {
            DecodeArm(scanline, previousScanline);
        }
        else
        {
            DecodeScalar(scanline, previousScanline, (nint)(uint)bytesPerPixel);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeSse2(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        Vector128<byte> d = Vector128<byte>.Zero;
        Vector128<byte> ones = Vector128.Create((byte)1);

        int rb = scanline.Length;
        nint offset = 1;
        while (rb >= 4)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector128<byte> a = d;
            Vector128<byte> b = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref Unsafe.Add(ref prevBaseRef, offset))).AsByte();
            d = Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref scanRef)).AsByte();

            // PNG requires a truncating average, so we can't just use _mm_avg_epu8,
            // but we can fix it up by subtracting off 1 if it rounded up.
            Vector128<byte> avg = Sse2.Average(a, b);
            Vector128<byte> xor = Sse2.Xor(a, b);
            Vector128<byte> and = Sse2.And(xor, ones);
            avg = Sse2.Subtract(avg, and);
            d = Sse2.Add(d, avg);

            // Store the result.
            Unsafe.As<byte, int>(ref scanRef) = Sse2.ConvertToInt32(d.AsInt32());

            rb -= 4;
            offset += 4;
        }
    }

    public static void DecodeArm(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        Vector64<byte> d = Vector64<byte>.Zero;

        int rb = scanline.Length;
        nint offset = 1;
        const int bytesPerBatch = 4;
        while (rb >= bytesPerBatch)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector64<byte> a = d;
            Vector64<byte> b = Vector64.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref prevBaseRef, offset))).AsByte();
            d = Vector64.CreateScalar(Unsafe.As<byte, int>(ref scanRef)).AsByte();

            Vector64<byte> avg = AdvSimd.FusedAddHalving(a, b);
            d = AdvSimd.Add(d, avg);

            Unsafe.As<byte, int>(ref scanRef) = d.AsInt32().ToScalar();

            rb -= bytesPerBatch;
            offset += bytesPerBatch;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeScalar(Span<byte> scanline, Span<byte> previousScanline, nint bytesPerPixel)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        nint x = 1;
        for (; x <= bytesPerPixel /* Note the <= because x starts at 1 */; ++x)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            scan = (byte)(scan + (above >> 1));
        }

        for (; x < (uint)scanline.Length; ++x)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            scan = (byte)(scan + Average(left, above));
        }
    }

    /// <summary>
    /// Encodes a scanline with the average filter applied.
    /// </summary>
    /// <param name="scanline">The scanline to encode.</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="result">The filtered scanline result.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    /// <param name="sum">The sum of the total variance of the filtered row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, int bytesPerPixel, out int sum)
    {
        DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
        DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);
        ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
        sum = 0;

        // Average(x) = Raw(x) - floor((Raw(x-bpp)+Prior(x))/2)
        resultBaseRef = (byte)FilterType.Average;

        nint x = 0;
        for (; x < (uint)bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            ++x;
            ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
            res = (byte)(scan - (above >> 1));
            sum += Numerics.Abs(unchecked((sbyte)res));
        }

        if (Avx2.IsSupported)
        {
            Vector256<byte> zero = Vector256<byte>.Zero;
            Vector256<int> sumAccumulator = Vector256<int>.Zero;
            Vector256<byte> allBitsSet = Avx2.CompareEqual(sumAccumulator, sumAccumulator).AsByte();

            for (nint xLeft = x - (nint)(uint)bytesPerPixel; x <= (uint)scanline.Length - Vector256<byte>.Count; xLeft += Vector256<byte>.Count)
            {
                Vector256<byte> scan = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector256<byte> left = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector256<byte> above = Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                Vector256<byte> avg = Avx2.Xor(Avx2.Average(Avx2.Xor(left, allBitsSet), Avx2.Xor(above, allBitsSet)), allBitsSet);
                Vector256<byte> res = Avx2.Subtract(scan, avg);

                Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                x += Vector256<byte>.Count;

                sumAccumulator = Avx2.Add(sumAccumulator, Avx2.SumAbsoluteDifferences(Avx2.Abs(res.AsSByte()), zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(sumAccumulator);
        }
        else if (Sse2.IsSupported)
        {
            Vector128<byte> zero = Vector128<byte>.Zero;
            Vector128<int> sumAccumulator = Vector128<int>.Zero;
            Vector128<byte> allBitsSet = Sse2.CompareEqual(sumAccumulator, sumAccumulator).AsByte();

            for (nint xLeft = x - (nint)(uint)bytesPerPixel; x <= (uint)scanline.Length - Vector128<byte>.Count; xLeft += Vector128<byte>.Count)
            {
                Vector128<byte> scan = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, x));
                Vector128<byte> left = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref scanBaseRef, xLeft));
                Vector128<byte> above = Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref prevBaseRef, x));

                Vector128<byte> avg = Sse2.Xor(Sse2.Average(Sse2.Xor(left, allBitsSet), Sse2.Xor(above, allBitsSet)), allBitsSet);
                Vector128<byte> res = Sse2.Subtract(scan, avg);

                Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref resultBaseRef, x + 1)) = res; // +1 to skip filter type
                x += Vector128<byte>.Count;

                Vector128<byte> absRes;
                if (Ssse3.IsSupported)
                {
                    absRes = Ssse3.Abs(res.AsSByte());
                }
                else
                {
                    Vector128<sbyte> mask = Sse2.CompareGreaterThan(zero.AsSByte(), res.AsSByte());
                    absRes = Sse2.Xor(Sse2.Add(res.AsSByte(), mask), mask).AsByte();
                }

                sumAccumulator = Sse2.Add(sumAccumulator, Sse2.SumAbsoluteDifferences(absRes, zero).AsInt32());
            }

            sum += Numerics.EvenReduceSum(sumAccumulator);
        }

        for (nint xLeft = x - (nint)(uint)bytesPerPixel; x < (uint)scanline.Length; ++xLeft /* Note: ++x happens in the body to avoid one add operation */)
        {
            byte scan = Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, xLeft);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            ++x;
            ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
            res = (byte)(scan - Average(left, above));
            sum += Numerics.Abs(unchecked((sbyte)res));
        }
    }

    /// <summary>
    /// Calculates the average value of two bytes
    /// </summary>
    /// <param name="left">The left byte</param>
    /// <param name="above">The above byte</param>
    /// <returns>The <see cref="int"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Average(byte left, byte above) => (left + above) >> 1;
}
