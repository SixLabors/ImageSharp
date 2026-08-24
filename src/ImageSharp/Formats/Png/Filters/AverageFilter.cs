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
            DecodeScalar(scanline, previousScanline, (uint)bytesPerPixel);
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
        nuint offset = 1;
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
        nuint offset = 1;
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
    private static void DecodeScalar(Span<byte> scanline, Span<byte> previousScanline, uint bytesPerPixel)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        nuint x = 1;
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
    public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, uint bytesPerPixel, out int sum)
        => PngFilterEncoder.Encode<AverageFilterOperator>(scanline, previousScanline, result, bytesPerPixel, out sum);

    /// <summary>
    /// Calculates the average value of two bytes
    /// </summary>
    /// <param name="left">The left byte</param>
    /// <param name="above">The above byte</param>
    /// <returns>The <see cref="int"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Average(byte left, byte above) => (left + above) >> 1;
}
