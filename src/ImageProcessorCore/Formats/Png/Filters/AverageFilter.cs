// <copyright file="AverageFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;

    /// <summary>
    /// The Average filter uses the average of the two neighboring pixels (left and above) to predict 
    /// the value of a pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class AverageFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <returns>
        /// The <see cref="T:byte[]"/>
        /// </returns>
        public static byte[] Decode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            // Average(x) + floor((Raw(x-bpp)+Prior(x))/2)
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];
                byte above = previousScanline[x];

                result[x] = (byte)((scanline[x] + Average(left, above)) % 256);
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
            // Average(x) = Raw(x) - floor((Raw(x-bpp)+Prior(x))/2)
            byte[] encodedScanline = new byte[scanline.Length + 1];

            encodedScanline[0] = (byte)FilterType.Average;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte left = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];
                byte above = previousScanline[x];

                encodedScanline[x + 1] = (byte)((scanline[x] - Average(left, above)) % 256);
            }

            return encodedScanline;
        }

        /// <summary>
        /// Calculates the average value of two bytes
        /// </summary>
        /// <param name="left">The left byte</param>
        /// <param name="above">The above byte</param>
        /// <returns>The <see cref="int"/></returns>
        private static int Average(byte left, byte above)
        {
            return Convert.ToInt32(Math.Floor((left + above) / 2.0D));
        }
    }
}
