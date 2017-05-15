// <copyright file="BlackIsZeroTiffColor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Tiff
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using ImageSharp;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (for all bit depths).
    /// </summary>
    internal static class BlackIsZeroTiffColor
    {
        /// <summary>
        /// Decodes pixel data using the current photometric interpretation.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="data">The buffer to read image data from.</param>
        /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode<TPixel>(byte[] data, uint[] bitsPerSample, PixelAccessor<TPixel> pixels, int left, int top, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default(TPixel);

            BitReader bitReader = new BitReader(data);
            float factor = (float)Math.Pow(2, bitsPerSample[0]) - 1.0f;

            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x++)
                {
                    int value = bitReader.ReadBits(bitsPerSample[0]);
                    float intensity = ((float)value) / factor;
                    color.PackFromVector4(new Vector4(intensity, intensity, intensity, 1.0f));
                    pixels[x, y] = color;
                }

                bitReader.NextRow();
            }
        }
    }
}
