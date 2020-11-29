// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
    /// then chooses as predictor the neighboring pixel closest to the computed value.
    /// This technique is due to Alan W. Paeth.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class PaethFilter
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

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

            // Paeth(x) + PaethPredictor(Raw(x-bpp), Prior(x), Prior(x-bpp))
            int offset = bytesPerPixel + 1; // Add one because x starts at one.
            int x = 1;
            for (; x < offset; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + above);
            }

            for (; x < scanline.Length; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                byte upperLeft = Unsafe.Add(ref prevBaseRef, x - bytesPerPixel);
                scan = (byte)(scan + PaethPredictor(left, above, upperLeft));
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="sum">The sum of the total variance of the filtered row</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(Span<byte> scanline, Span<byte> previousScanline, Span<byte> result, int bytesPerPixel, out int sum)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);
            ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
            sum = 0;

            // Paeth(x) = Raw(x) - PaethPredictor(Raw(x-bpp), Prior(x), Prior(x - bpp))
            resultBaseRef = 4;

            int x = 0;
            for (; x < bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - PaethPredictor(0, above, 0));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

            for (int xLeft = x - bytesPerPixel; x < scanline.Length; ++xLeft /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte left = Unsafe.Add(ref scanBaseRef, xLeft);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                byte upperLeft = Unsafe.Add(ref prevBaseRef, xLeft);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - PaethPredictor(left, above, upperLeft));
                sum += Numerics.Abs(unchecked((sbyte)res));
            }

            sum -= 4;
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
        private static byte PaethPredictor(byte left, byte above, byte upperLeft)
        {
            int p = left + above - upperLeft;
            int pa = Numerics.Abs(p - left);
            int pb = Numerics.Abs(p - above);
            int pc = Numerics.Abs(p - upperLeft);

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
