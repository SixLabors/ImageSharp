// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with 32 bits for each channel.
    /// </summary>
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
            var color = default(TPixel);
            color.FromScaledVector4(Vector4.Zero);
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

                        pixelRow[x] = TiffUtils.ColorScaleTo32Bit(r, g, b, color);
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

                        pixelRow[x] = TiffUtils.ColorScaleTo32Bit(r, g, b, color);
                    }
                }
            }
        }
    }
}
