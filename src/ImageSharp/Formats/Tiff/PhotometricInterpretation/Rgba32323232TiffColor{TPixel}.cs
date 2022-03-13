// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with an alpha channel and with 32 bits for each channel.
    /// </summary>
    internal class Rgba32323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        private readonly TiffExtraSampleType? extraSamplesType;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32323232TiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="extraSamplesType">The type of the extra samples.</param>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public Rgba32323232TiffColor(TiffExtraSampleType? extraSamplesType, bool isBigEndian)
        {
            this.extraSamplesType = extraSamplesType;
            this.isBigEndian = isBigEndian;
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            // Note: due to an issue with netcore 2.1 and default values and unpredictable behavior with those,
            // we define our own defaults as a workaround. See: https://github.com/dotnet/runtime/issues/55623
            var color = default(TPixel);
            color.FromVector4(TiffUtils.Vector4Default);

            bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                if (this.isBigEndian)
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ulong r = TiffUtils.ConvertToUIntBigEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong g = TiffUtils.ConvertToUIntBigEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong b = TiffUtils.ConvertToUIntBigEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong a = TiffUtils.ConvertToUIntBigEndian(data.Slice(offset, 4));
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
                        ulong r = TiffUtils.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong g = TiffUtils.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong b = TiffUtils.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                        offset += 4;

                        ulong a = TiffUtils.ConvertToUIntLittleEndian(data.Slice(offset, 4));
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
