// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (optimised for 8-bit grayscale images).
    /// </summary>
    internal class BlackIsZero8TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public BlackIsZero8TiffColor()
        {
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var rgba = default(Rgba32);
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x++)
                {
                    byte intensity = data[offset++];

                    rgba.PackedValue = (uint)(intensity | (intensity << 8) | (intensity << 16) | (0xff << 24));
                    color.FromRgba32(rgba);

                    pixels[x, y] = color;
                }
            }
        }
    }
}
