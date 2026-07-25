// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Png.Filters;

/// <summary>
/// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
/// then chooses as predictor the neighboring pixel closest to the computed value.
/// This technique is due to Alan W. Paeth.
/// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
/// </summary>
internal static class PaethFilter
{
    /// <summary>
    /// Decodes a scanline, which was filtered with the paeth filter.
    /// </summary>
    /// <param name="scanline">The scanline to decode.</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Decode(Span<byte> scanline, Span<byte> previousScanline, int bytesPerPixel)
    {
        DebugGuard.MustBeSameSized<byte>(scanline, previousScanline, nameof(scanline));

        // Paeth tries to predict pixel d using the pixel to the left of it, a,
        // and two pixels from the previous row, b and c:
        // prev: c b
        // row:  a d
        // The Paeth function predicts d to be whichever of a, b, or c is nearest to
        // p = a + b - c.
        if (Ssse3.IsSupported && bytesPerPixel is 4)
        {
            DecodeSsse3(scanline, previousScanline);
        }
        else if (AdvSimd.Arm64.IsSupported && bytesPerPixel is 4)
        {
            DecodeArm(scanline, previousScanline);
        }
        else
        {
            DecodeScalar(scanline, previousScanline, (uint)bytesPerPixel);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeSsse3(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        Vector128<byte> b = Vector128<byte>.Zero;
        Vector128<byte> d = Vector128<byte>.Zero;

        int rb = scanline.Length;
        nuint offset = 1;
        while (rb >= 4)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);

            // It's easiest to do this math (particularly, deal with pc) with 16-bit intermediates.
            Vector128<byte> c = b;
            Vector128<byte> a = d;
            b = Sse2.UnpackLow(
                Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref Unsafe.Add(ref prevBaseRef, offset))).AsByte(),
                Vector128<byte>.Zero);
            d = Sse2.UnpackLow(
                Sse2.ConvertScalarToVector128Int32(Unsafe.As<byte, int>(ref scanRef)).AsByte(),
                Vector128<byte>.Zero);

            // (p-a) == (a+b-c - a) == (b-c)
            Vector128<short> pa = Sse2.Subtract(b.AsInt16(), c.AsInt16());

            // (p-b) == (a+b-c - b) == (a-c)
            Vector128<short> pb = Sse2.Subtract(a.AsInt16(), c.AsInt16());

            // (p-c) == (a+b-c - c) == (a+b-c-c) == (b-c)+(a-c)
            Vector128<short> pc = Sse2.Add(pa.AsInt16(), pb.AsInt16());

            pa = Ssse3.Abs(pa.AsInt16()).AsInt16(); /* |p-a| */
            pb = Ssse3.Abs(pb.AsInt16()).AsInt16(); /* |p-b| */
            pc = Ssse3.Abs(pc.AsInt16()).AsInt16(); /* |p-c| */

            Vector128<short> smallest = Sse2.Min(pc, Sse2.Min(pa, pb));

            // Paeth breaks ties favoring a over b over c.
            Vector128<byte> mask = SimdUtils.HwIntrinsics.BlendVariable(c, b, Sse2.CompareEqual(smallest, pb).AsByte());
            Vector128<byte> nearest = SimdUtils.HwIntrinsics.BlendVariable(mask, a, Sse2.CompareEqual(smallest, pa).AsByte());

            // Note `_epi8`: we need addition to wrap modulo 255.
            d = Sse2.Add(d, nearest);

            // Store the result.
            Unsafe.As<byte, int>(ref scanRef) = Sse2.ConvertToInt32(Sse2.PackUnsignedSaturate(d.AsInt16(), d.AsInt16()).AsInt32());

