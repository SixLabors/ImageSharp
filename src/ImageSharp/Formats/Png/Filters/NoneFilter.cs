// <copyright file="NoneFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;

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
        /// <param name="result">The filtered scanline result.</param>
        public static void Encode(byte[] scanline, byte[] result)
        {
            // Insert a byte before the data.
            result[0] = 0;
            Buffer.BlockCopy(scanline, 0, result, 1, scanline.Length);
        }
    }
}
