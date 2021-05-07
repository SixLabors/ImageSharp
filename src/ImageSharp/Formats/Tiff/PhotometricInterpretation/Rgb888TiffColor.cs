// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation (optimized for 8-bit full color images).
    /// </summary>
    internal class Rgb888TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var rgba = default(Rgba32);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);

                for (int x = left; x < left + width; x++)
                {
                    byte r = data[offset++];
                    byte g = data[offset++];
                    byte b = data[offset++];

                    rgba.PackedValue = (uint)(r | (g << 8) | (b << 16) | (0xff << 24));
                    color.FromRgba32(rgba);

                    pixelRow[x] = color;
                }
            }
        }
    }
}