            rb -= 4;
            offset += 4;
        }
    }

    public static void DecodeArm(Span<byte> scanline, Span<byte> previousScanline)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        Vector128<byte> b = Vector128<byte>.Zero;
        Vector128<byte> d = Vector128<byte>.Zero;

        int rb = scanline.Length;
        nuint offset = 1;
        const int bytesPerBatch = 4;
        while (rb >= bytesPerBatch)
        {
            ref byte scanRef = ref Unsafe.Add(ref scanBaseRef, offset);
            Vector128<byte> c = b;
            Vector128<byte> a = d;
            b = AdvSimd.Arm64.ZipLow(
                Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref prevBaseRef, offset))).AsByte(),
                Vector128<byte>.Zero).AsByte();
            d = AdvSimd.Arm64.ZipLow(
                Vector128.CreateScalar(Unsafe.As<byte, int>(ref scanRef)).AsByte(),
                Vector128<byte>.Zero).AsByte();

            // (p-a) == (a+b-c - a) == (b-c)
            Vector128<short> pa = AdvSimd.Subtract(b.AsInt16(), c.AsInt16());

            // (p-b) == (a+b-c - b) == (a-c)
            Vector128<short> pb = AdvSimd.Subtract(a.AsInt16(), c.AsInt16());

            // (p-c) == (a+b-c - c) == (a+b-c-c) == (b-c)+(a-c)
            Vector128<short> pc = AdvSimd.Add(pa.AsInt16(), pb.AsInt16());

            pa = AdvSimd.Abs(pa.AsInt16()).AsInt16(); /* |p-a| */
            pb = AdvSimd.Abs(pb.AsInt16()).AsInt16(); /* |p-b| */
            pc = AdvSimd.Abs(pc.AsInt16()).AsInt16(); /* |p-c| */

            Vector128<short> smallest = AdvSimd.Min(pc, AdvSimd.Min(pa, pb));

            // Paeth breaks ties favoring a over b over c.
            Vector128<byte> mask = SimdUtils.HwIntrinsics.BlendVariable(c, b, AdvSimd.CompareEqual(smallest, pb).AsByte());
            Vector128<byte> nearest = SimdUtils.HwIntrinsics.BlendVariable(mask, a, AdvSimd.CompareEqual(smallest, pa).AsByte());

            d = AdvSimd.Add(d, nearest);

            Vector64<byte> e = AdvSimd.ExtractNarrowingSaturateUnsignedLower(d.AsInt16());

            Unsafe.As<byte, int>(ref scanRef) = Vector128.Create(e, e).AsInt32().ToScalar();

            rb -= bytesPerBatch;
            offset += bytesPerBatch;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeScalar(Span<byte> scanline, Span<byte> previousScanline, uint bytesPerPixel)
    {
        ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
        ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

        // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
        nuint offset = bytesPerPixel + 1; // Add one because x starts at one.
        nuint x = 1;
        for (; x < offset; x++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            scan = (byte)(scan + above);
        }

        for (; x < (uint)scanline.Length; x++)
        {
            ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
            byte above = Unsafe.Add(ref prevBaseRef, x);
            byte upperLeft = Unsafe.Add(ref prevBaseRef, x - bytesPerPixel);
            scan = (byte)(scan + PaethPredictor(left, above, upperLeft));
        }
    }

    /// <summary>
    /// Encodes a scanline and applies the paeth filter.
    /// </summary>
    /// <param name="scanline">The scanline to encode</param>
    /// <param name="previousScanline">The previous scanline.</param>
    /// <param name="result">The filtered scanline result.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    /// <param name="sum">The sum of the total variance of the filtered row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Encode(ReadOnlySpan<byte> scanline, ReadOnlySpan<byte> previousScanline, Span<byte> result, int bytesPerPixel, out int sum)
        => PngFilterEncoder.Encode<PaethFilterOperator>(scanline, previousScanline, result, (uint)bytesPerPixel, out sum);

    /// <summary>
    /// Computes a simple linear function of the three neighboring pixels (left, above, upper left), then chooses
    /// as predictor the neighboring pixel closest to the computed value.
    /// </summary>
    /// <param name="left">The left neighbor pixel.</param>
    /// <param name="above">The above neighbor pixel.</param>
    /// <param name="upperLeft">The upper left neighbor pixel.</param>
    /// <returns>
    /// The <see cref="byte"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte PaethPredictor(byte left, byte above, byte upperLeft)
    {
        int p = left + above - upperLeft;
        int pa = Numerics.Abs(p - left);
        int pb = Numerics.Abs(p - above);
        int pc = Numerics.Abs(p - upperLeft);

        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        if (pb <= pc)
        {
            return above;
        }

        return upperLeft;
    }
}
