// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 32 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba32323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba32323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgba32323232TiffColor(bool isBigEndian)
    {
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            // Factory dispatch guarantees a straight Rgba128 destination here.
            Span<Rgba128> exactRow = MemoryMarshal.Cast<TPixel, Rgba128>(pixelRow);
            int rowBytes = width * 16;
            ReadOnlySpan<byte> row = data.Slice(offset, rowBytes);

            if (this.isBigEndian != BitConverter.IsLittleEndian)
            {
                MemoryMarshal.Cast<byte, Rgba128>(row).CopyTo(exactRow);
            }
            else
            {
                Span<uint> exactSamples = MemoryMarshal.Cast<Rgba128, uint>(exactRow);
                BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, uint>(row), exactSamples);
            }

            offset += rowBytes;
        }
    }
}
