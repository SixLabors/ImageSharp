// <copyright file="UpFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// The Up filter is just like the Sub filter except that the pixel immediately above the currenTColor pixel, 
    /// rather than just to its left, is used as the predictor.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class UpFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Decode(byte[] scanline, byte[] previousScanline)
        {
            // Up(x) + Prior(x)
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte above = previousScanline[x];

                result[x] = (byte)((scanline[x] + above) % 256);
            }

            return result;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline, byte[] previousScanline)
        {
            // Up(x) = Raw(x) - Prior(x)
            byte[] encodedScanline = new byte[scanline.Length + 1];
            encodedScanline[0] = (byte)FilterType.Up;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte above = previousScanline[x];

                encodedScanline[x + 1] = (byte)((scanline[x] - above) % 256);
            }

            return encodedScanline;
        }
    }
}
