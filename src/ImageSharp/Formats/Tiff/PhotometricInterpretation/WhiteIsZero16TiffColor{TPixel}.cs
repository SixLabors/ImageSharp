// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation for 16-bit grayscale images.
    /// </summary>
    internal class WhiteIsZero16TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhiteIsZero16TiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public WhiteIsZero16TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var l16 = default(L16);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);
                if (this.isBigEndian)
                {
                    for (int x = left; x < left + width; x++)
                    {
                        ushort intensity = (ushort)(ushort.MaxValue - TiffUtils.ConvertToShortBigEndian(data.Slice(offset, 2)));
                        offset += 2;

                        pixelRow[x] = TiffUtils.ColorFromL16(l16, intensity, color);
                    }
                }
                else
                {
                    for (int x = left; x < left + width; x++)
                    {
                        ushort intensity = (ushort)(ushort.MaxValue - TiffUtils.ConvertToShortLittleEndian(data.Slice(offset, 2)));
                        offset += 2;

                        pixelRow[x] = TiffUtils.ColorFromL16(l16, intensity, color);
                    }
                }
            }
        }
    }
}
