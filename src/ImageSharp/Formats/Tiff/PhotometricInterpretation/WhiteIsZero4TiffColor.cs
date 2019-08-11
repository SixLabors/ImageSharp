// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimised for 4-bit grayscale images).
    /// </summary>
    internal static class WhiteIsZero4TiffColor
    {
        /// <summary>
        /// Decodes pixel data using the current photometric interpretation.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="data">The buffer to read image data from.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode<TPixel>(byte[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default(TPixel);

            uint offset = 0;
            bool isOddWidth = (width & 1) == 1;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> buffer = pixels.GetRowSpan(y);

                for (int x = left; x < left + width - 1; x += 2)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)((15 - ((byteData & 0xF0) >> 4)) * 17);
                    color.FromRgba32(new Rgba32(intensity1, intensity1, intensity1, 255));
                    buffer[x] = color;

                    byte intensity2 = (byte)((15 - (byteData & 0x0F)) * 17);
                    color.FromRgba32(new Rgba32(intensity2, intensity2, intensity2, 255));
                    buffer[x + 1] = color;
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
