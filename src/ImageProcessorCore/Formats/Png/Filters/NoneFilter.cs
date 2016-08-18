// <copyright file="NoneFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// The None filter, the scanline is transmitted unmodified; it is only necessary to 
    /// insert a filter type byte before the data.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class NoneFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Decode(byte[] scanline)
        {
            // No change required.
            return scanline;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline)
        {
            // Insert a byte before the data.
            byte[] encodedScanline = new byte[scanline.Length + 1];
            encodedScanline[0] = (byte)FilterType.None;
            scanline.CopyTo(encodedScanline, 1);

            return encodedScanline;
        }
    }
}
