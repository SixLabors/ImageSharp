// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation with 16 bits for each channel.
    /// </summary>
    internal class Rgb161616TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool isBigEndian;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb161616TiffColor{TPixel}" /> class.
        /// </summary>
        /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
        public Rgb161616TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var rgba = default(Rgba64);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);

                for (int x = left; x < left + width; x++)
                {
                    ulong r = TiffUtils.ConvertToShort(data.Slice(offset, 2), this.isBigEndian);
                    offset += 2;
                    ulong g = TiffUtils.ConvertToShort(data.Slice(offset, 2), this.isBigEndian);
                    offset += 2;
                    ulong b = TiffUtils.ConvertToShort(data.Slice(offset, 2), this.isBigEndian);
                    offset += 2;

                    rgba.PackedValue = r | (g << 16) | (b << 32) | (0xfffful << 48);
                    color.FromRgba64(rgba);

                    pixelRow[x] = color;
                }
            }
        }
    }
}
