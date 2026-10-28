// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes packed RGB with 32-bit integer channels into Rgb96 without losing any channel bits.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb323232TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            Span<Rgb96> exactRow = MemoryMarshal.Cast<TPixel, Rgb96>(pixelRow);
            int rowBytes = width * 12;
            ReadOnlySpan<byte> encoded = data.Slice(offset, rowBytes);

            // The factory selects this decoder only for Rgb96 output.
            // Copy integer words when byte order matches; otherwise reverse each word without converting to float.
            if (this.isBigEndian != BitConverter.IsLittleEndian)
            {
                MemoryMarshal.Cast<byte, Rgb96>(encoded).CopyTo(exactRow);
            }
            else
            {
                BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, uint>(encoded), MemoryMarshal.Cast<Rgb96, uint>(exactRow));
            }

            offset += rowBytes;
        }
    }
}
