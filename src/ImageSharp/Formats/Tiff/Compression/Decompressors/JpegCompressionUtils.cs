// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

internal static class JpegCompressionUtils
{
    public static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<Rgb24> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<Rgb24> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
            rgbBytes.CopyTo(buffer[offset..]);
            offset += rgbBytes.Length;
        }
    }

    public static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<L8> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<L8> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
            rgbBytes.CopyTo(buffer[offset..]);
            offset += rgbBytes.Length;
        }
    }
}
