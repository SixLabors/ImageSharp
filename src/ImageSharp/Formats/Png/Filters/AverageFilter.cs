// <copyright file="AverageFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Runtime.CompilerServices;

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
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(ref byte scanline, ref byte previousScanline, int bytesPerScanline, int bytesPerPixel)
        {
            // Average(x) + floor((Raw(x-bpp)+Prior(x))/2)
            for (int x = 1; x < bytesPerScanline; x++)
            {
                if (x - bytesPerPixel < 1)
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    scan = (byte)((scan + (above >> 1)) % 256);
                }
                else
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte left = ref Unsafe.Add(ref scanline, x - bytesPerPixel);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    scan = (byte)((scan + Average(left, above)) % 256);
                }
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(ref byte scanline, ref byte previousScanline, ref byte result, int bytesPerScanline, int bytesPerPixel)
        {
            // Average(x) = Raw(x) - floor((Raw(x-bpp)+Prior(x))/2)
            result = 3;

            for (int x = 0; x < bytesPerScanline; x++)
            {
                if (x - bytesPerPixel < 0)
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    ref byte res = ref Unsafe.Add(ref result, x + 1);
                    res = (byte)((scan - (above >> 1)) % 256);
                }
                else
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte left = ref Unsafe.Add(ref scanline, x - bytesPerPixel);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    ref byte res = ref Unsafe.Add(ref result, x + 1);
                    res = (byte)((scan - Average(left, above)) % 256);
                }
            }
        }

        /// <summary>
        /// Calculates the average value of two bytes
        /// </summary>
        /// <param name="left">The left byte</param>
        /// <param name="above">The above byte</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Average(byte left, byte above)
        {
            return (left + above) >> 1;
        }
    }
}