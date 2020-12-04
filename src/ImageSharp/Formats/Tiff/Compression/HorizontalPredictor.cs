// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Methods for undoing the horizontal prediction used in combination with deflate and LZW compressed TIFF images.
    /// </summary>
    public static class HorizontalPredictor
    {
        /// <summary>
        /// Inverts the horizontal prediction.
        /// </summary>
        /// <param name="pixelBytes">Buffer with decompressed pixel data.</param>
        /// <param name="width">The width of the image or strip.</param>
        /// <param name="bitsPerPixel">Bits per pixel.</param>
        public static void Undo(Span<byte> pixelBytes, int width, int bitsPerPixel)
        {
            if (bitsPerPixel == 8)
            {
                Undo8Bit(pixelBytes, width);
            }
            else if (bitsPerPixel == 24)
            {
                Undo24Bit(pixelBytes, width);
            }
        }

        private static void Undo8Bit(Span<byte> pixelBytes, int width)
        {
            var rowBytesCount = width;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);

                byte pixelValue = rowBytes[0];
                for (int x = 1; x < width; x++)
                {
                    pixelValue += rowBytes[x];
                    rowBytes[x] = pixelValue;
                }
            }
        }

        private static void Undo24Bit(Span<byte> pixelBytes, int width)
        {
            var rowBytesCount = width * 3;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowBytes);

                byte r = rowRgb[0].R;
                byte g = rowRgb[0].G;
                byte b = rowRgb[0].B;
                for (int x = 1; x < width; x++)
                {
                    ref Rgb24 pixel = ref rowRgb[x];
                    r += rowRgb[x].R;
                    g += rowRgb[x].G;
                    b += rowRgb[x].B;
                    var rgb = new Rgb24(r, g, b);
                    pixel.FromRgb24(rgb);
                }
            }
        }
    }
}
