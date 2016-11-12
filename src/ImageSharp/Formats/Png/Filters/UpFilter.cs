// <copyright file="UpFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// The Up filter is just like the Sub filter except that the pixel immediately above the current pixel,
    /// rather than just to its left, is used as the predictor.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static unsafe class UpFilter
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

            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            fixed (byte* res = result)
            {
                for (int x = 1; x < scanline.Length; x++)
                {
                    byte above = prev[x];

                    res[x] = (byte)((scan[x] + above) % 256);
                }
            }

            return result;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline, byte[] previousScanline, int bytesPerScanline)
        {
            // Up(x) = Raw(x) - Prior(x)
            byte[] result = new byte[bytesPerScanline + 1];
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            fixed (byte* res = result)
            {
                res[0] = 2;

                for (int x = 0; x < bytesPerScanline; x++)
                {
                    byte above = prev[x];

                    res[x + 1] = (byte)((scan[x] - above) % 256);
                }
            }

            return result;
        }
    }
}
