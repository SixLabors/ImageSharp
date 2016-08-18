// <copyright file="SubFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// The Sub filter transmits the difference between each byte and the value of the corresponding byte 
    /// of the prior pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class SubFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Decode(byte[] scanline, int bytesPerPixel)
        {
            // Sub(x) + Raw(x-bpp)
            byte[] result = new byte[scanline.Length];

            for (int x = 1; x < scanline.Length; x++)
            {
                byte priorRawByte = (x - bytesPerPixel < 1) ? (byte)0 : result[x - bytesPerPixel];

                result[x] = (byte)((scanline[x] + priorRawByte) % 256);
            }

            return result;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline, int bytesPerPixel)
        {
            // Sub(x) = Raw(x) - Raw(x-bpp)
            byte[] encodedScanline = new byte[scanline.Length + 1];
            encodedScanline[0] = (byte)FilterType.Sub;

            for (int x = 0; x < scanline.Length; x++)
            {
                byte priorRawByte = (x - bytesPerPixel < 0) ? (byte)0 : scanline[x - bytesPerPixel];

                encodedScanline[x + 1] = (byte)((scanline[x] - priorRawByte) % 256);
            }

            return encodedScanline;
        }
    }
}
