// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 'Planar' layout for each color channel with 24 bit.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb24PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb24PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb24PlanarTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> buffer = stackalloc byte[4];
        int bufferStartIdx = this.isBigEndian ? 1 : 0;

        Span<byte> redData = data[0].GetSpan();
        Span<byte> greenData = data[1].GetSpan();
        Span<byte> blueData = data[2].GetSpan();
        Span<byte> bufferSpan = buffer[bufferStartIdx..];

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    redData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint r = TiffUtilities.ConvertToUIntBigEndian(buffer);
                    greenData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint g = TiffUtilities.ConvertToUIntBigEndian(buffer);
                    blueData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint b = TiffUtilities.ConvertToUIntBigEndian(buffer);

                    offset += 3;

                    pixelRow[x] = TiffUtilities.ColorScaleTo24Bit<TPixel>(r, g, b);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    redData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint r = TiffUtilities.ConvertToUIntLittleEndian(buffer);
                    greenData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint g = TiffUtilities.ConvertToUIntLittleEndian(buffer);
                    blueData.Slice(offset, 3).CopyTo(bufferSpan);
                    uint b = TiffUtilities.ConvertToUIntLittleEndian(buffer);

                    offset += 3;

                    pixelRow[x] = TiffUtilities.ColorScaleTo24Bit<TPixel>(r, g, b);
                }
            }
        }
    }
}
