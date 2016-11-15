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
        public static void Decode(byte[] scanline, byte[] previousScanline)
        {
            // Up(x) + Prior(x)
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            {
                for (int x = 1; x < scanline.Length; x++)
                {
                    byte above = prev[x];

                    scan[x] = (byte)((scan[x] + above) % 256);
                }
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        public static void Encode(byte[] scanline, byte[] previousScanline, byte[] result)
        {
            // Up(x) = Raw(x) - Prior(x)
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            fixed (byte* res = result)
            {
                res[0] = 2;

                for (int x = 0; x < scanline.Length; x++)
                {
                    byte above = prev[x];

                    res[x + 1] = (byte)((scan[x] - above) % 256);
                }
            }
        }
    }
}
