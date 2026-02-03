// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 'Planar' layout for each color channel with 16 bit.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba16PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    private readonly TiffExtraSampleType? extraSamplesType;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba16PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="extraSamplesType">The extra samples type.</param>
    /// <param name="isBigEndian">If set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgba16PlanarTiffColor(TiffExtraSampleType? extraSamplesType, bool isBigEndian)
    {
        this.extraSamplesType = extraSamplesType;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> redData = data[0].GetSpan();
        Span<byte> greenData = data[1].GetSpan();
        Span<byte> blueData = data[2].GetSpan();
        Span<byte> alphaData = data[3].GetSpan();

        bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    ushort r = TiffUtilities.ConvertToUShortBigEndian(redData.Slice(offset, 2));
                    ushort g = TiffUtilities.ConvertToUShortBigEndian(greenData.Slice(offset, 2));
                    ushort b = TiffUtilities.ConvertToUShortBigEndian(blueData.Slice(offset, 2));
                    ushort a = TiffUtilities.ConvertToUShortBigEndian(alphaData.Slice(offset, 2));

                    offset += 2;

                    pixelRow[x] = hasAssociatedAlpha
                        ? TiffUtilities.ColorFromRgba64Premultiplied<TPixel>(r, g, b, a)
                        : TPixel.FromRgba64(new Rgba64(r, g, b, a));
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    ushort r = TiffUtilities.ConvertToUShortLittleEndian(redData.Slice(offset, 2));
                    ushort g = TiffUtilities.ConvertToUShortLittleEndian(greenData.Slice(offset, 2));
                    ushort b = TiffUtilities.ConvertToUShortLittleEndian(blueData.Slice(offset, 2));
                    ushort a = TiffUtilities.ConvertToUShortLittleEndian(alphaData.Slice(offset, 2));

                    offset += 2;

                    pixelRow[x] = hasAssociatedAlpha
                        ? TiffUtilities.ColorFromRgba64Premultiplied<TPixel>(r, g, b, a)
                        : TPixel.FromRgba64(new Rgba64(r, g, b, a));
                }
            }
        }
    }
}
