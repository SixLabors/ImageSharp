// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Methods for undoing the horizontal prediction used in combination with deflate and LZW compressed TIFF images.
    /// </summary>
    internal static class HorizontalPredictor
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

        public static void ApplyHorizontalPrediction(Span<byte> rows, int width, int bitsPerPixel)
        {
            if (bitsPerPixel == 8)
            {
                ApplyHorizontalPrediction8Bit(rows, width);
            }
            else if (bitsPerPixel == 24)
            {
                ApplyHorizontalPrediction24Bit(rows, width);
            }
        }

        /// <summary>
        /// Applies a horizontal predictor to the rgb row.
        /// Make use of the fact that many continuous-tone images rarely vary much in pixel value from one pixel to the next.
        /// In such images, if we replace the pixel values by differences between consecutive pixels, many of the differences should be 0, plus
        /// or minus 1, and so on.This reduces the apparent information content and allows LZW to encode the data more compactly.
        /// </summary>
        /// <param name="rows">The rgb pixel rows.</param>
        /// <param name="width">The width.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ApplyHorizontalPrediction24Bit(Span<byte> rows, int width)
        {
            DebugGuard.IsTrue(rows.Length % width == 0, "Values must be equals");
            int height = rows.Length / width;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowSpan = rows.Slice(y * width, width);
                Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowSpan);

                for (int x = rowRgb.Length - 1; x >= 1; x--)
                {
                    byte r = (byte)(rowRgb[x].R - rowRgb[x - 1].R);
                    byte g = (byte)(rowRgb[x].G - rowRgb[x - 1].G);
                    byte b = (byte)(rowRgb[x].B - rowRgb[x - 1].B);
                    var rgb = new Rgb24(r, g, b);
                    rowRgb[x].FromRgb24(rgb);
                }
            }
        }

        /// <summary>
        /// Applies a horizontal predictor to a gray pixel row.
        /// </summary>
        /// <param name="rows">The gray pixel rows.</param>
        /// <param name="width">The width.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ApplyHorizontalPrediction8Bit(Span<byte> rows, int width)
        {
            DebugGuard.IsTrue(rows.Length % width == 0, "Values must be equals");
            int height = rows.Length / width;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowSpan = rows.Slice(y * width, width);
                for (int x = rowSpan.Length - 1; x >= 1; x--)
                {
                    rowSpan[x] -= rowSpan[x - 1];
                }
            }
        }

        private static void Undo8Bit(Span<byte> pixelBytes, int width)
        {
            int rowBytesCount = width;
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
            int rowBytesCount = width * 3;
            int height = pixelBytes.Length / rowBytesCount;
            for (int y = 0; y < height; y++)
            {
                Span<byte> rowBytes = pixelBytes.Slice(y * rowBytesCount, rowBytesCount);
                Span<Rgb24> rowRgb = MemoryMarshal.Cast<byte, Rgb24>(rowBytes).Slice(0, width);
                ref Rgb24 rowRgbBase = ref MemoryMarshal.GetReference(rowRgb);
                byte r = rowRgbBase.R;
                byte g = rowRgbBase.G;
                byte b = rowRgbBase.B;

                for (int x = 1; x < rowRgb.Length; x++)
                {
                    ref Rgb24 pixel = ref rowRgb[x];
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    var rgb = new Rgb24(r, g, b);
                    pixel.FromRgb24(rgb);
                }
            }
        }
    }
}
