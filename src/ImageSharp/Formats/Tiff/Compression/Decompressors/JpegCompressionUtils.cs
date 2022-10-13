// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

internal static class JpegCompressionUtils
{
    public static void CopyImageBytesToBuffer(Configuration configuration, Span<byte> buffer, Buffer2D<Rgb24> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<Rgb24> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<Rgb24>.Instance.ToRgb24Bytes(configuration, pixelRowSpan, buffer[offset..], pixelRowSpan.Length);
            offset += Unsafe.SizeOf<Rgb24>() * pixelRowSpan.Length;
        }
    }

    public static void CopyImageBytesToBuffer(Configuration configuration, Span<byte> buffer, Buffer2D<L8> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<L8> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<L8>.Instance.ToL8Bytes(configuration, pixelRowSpan, buffer[offset..], pixelRowSpan.Length);
            offset += Unsafe.SizeOf<L8>() * pixelRowSpan.Length;
        }
    }
}
