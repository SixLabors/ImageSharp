// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Reverses the pixel mangling applied by Apple's CgBI PNG variant. CgBI files
/// (emitted by <c>pngcrush -iphone</c>) swap channel order from RGB(A) to BGR(A)
/// and premultiply RGB samples by alpha. This converts a defiltered scanline back
/// to standard PNG semantics in place so the existing scanline processors can
/// consume it unchanged. CgBI is only emitted for 8-bit truecolor (with or
/// without alpha); other color types are left alone.
/// </summary>
/// <remarks>
/// See https://theapplewiki.com/wiki/PNG_CgBI_Format
/// </remarks>
internal static class PngCgbiProcessor
{
    // Per-pixel byte indices that swap CgBI's BGRA layout to Rgba32's RGBA.
    // MMShuffle3012 expands to [2, 1, 0, 3] per 4-byte pixel; the same 64-byte
    // sequence seeds all three shuffle masks (Vector128/256 take a leading slice).
    private static readonly byte[] BgraToRgbaShuffleBytes = BuildShuffleBytes();

    private static readonly Vector128<byte> BgraToRgbaShuffle128 = Vector128.Create(new ReadOnlySpan<byte>(BgraToRgbaShuffleBytes, 0, Vector128<byte>.Count));

    private static readonly Vector256<byte> BgraToRgbaShuffle256 = Vector256.Create(new ReadOnlySpan<byte>(BgraToRgbaShuffleBytes, 0, Vector256<byte>.Count));

    private static readonly Vector512<byte> BgraToRgbaShuffle512 = Vector512.Create(BgraToRgbaShuffleBytes);

