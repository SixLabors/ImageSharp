// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 32 bits for each channel.
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

            if (typeof(TPixel) == typeof(Rgb96))
            {
                // Keep uint samples in their native integer representation. A Vector4
                // conversion would discard low bits before the pixel is stored.
                Span<Rgb96> exactRow = MemoryMarshal.Cast<TPixel, Rgb96>(pixelRow);
                int rowBytes = width * 12;
                if (this.isBigEndian != BitConverter.IsLittleEndian)
                {
                    MemoryMarshal.Cast<byte, Rgb96>(data.Slice(offset, rowBytes)).CopyTo(exactRow);
                }
                else
                {
                    ReadOnlySpan<uint> samples = MemoryMarshal.Cast<byte, uint>(data.Slice(offset, rowBytes));
                    for (int x = 0; x < exactRow.Length; x++)
                    {
                        int sample = x * 3;
                        exactRow[x] = new Rgb96(
                            BinaryPrimitives.ReverseEndianness(samples[sample]),
                            BinaryPrimitives.ReverseEndianness(samples[sample + 1]),
                            BinaryPrimitives.ReverseEndianness(samples[sample + 2]));
                    }
                }

                offset += rowBytes;
                continue;
            }

            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntBigEndian(data.Slice(offset, 4));
                    offset += 4;

                    uint g = TiffUtilities.ConvertToUIntBigEndian(data.Slice(offset, 4));
                    offset += 4;

                    uint b = TiffUtilities.ConvertToUIntBigEndian(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                    offset += 4;

                    uint g = TiffUtilities.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                    offset += 4;

                    uint b = TiffUtilities.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b);
                }
            }
        }
    }
}
