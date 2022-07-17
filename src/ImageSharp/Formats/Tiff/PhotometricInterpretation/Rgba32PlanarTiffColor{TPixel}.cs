// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with an alpha channel and a 'Planar' layout for each color channel with 32 bit.
    /// </summary>
    internal class Rgba32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        private readonly TiffExtraSampleType? extraSamplesType;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32PlanarTiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="extraSamplesType">The extra samples type.</param>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public Rgba32PlanarTiffColor(TiffExtraSampleType? extraSamplesType, bool isBigEndian)
        {
            this.extraSamplesType = extraSamplesType;
            this.isBigEndian = isBigEndian;
        }

        /// <inheritdoc/>
        public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);
            color.FromScaledVector4(Vector4.Zero);

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
                        ulong r = TiffUtils.ConvertToUIntBigEndian(redData.Slice(offset, 4));
                        ulong g = TiffUtils.ConvertToUIntBigEndian(greenData.Slice(offset, 4));
                        ulong b = TiffUtils.ConvertToUIntBigEndian(blueData.Slice(offset, 4));
                        ulong a = TiffUtils.ConvertToUIntBigEndian(alphaData.Slice(offset, 4));

                        offset += 4;

                        pixelRow[x] = hasAssociatedAlpha ?
                            TiffUtils.ColorScaleTo32BitPremultiplied(r, g, b, a, color) :
                            TiffUtils.ColorScaleTo32Bit(r, g, b, a, color);
                    }
                }
                else
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ulong r = TiffUtils.ConvertToUIntLittleEndian(redData.Slice(offset, 4));
                        ulong g = TiffUtils.ConvertToUIntLittleEndian(greenData.Slice(offset, 4));
                        ulong b = TiffUtils.ConvertToUIntLittleEndian(blueData.Slice(offset, 4));
                        ulong a = TiffUtils.ConvertToUIntLittleEndian(alphaData.Slice(offset, 4));

                        offset += 4;

                        pixelRow[x] = hasAssociatedAlpha ?
                            TiffUtils.ColorScaleTo32BitPremultiplied(r, g, b, a, color) :
                            TiffUtils.ColorScaleTo32Bit(r, g, b, a, color);
                    }
                }
            }
        }
    }
}
