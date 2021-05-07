// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (optimized for bilevel images).
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BlackIsZero1TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public BlackIsZero1TiffColor()
        {
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            Color black = Color.Black;
            Color white = Color.White;
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x += 8)
                {
                    byte b = data[offset++];
                    int maxShift = Math.Min(left + width - x, 8);

                    for (int shift = 0; shift < maxShift; shift++)
                    {
                        int bit = (b >> (7 - shift)) & 1;

                        color.FromRgba32(bit == 0 ? black : white);

                        pixels[x + shift, y] = color;
                    }
                }
            }
        }
    }
}
