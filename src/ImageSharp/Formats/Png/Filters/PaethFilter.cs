// <copyright file="PaethFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
    /// then chooses as predictor the neighboring pixel closest to the computed value.
    /// This technique is due to Alan W. Paeth.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static unsafe class PaethFilter
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
            // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
            for (int x = 1; x < bytesPerScanline; x++)
            {
                if (x - bytesPerPixel < 1)
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    scan = (byte)((scan + PaethPredicator(0, above, 0)) % 256);
                }
                else
                {
                    ref byte scan = ref Unsafe.Add(ref scanline, x);
                    ref byte left = ref Unsafe.Add(ref scanline, x - bytesPerPixel);
                    ref byte above = ref Unsafe.Add(ref previousScanline, x);
                    ref byte upperLeft = ref Unsafe.Add(ref previousScanline, x - bytesPerPixel);
                    scan = (byte)((scan + PaethPredicator(left, above, upperLeft)) % 256);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(byte[] scanline, byte[] previousScanline, byte[] result, int bytesPerPixel)
        {
            // Paeth(x) = Raw(x) - PaethPredictor(Raw(x-bpp), Prior(x), Prior(x - bpp))
            fixed (byte* scan = scanline)
            fixed (byte* prev = previousScanline)
            fixed (byte* res = result)
            {
                res[0] = 4;

                for (int x = 0; x < scanline.Length; x++)
                {
                    byte left = (x - bytesPerPixel < 0) ? (byte)0 : scan[x - bytesPerPixel];
                    byte above = prev[x];
                    byte upperLeft = (x - bytesPerPixel < 0) ? (byte)0 : prev[x - bytesPerPixel];

                    res[x + 1] = (byte)((scan[x] - PaethPredicator(left, above, upperLeft)) % 256);
                }
            }
        }

        /// <summary>
        /// Computes a simple linear function of the three neighboring pixels (left, above, upper left), then chooses
        /// as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        /// <param name="left">The left neighbor pixel.</param>
        /// <param name="above">The above neighbor pixel.</param>
        /// <param name="upperLeft">The upper left neighbor pixel.</param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte PaethPredicator(byte left, byte above, byte upperLeft)
        {
            int p = left + above - upperLeft;
            int pa = ImageMaths.FastAbs(p - left);
            int pb = ImageMaths.FastAbs(p - above);
            int pc = ImageMaths.FastAbs(p - upperLeft);

            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            if (pb <= pc)
            {
                return above;
            }

            return upperLeft;
        }
    }
}