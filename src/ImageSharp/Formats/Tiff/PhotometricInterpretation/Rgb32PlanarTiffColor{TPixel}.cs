// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 'Planar' layout for each color channel with 32 bit.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb32PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb32PlanarTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> redData = data[0].GetSpan();
        Span<byte> greenData = data[1].GetSpan();
        Span<byte> blueData = data[2].GetSpan();

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntBigEndian(redData.Slice(offset, 4));
                    uint g = TiffUtilities.ConvertToUIntBigEndian(greenData.Slice(offset, 4));
                    uint b = TiffUtilities.ConvertToUIntBigEndian(blueData.Slice(offset, 4));

                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntLittleEndian(redData.Slice(offset, 4));
                    uint g = TiffUtilities.ConvertToUIntLittleEndian(greenData.Slice(offset, 4));
                    uint b = TiffUtilities.ConvertToUIntLittleEndian(blueData.Slice(offset, 4));

                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b);
                }
            }
        }
    }
}
