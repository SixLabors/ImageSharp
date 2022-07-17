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
    /// Implements the 'RGB' photometric interpretation with 'Planar' layout for each color channel with 24 bit.
    /// </summary>
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
            var color = default(TPixel);
            color.FromScaledVector4(Vector4.Zero);
            Span<byte> buffer = stackalloc byte[4];
            int bufferStartIdx = this.isBigEndian ? 1 : 0;

            Span<byte> redData = data[0].GetSpan();
            Span<byte> greenData = data[1].GetSpan();
            Span<byte> blueData = data[2].GetSpan();
            Span<byte> bufferSpan = buffer.Slice(bufferStartIdx);

            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                if (this.isBigEndian)
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        redData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong r = TiffUtils.ConvertToUIntBigEndian(buffer);
                        greenData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong g = TiffUtils.ConvertToUIntBigEndian(buffer);
                        blueData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong b = TiffUtils.ConvertToUIntBigEndian(buffer);

                        offset += 3;

                        pixelRow[x] = TiffUtils.ColorScaleTo24Bit(r, g, b, color);
                    }
                }
                else
                {
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        redData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong r = TiffUtils.ConvertToUIntLittleEndian(buffer);
                        greenData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong g = TiffUtils.ConvertToUIntLittleEndian(buffer);
                        blueData.Slice(offset, 3).CopyTo(bufferSpan);
                        ulong b = TiffUtils.ConvertToUIntLittleEndian(buffer);

                        offset += 3;

                        pixelRow[x] = TiffUtils.ColorScaleTo24Bit(r, g, b, color);
                    }
                }
            }
        }
    }
}