    /// <summary>
    /// Applies the inverse of Apple's CgBI pixel mangling to a defiltered scanline in place.
    /// </summary>
    /// <param name="configuration">The configuration used by the Rgb24 R/B swap.</param>
    /// <param name="scanline">The defiltered pixel bytes (without the leading filter byte).</param>
    /// <param name="colorType">The PNG color type from IHDR.</param>
    public static void ApplyTransform(Configuration configuration, Span<byte> scanline, PngColorType colorType)
    {
        if (colorType == PngColorType.RgbWithAlpha)
        {
            Span<Rgba32> pixels = MemoryMarshal.Cast<byte, Rgba32>(scanline);
            int i = 0;

            if (Vector512.IsHardwareAccelerated && pixels.Length >= Vector512<int>.Count)
            {
                i = ApplyTransformVector512(scanline, pixels.Length);
            }

            if (Vector256.IsHardwareAccelerated && Avx2.IsSupported && (pixels.Length - i) >= Vector256<int>.Count)
            {
                i = ApplyTransformVector256(scanline, i, pixels.Length);
            }

            if (Vector128.IsHardwareAccelerated && (pixels.Length - i) >= Vector128<int>.Count)
            {
                i = ApplyTransformVector128(scanline, i, pixels.Length);
            }

            for (; i < pixels.Length; i++)
            {
                ref Rgba32 pixel = ref pixels[i];
                pixel = new Rgba32(pixel.B, pixel.G, pixel.R, pixel.A);
                UndoPremultiplicationScalar(ref pixel);
            }
        }
        else if (colorType == PngColorType.Rgb)
        {
            // No alpha channel, so just swap R and B using built in SIMD-optimized pixel operations.
            Span<Rgb24> target = MemoryMarshal.Cast<byte, Rgb24>(scanline);
            PixelOperations<Rgb24>.Instance.FromBgr24Bytes(configuration, scanline, target, target.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UndoPremultiplicationScalar(ref Rgba32 pixel)
    {
        byte a = pixel.A;
        if (a is 0 or byte.MaxValue)
        {
            return;
        }

        // Reverse: c' = c * a / 255  =>  c = round(c' * 255 / a)
        int half = a >> 1;
        byte r = (byte)Math.Min(byte.MaxValue, ((pixel.R * byte.MaxValue) + half) / a);
        byte g = (byte)Math.Min(byte.MaxValue, ((pixel.G * byte.MaxValue) + half) / a);
        byte b = (byte)Math.Min(byte.MaxValue, ((pixel.B * byte.MaxValue) + half) / a);
        pixel = new Rgba32(r, g, b, a);
    }

    private static int ApplyTransformVector512(Span<byte> scanline, int pixelCount)
    {
        ref byte scanlineRef = ref MemoryMarshal.GetReference(scanline);
        int i = 0;

        // Indices stay within their own 4-byte pixel, so the per-pixel pattern
        // is also valid under the per-128-bit-lane vpshufb that ShuffleNative
        // selects on AVX-512BW hosts.
        Vector512<byte> shuffleMask = BgraToRgbaShuffle512;

        Vector512<int> zero = Vector512<int>.Zero;
        Vector512<int> one = Vector512<int>.One;
        Vector512<int> byteMax = Vector512.Create((int)byte.MaxValue);

        for (; i <= pixelCount - Vector512<int>.Count; i += Vector512<int>.Count)
        {
            ref byte blockRef = ref Unsafe.Add(ref scanlineRef, i * Unsafe.SizeOf<Rgba32>());
            Vector512<byte> bgra = Unsafe.ReadUnaligned<Vector512<byte>>(ref blockRef);
            Vector512<byte> rgba = Vector512_.ShuffleNative(bgra, shuffleMask);
            Vector512<int> packed = rgba.AsInt32();
            Vector512<int> alpha = Vector512.ShiftRightLogical(packed, 24);

            // Fully transparent and fully opaque pixels are identity cases for
            // unpremultiplication. Masking them keeps the scalar behavior and lets
            // safeAlpha avoid dividing by zero for alpha == 0.
            Vector512<int> partialMask = ~(Vector512.Equals(alpha, zero) | Vector512.Equals(alpha, byteMax));

            Vector512<int> r = packed & byteMax;
            Vector512<int> g = Vector512.ShiftRightLogical(packed, 8) & byteMax;
            Vector512<int> b = Vector512.ShiftRightLogical(packed, 16) & byteMax;

            Vector512<int> safeAlpha = Vector512.ConditionalSelect(partialMask, alpha, one);
            Vector512<int> halfAlpha = Vector512.ShiftRightLogical(safeAlpha, 1);
            Vector512<float> safeAlphaF = Vector512.ConvertToSingle(safeAlpha);

            // ConvertToInt32 truncates toward zero (cvttps2dq / fcvtzs); since
            // every quotient here is non-negative, that matches the scalar
            // ((c * 255) + (a >> 1)) / a integer-division floor.
            Vector512<int> unpremultipliedR = Vector512.Min(
                byteMax,
                Vector512.ConvertToInt32(Vector512.ConvertToSingle((r * byteMax) + halfAlpha) / safeAlphaF));

            Vector512<int> unpremultipliedG = Vector512.Min(
                byteMax,
                Vector512.ConvertToInt32(Vector512.ConvertToSingle((g * byteMax) + halfAlpha) / safeAlphaF));

            Vector512<int> unpremultipliedB = Vector512.Min(
                byteMax,
                Vector512.ConvertToInt32(Vector512.ConvertToSingle((b * byteMax) + halfAlpha) / safeAlphaF));

            // ConditionalSelect applies the expensive unpremultiply only to pixels
            // where alpha is between 1 and 254; alpha 0 and 255 lanes keep the
            // shuffled channel values exactly as the scalar path does.
            Vector512<int> finalR = Vector512.ConditionalSelect(partialMask, unpremultipliedR, r);
            Vector512<int> finalG = Vector512.ConditionalSelect(partialMask, unpremultipliedG, g);
            Vector512<int> finalB = Vector512.ConditionalSelect(partialMask, unpremultipliedB, b);

            // Rgba32 is laid out as little-endian 0xAABBGGRR in an int lane, so
            // shifting the unpacked channels back to byte offsets 0, 1, 2, and 3
            // recreates the in-memory RGBA bytes for the unaligned store.
            Vector512<int> result =
                finalR |
                Vector512.ShiftLeft(finalG, 8) |
                Vector512.ShiftLeft(finalB, 16) |
                Vector512.ShiftLeft(alpha, 24);

            Unsafe.WriteUnaligned(ref blockRef, result.AsByte());
        }

        return i;
    }

    private static int ApplyTransformVector256(Span<byte> scanline, int startPixel, int pixelCount)
    {
        ref byte scanlineRef = ref MemoryMarshal.GetReference(scanline);
        int i = startPixel;

        // vpshufb is 128-bit lane-local and uses only the low 4 bits of each
        // index, so the same per-pixel [2,1,0,3] pattern in both lanes keeps
        // every byte inside its own lane.
        Vector256<byte> shuffleMask = BgraToRgbaShuffle256;

        Vector256<int> zero = Vector256<int>.Zero;
        Vector256<int> one = Vector256<int>.One;
        Vector256<int> byteMax = Vector256.Create((int)byte.MaxValue);

        for (; i <= pixelCount - Vector256<int>.Count; i += Vector256<int>.Count)
        {
            ref byte blockRef = ref Unsafe.Add(ref scanlineRef, i * Unsafe.SizeOf<Rgba32>());
            Vector256<byte> bgra = Unsafe.ReadUnaligned<Vector256<byte>>(ref blockRef);
            Vector256<byte> rgba = Vector256_.ShufflePerLane(bgra, shuffleMask);
            Vector256<int> packed = rgba.AsInt32();
            Vector256<int> alpha = Vector256.ShiftRightLogical(packed, 24);

            // Fully transparent and fully opaque pixels are identity cases for
            // unpremultiplication. Masking them keeps the scalar behavior and lets
            // safeAlpha avoid dividing by zero for alpha == 0.
            Vector256<int> partialMask = ~(Vector256.Equals(alpha, zero) | Vector256.Equals(alpha, byteMax));

            Vector256<int> r = packed & byteMax;
            Vector256<int> g = Vector256.ShiftRightLogical(packed, 8) & byteMax;
            Vector256<int> b = Vector256.ShiftRightLogical(packed, 16) & byteMax;

            Vector256<int> safeAlpha = Vector256.ConditionalSelect(partialMask, alpha, one);
            Vector256<int> halfAlpha = Vector256.ShiftRightLogical(safeAlpha, 1);
            Vector256<float> safeAlphaF = Vector256.ConvertToSingle(safeAlpha);

            // ConvertToInt32 truncates toward zero (cvttps2dq / fcvtzs); since
            // every quotient here is non-negative, that matches the scalar
            // ((c * 255) + (a >> 1)) / a integer-division floor.
            Vector256<int> unpremultipliedR = Vector256.Min(
                byteMax,
                Vector256.ConvertToInt32(Vector256.ConvertToSingle((r * byteMax) + halfAlpha) / safeAlphaF));

            Vector256<int> unpremultipliedG = Vector256.Min(
                byteMax,
                Vector256.ConvertToInt32(Vector256.ConvertToSingle((g * byteMax) + halfAlpha) / safeAlphaF));

            Vector256<int> unpremultipliedB = Vector256.Min(
                byteMax,
                Vector256.ConvertToInt32(Vector256.ConvertToSingle((b * byteMax) + halfAlpha) / safeAlphaF));

            // ConditionalSelect applies the expensive unpremultiply only to pixels
            // where alpha is between 1 and 254; alpha 0 and 255 lanes keep the
            // shuffled channel values exactly as the scalar path does.
            Vector256<int> finalR = Vector256.ConditionalSelect(partialMask, unpremultipliedR, r);
            Vector256<int> finalG = Vector256.ConditionalSelect(partialMask, unpremultipliedG, g);
            Vector256<int> finalB = Vector256.ConditionalSelect(partialMask, unpremultipliedB, b);

            // Rgba32 is laid out as little-endian 0xAABBGGRR in an int lane, so
            // shifting the unpacked channels back to byte offsets 0, 1, 2, and 3
            // recreates the in-memory RGBA bytes for the unaligned store.
            Vector256<int> result =
                finalR |
                Vector256.ShiftLeft(finalG, 8) |
                Vector256.ShiftLeft(finalB, 16) |
                Vector256.ShiftLeft(alpha, 24);

            Unsafe.WriteUnaligned(ref blockRef, result.AsByte());
        }

        return i;
    }

    private static int ApplyTransformVector128(Span<byte> scanline, int startPixel, int pixelCount)
    {
        ref byte scanlineRef = ref MemoryMarshal.GetReference(scanline);
        int i = startPixel;

        Vector128<byte> shuffleMask = BgraToRgbaShuffle128;

        Vector128<int> zero = Vector128<int>.Zero;
        Vector128<int> one = Vector128<int>.One;
        Vector128<int> byteMax = Vector128.Create((int)byte.MaxValue);

        for (; i <= pixelCount - Vector128<int>.Count; i += Vector128<int>.Count)
        {
            ref byte blockRef = ref Unsafe.Add(ref scanlineRef, i * Unsafe.SizeOf<Rgba32>());
            Vector128<byte> bgra = Unsafe.ReadUnaligned<Vector128<byte>>(ref blockRef);
            Vector128<byte> rgba = Vector128_.ShuffleNative(bgra, shuffleMask);
            Vector128<int> packed = rgba.AsInt32();
            Vector128<int> alpha = Vector128.ShiftRightLogical(packed, 24);

            // Fully transparent and fully opaque pixels are identity cases for
            // unpremultiplication. Masking them keeps the scalar behavior and lets
            // safeAlpha avoid dividing by zero for alpha == 0.
            Vector128<int> partialMask = ~(Vector128.Equals(alpha, zero) | Vector128.Equals(alpha, byteMax));

            Vector128<int> r = packed & byteMax;
            Vector128<int> g = Vector128.ShiftRightLogical(packed, 8) & byteMax;
            Vector128<int> b = Vector128.ShiftRightLogical(packed, 16) & byteMax;

            Vector128<int> safeAlpha = Vector128.ConditionalSelect(partialMask, alpha, one);
            Vector128<int> halfAlpha = Vector128.ShiftRightLogical(safeAlpha, 1);
            Vector128<float> safeAlphaF = Vector128.ConvertToSingle(safeAlpha);

            // ConvertToInt32 truncates toward zero (cvttps2dq / fcvtzs); since
            // every quotient here is non-negative, that matches the scalar
            // ((c * 255) + (a >> 1)) / a integer-division floor.
            Vector128<int> unpremultipliedR = Vector128.Min(
                byteMax,
                Vector128.ConvertToInt32(Vector128.ConvertToSingle((r * byteMax) + halfAlpha) / safeAlphaF));

            Vector128<int> unpremultipliedG = Vector128.Min(
                byteMax,
                Vector128.ConvertToInt32(Vector128.ConvertToSingle((g * byteMax) + halfAlpha) / safeAlphaF));

            Vector128<int> unpremultipliedB = Vector128.Min(
                byteMax,
                Vector128.ConvertToInt32(Vector128.ConvertToSingle((b * byteMax) + halfAlpha) / safeAlphaF));

            // ConditionalSelect applies the expensive unpremultiply only to pixels
            // where alpha is between 1 and 254; alpha 0 and 255 lanes keep the
            // shuffled channel values exactly as the scalar path does.
            Vector128<int> finalR = Vector128.ConditionalSelect(partialMask, unpremultipliedR, r);
            Vector128<int> finalG = Vector128.ConditionalSelect(partialMask, unpremultipliedG, g);
            Vector128<int> finalB = Vector128.ConditionalSelect(partialMask, unpremultipliedB, b);

            // Rgba32 is laid out as little-endian 0xAABBGGRR in an int lane, so
            // shifting the unpacked channels back to byte offsets 0, 1, 2, and 3
            // recreates the in-memory RGBA bytes for the unaligned store.
            Vector128<int> result =
                finalR |
                Vector128.ShiftLeft(finalG, 8) |
                Vector128.ShiftLeft(finalB, 16) |
                Vector128.ShiftLeft(alpha, 24);

            Unsafe.WriteUnaligned(ref blockRef, result.AsByte());
        }

        return i;
    }

    private static byte[] BuildShuffleBytes()
    {
        byte[] bytes = new byte[Vector512<byte>.Count];
        Span<byte> span = bytes;
        Shuffle.MMShuffleSpan(ref span, Shuffle.MMShuffle3012);

        return bytes;
    }
}
