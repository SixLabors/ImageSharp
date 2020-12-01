// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimised for bilevel images).
    /// </summary>
    internal class WhiteIsZero1TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public WhiteIsZero1TiffColor()
        {
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x += 8)
                {
                    byte b = data[offset++];
                    int maxShift = Math.Min(left + width - x, 8);

                    for (int shift = 0; shift < maxShift; shift++)
                    {
                        int bit = (b >> (7 - shift)) & 1;
                        byte intensity = (bit == 1) ? (byte)0 : (byte)255;

                        color.FromRgba32(new Rgba32(intensity, intensity, intensity, 255));
                        pixels[x + shift, y] = color;
                    }
                }
            }
        }
    }
}
