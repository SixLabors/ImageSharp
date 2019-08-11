// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Implements the 'PaletteTiffColor' photometric interpretation (for all bit depths).
    /// </summary>
    internal static class PaletteTiffColor
    {
        /// <summary>
        /// Decodes pixel data using the current photometric interpretation.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="data">The buffer to read image data from.</param>
        /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
        /// <param name="colorMap">The RGB color lookup table to use for decoding the image.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode<TPixel>(byte[] data, uint[] bitsPerSample, uint[] colorMap, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            int colorCount = (int)Math.Pow(2, bitsPerSample[0]);
            TPixel[] palette = GeneratePalette<TPixel>(colorMap, colorCount);

            BitReader bitReader = new BitReader(data);

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> buffer = pixels.GetRowSpan(y);

                for (int x = left; x < left + width; x++)
                {
                    int index = bitReader.ReadBits(bitsPerSample[0]);
                    buffer[x] = palette[index];
                }

                bitReader.NextRow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TPixel[] GeneratePalette<TPixel>(uint[] colorMap, int colorCount)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel[] palette = new TPixel[colorCount];

            int rOffset = 0;
            int gOffset = colorCount;
            int bOffset = colorCount * 2;

            for (int i = 0; i < palette.Length; i++)
            {
                float r = colorMap[rOffset + i] / 65535F;
                float g = colorMap[gOffset + i] / 65535F;
                float b = colorMap[bOffset + i] / 65535F;
                palette[i].FromVector4(new Vector4(r, g, b, 1.0f));
            }

            return palette;
        }
    }
}
