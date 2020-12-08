// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (optimised for 4-bit grayscale images).
    /// </summary>
    internal class BlackIsZero4TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public BlackIsZero4TiffColor()
        {
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;
            bool isOddWidth = (width & 1) == 1;

            var rgba = default(Rgba32);
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width - 1;)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)(((byteData & 0xF0) >> 4) * 17);
                    rgba.PackedValue = (uint)(intensity1 | (intensity1 << 8) | (intensity1 << 16) | (0xff << 24));
                    color.FromRgba32(rgba);

                    pixels[x++, y] = color;

                    byte intensity2 = (byte)((byteData & 0x0F) * 17);
                    rgba.PackedValue = (uint)(intensity2 | (intensity2 << 8) | (intensity2 << 16) | (0xff << 24));
                    color.FromRgba32(rgba);

                    pixels[x++, y] = color;
                }

                if (isOddWidth)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)(((byteData & 0xF0) >> 4) * 17);
                    rgba.PackedValue = (uint)(intensity1 | (intensity1 << 8) | (intensity1 << 16) | (0xff << 24));
                    color.FromRgba32(rgba);

                    pixels[left + width - 1, y] = color;
                }
            }
        }
    }
}
