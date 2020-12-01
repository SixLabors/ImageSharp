// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimised for 4-bit grayscale images).
    /// </summary>
    internal class WhiteIsZero4TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public WhiteIsZero4TiffColor()
        {
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;
            bool isOddWidth = (width & 1) == 1;

            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width - 1; x += 2)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)((15 - ((byteData & 0xF0) >> 4)) * 17);
                    color.FromRgba32(new Rgba32(intensity1, intensity1, intensity1, 255));
                    pixels[x, y] = color;

                    byte intensity2 = (byte)((15 - (byteData & 0x0F)) * 17);
                    color.FromRgba32(new Rgba32(intensity2, intensity2, intensity2, 255));
                    pixels[x + 1, y] = color;
                }

                if (isOddWidth)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)((15 - ((byteData & 0xF0) >> 4)) * 17);
                    color.FromRgba32(new Rgba32(intensity1, intensity1, intensity1, 255));
                    pixels[left + width - 1, y] = color;
                }
            }
        }
    }
}
