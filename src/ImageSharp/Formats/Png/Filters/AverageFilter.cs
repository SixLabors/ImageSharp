// <copyright file="AverageFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;

    /// <summary>
    /// The Average filter uses the average of the two neighboring pixels (left and above) to predict
    /// the value of a pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static unsafe class AverageFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        public static void Decode(byte[] scanline, byte[] previousScanline, int bytesPerPixel)
        {
            // Average(x) + floor((Raw(x-bpp)+Prior(x))/2)
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            {
                for (int x = 1; x < scanline.Length; x++)
                {
                    byte left = (x - bytesPerPixel < 1) ? (byte)0 : scan[x - bytesPerPixel];
                    byte above = prev[x];

                    scan[x] = (byte)((scan[x] + Average(left, above)) % 256);
                }
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        public static void Encode(byte[] scanline, byte[] previousScanline, byte[] result, int bytesPerPixel)
        {
            // Average(x) = Raw(x) - floor((Raw(x-bpp)+Prior(x))/2)
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            fixed (byte* res = result)
            {
                res[0] = 3;

                for (int x = 0; x < scanline.Length; x++)
                {
                    byte left = (x - bytesPerPixel < 0) ? (byte)0 : scan[x - bytesPerPixel];
                    byte above = prev[x];

                    res[x + 1] = (byte)((scan[x] - Average(left, above)) % 256);
                }
            }
        }

        /// <summary>
        /// Calculates the average value of two bytes
        /// </summary>
        /// <param name="left">The left byte</param>
        /// <param name="above">The above byte</param>
        /// <returns>The <see cref="int"/></returns>
        private static int Average(byte left, byte above)
        {
            return (left + above) >> 1;
        }
    }
}