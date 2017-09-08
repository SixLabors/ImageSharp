// <copyright file="PaethFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
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
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(Span<byte> scanline, Span<byte> previousScanline, int bytesPerPixel)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));

            ref byte scanBaseRef = ref scanline.DangerousGetPinnableReference();
            ref byte prevBaseRef = ref previousScanline.DangerousGetPinnableReference();

            // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
            int offset = bytesPerPixel + 1;
            for (int x = 1; x < offset; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + above);
            }

            for (int x = offset; x < scanline.Length; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                byte upperLeft = Unsafe.Add(ref prevBaseRef, x - bytesPerPixel);
                scan = (byte)(scan + PaethPredicator(left, above, upperLeft));
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
        public static void Encode(Span<byte> scanline, Span<byte> previousScanline, Span<byte> result, int bytesPerPixel)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref scanline.DangerousGetPinnableReference();
            ref byte prevBaseRef = ref previousScanline.DangerousGetPinnableReference();
            ref byte resultBaseRef = ref result.DangerousGetPinnableReference();

            // Paeth(x) = Raw(x) - PaethPredictor(Raw(x-bpp), Prior(x), Prior(x - bpp))
            resultBaseRef = 4;

            for (int x = 0; x < scanline.Length; x++)
            {
                if (x - bytesPerPixel < 0)
                {
                    byte scan = Unsafe.Add(ref scanBaseRef, x);
                    byte above = Unsafe.Add(ref prevBaseRef, x);
                    ref byte res = ref Unsafe.Add(ref resultBaseRef, x + 1);
                    res = (byte)((scan - PaethPredicator(0, above, 0)) % 256);
                }
                else
                {
                    byte scan = Unsafe.Add(ref scanBaseRef, x);
                    byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                    byte above = Unsafe.Add(ref prevBaseRef, x);
                    byte upperLeft = Unsafe.Add(ref prevBaseRef, x - bytesPerPixel);
                    ref byte res = ref Unsafe.Add(ref resultBaseRef, x + 1);
                    res = (byte)((scan - PaethPredicator(left, above, upperLeft)) % 256);
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