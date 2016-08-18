// <copyright file="PaethFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;

    /// <summary>
    /// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
    /// then chooses as predictor the neighboring pixel closest to the computed value. 
    /// This technique is due to Alan W. Paeth.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class PaethFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Decode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];
                byte above = previousScanline[x];
                byte upperLeft = (x - bytesPerPixel < 1) ? (byte)0 : previousScanline[x - bytesPerPixel];

                result[x] = (byte)((scanline[x] + PaethPredicator(left, above, upperLeft)) % 256);
            }

            return result;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            // Paeth(x) = Raw(x) - PaethPredictor(Raw(x-bpp), Prior(x), Prior(x - bpp))
            byte[] encodedScanline = new byte[scanline.Length + 1];
            encodedScanline[0] = (byte)FilterType.Paeth;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];
                byte above = previousScanline[x];
                byte upperLeft = (x - bytesPerPixel < 0) ? (byte)0 : previousScanline[x - bytesPerPixel];

                encodedScanline[x + 1] = (byte)((scanline[x] - PaethPredicator(left, above, upperLeft)) % 256);
            }

            return encodedScanline;
        }

        /// <summary>
        /// Computes a simple linear function of the three neighboring pixels (left, above, upper left), then chooses
        /// as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        /// <param name="left">The left neighbor pixel.</param>
        /// <param name="above">The above neighbor pixel.</param>
        /// <param name="upperLeft">The upper left neighbor pixel.</param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
        private static byte PaethPredicator(byte left, byte above, byte upperLeft)
        {
            int p = left + above - upperLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - above);
            int pc = Math.Abs(p - upperLeft);

            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            if (pb <= pc)
            {
                return above;
            }

            return upperLeft;
        }
    }
}
