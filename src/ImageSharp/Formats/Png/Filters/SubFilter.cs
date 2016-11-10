// <copyright file="SubFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// The Sub filter transmits the difference between each byte and the value of the corresponding byte
    /// of the prior pixel.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static unsafe class SubFilter
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

            fixed (byte* scan = scanline)
            fixed (byte* res = result)
            {
                for (int x = 1; x < scanline.Length; x++)
                {
                    byte priorRawByte = (x - bytesPerPixel < 1) ? (byte)0 : res[x - bytesPerPixel];

                    res[x] = (byte)((scan[x] + priorRawByte) % 256);
                }
            }

            return result;
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public static byte[] Encode(byte[] scanline, byte[] result, int bytesPerPixel, int bytesPerScanline)
        {
            // Sub(x) = Raw(x) - Raw(x-bpp)
            fixed (byte* scan = scanline)
            fixed (byte* res = result)
            {
                res[0] = 1;

                for (int x = 0; x < bytesPerScanline; x++)
                {
                    byte priorRawByte = (x - bytesPerPixel < 0) ? (byte)0 : scan[x - bytesPerPixel];

                    res[x + 1] = (byte)((scan[x] - priorRawByte) % 256);
                }
            }

            return result;
        }
    }
}
