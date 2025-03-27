// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Utility methods for lossy and lossless webp format.
/// </summary>
internal static class WebpCommonUtils
{
    public static WebpMetadata GetWebpMetadata<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image.Metadata.TryGetWebpMetadata(out WebpMetadata? webp))
        {
            return (WebpMetadata)webp.DeepClone();
        }

        if (image.Metadata.TryGetGifMetadata(out GifMetadata? gif))
        {
            AnimatedImageMetadata ani = gif.ToAnimatedImageMetadata();
            return WebpMetadata.FromAnimatedMetadata(ani);
        }

        if (image.Metadata.TryGetPngMetadata(out PngMetadata? png))
        {
            AnimatedImageMetadata ani = png.ToAnimatedImageMetadata();
            return WebpMetadata.FromAnimatedMetadata(ani);
        }

        // Return explicit new instance so we do not mutate the original metadata.
        return new();
    }

    public static WebpFrameMetadata GetWebpFrameMetadata<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (frame.Metadata.TryGetWebpFrameMetadata(out WebpFrameMetadata? webp))
        {
            return (WebpFrameMetadata)webp.DeepClone();
        }

        if (frame.Metadata.TryGetGifMetadata(out GifFrameMetadata? gif))
        {
            AnimatedImageFrameMetadata ani = gif.ToAnimatedImageFrameMetadata();
            return WebpFrameMetadata.FromAnimatedMetadata(ani);
        }

        if (frame.Metadata.TryGetPngMetadata(out PngFrameMetadata? png))
        {
            AnimatedImageFrameMetadata ani = png.ToAnimatedImageFrameMetadata();
            return WebpFrameMetadata.FromAnimatedMetadata(ani);
        }

        // Return explicit new instance so we do not mutate the original metadata.
        return new();
    }

    /// <summary>
    /// Checks if the pixel row is not opaque.
    /// </summary>
    /// <param name="row">The row to check.</param>
    /// <returns>Returns true if alpha has non-0xff values.</returns>
    public static unsafe bool CheckNonOpaque(ReadOnlySpan<Bgra32> row)
    {
        if (Avx2.IsSupported)
        {
            ReadOnlySpan<byte> rowBytes = MemoryMarshal.AsBytes(row);
            int i = 0;
            int length = (row.Length * 4) - 3;
            fixed (byte* src = rowBytes)
            {
                Vector256<byte> alphaMaskVector256 = Vector256.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
                Vector256<byte> all0x80Vector256 = Vector256.Create((byte)0x80).AsByte();

                for (; i + 128 <= length; i += 128)
                {
                    Vector256<byte> a0 = Avx.LoadVector256(src + i).AsByte();
                    Vector256<byte> a1 = Avx.LoadVector256(src + i + 32).AsByte();
                    Vector256<byte> a2 = Avx.LoadVector256(src + i + 64).AsByte();
                    Vector256<byte> a3 = Avx.LoadVector256(src + i + 96).AsByte();
                    Vector256<int> b0 = Avx2.And(a0, alphaMaskVector256).AsInt32();
                    Vector256<int> b1 = Avx2.And(a1, alphaMaskVector256).AsInt32();
                    Vector256<int> b2 = Avx2.And(a2, alphaMaskVector256).AsInt32();
                    Vector256<int> b3 = Avx2.And(a3, alphaMaskVector256).AsInt32();
                    Vector256<short> c0 = Avx2.PackSignedSaturate(b0, b1).AsInt16();
                    Vector256<short> c1 = Avx2.PackSignedSaturate(b2, b3).AsInt16();
                    Vector256<byte> d = Avx2.PackSignedSaturate(c0, c1).AsByte();
                    Vector256<byte> bits = Avx2.CompareEqual(d, all0x80Vector256);
                    int mask = Avx2.MoveMask(bits);
                    if (mask != -1)
                    {
                        return true;
                    }
                }

                for (; i + 64 <= length; i += 64)
                {
                    if (IsNoneOpaque64Bytes(src, i))
                    {
                        return true;
                    }
                }

                for (; i + 32 <= length; i += 32)
                {
                    if (IsNoneOpaque32Bytes(src, i))
                    {
                        return true;
                    }
                }

                for (; i <= length; i += 4)
                {
                    if (src[i + 3] != 0xFF)
                    {
                        return true;
                    }
                }
            }
        }
        else if (Sse2.IsSupported)
        {
            ReadOnlySpan<byte> rowBytes = MemoryMarshal.AsBytes(row);
            int i = 0;
            int length = (row.Length * 4) - 3;
            fixed (byte* src = rowBytes)
            {
                for (; i + 64 <= length; i += 64)
                {
                    if (IsNoneOpaque64Bytes(src, i))
                    {
                        return true;
                    }
                }

                for (; i + 32 <= length; i += 32)
                {
                    if (IsNoneOpaque32Bytes(src, i))
                    {
                        return true;
                    }
                }

                for (; i <= length; i += 4)
                {
                    if (src[i + 3] != 0xFF)
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x].A != 0xFF)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe bool IsNoneOpaque64Bytes(byte* src, int i)
    {
        Vector128<byte> alphaMask = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);

        Vector128<byte> a0 = Sse2.LoadVector128(src + i).AsByte();
        Vector128<byte> a1 = Sse2.LoadVector128(src + i + 16).AsByte();
        Vector128<byte> a2 = Sse2.LoadVector128(src + i + 32).AsByte();
        Vector128<byte> a3 = Sse2.LoadVector128(src + i + 48).AsByte();
        Vector128<int> b0 = Sse2.And(a0, alphaMask).AsInt32();
        Vector128<int> b1 = Sse2.And(a1, alphaMask).AsInt32();
        Vector128<int> b2 = Sse2.And(a2, alphaMask).AsInt32();
        Vector128<int> b3 = Sse2.And(a3, alphaMask).AsInt32();
        Vector128<short> c0 = Sse2.PackSignedSaturate(b0, b1).AsInt16();
        Vector128<short> c1 = Sse2.PackSignedSaturate(b2, b3).AsInt16();
        Vector128<byte> d = Sse2.PackSignedSaturate(c0, c1).AsByte();
        Vector128<byte> bits = Sse2.CompareEqual(d, Vector128.Create((byte)0x80).AsByte());
        int mask = Sse2.MoveMask(bits);
        return mask != 0xFFFF;
    }

    private static unsafe bool IsNoneOpaque32Bytes(byte* src, int i)
    {
        Vector128<byte> alphaMask = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);

        Vector128<byte> a0 = Sse2.LoadVector128(src + i).AsByte();
        Vector128<byte> a1 = Sse2.LoadVector128(src + i + 16).AsByte();
        Vector128<int> b0 = Sse2.And(a0, alphaMask).AsInt32();
        Vector128<int> b1 = Sse2.And(a1, alphaMask).AsInt32();
        Vector128<short> c = Sse2.PackSignedSaturate(b0, b1).AsInt16();
        Vector128<byte> d = Sse2.PackSignedSaturate(c, c).AsByte();
        Vector128<byte> bits = Sse2.CompareEqual(d, Vector128.Create((byte)0x80).AsByte());
        int mask = Sse2.MoveMask(bits);
        return mask != 0xFFFF;
    }
}
