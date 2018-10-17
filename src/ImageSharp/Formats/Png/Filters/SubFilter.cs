// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(Span<byte> scanline, int bytesPerPixel)
        {
            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);

            // Sub(x) + Raw(x-bpp)
            int x = 1;
            for (; x <= bytesPerPixel /* Note the <= because x starts at 1 */; ++x)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
            }

            for (; x < scanline.Length; ++x)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte prev = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                scan = (byte)(scan + prev);
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="sum">The sum of the total variance of the filtered row</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(Span<byte> scanline, Span<byte> result, int bytesPerPixel, out int sum)
        {
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
            sum = 0;

            // Sub(x) = Raw(x) - Raw(x-bpp)
            resultBaseRef = 1;

            int x = 0;
            for (; x < bytesPerPixel; /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = scan;
                sum += ImageMaths.FastAbs(unchecked((sbyte)res));
            }

            for (int xLeft = x - bytesPerPixel; x < scanline.Length; ++xLeft /* Note: ++x happens in the body to avoid one add operation */)
            {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte prev = Unsafe.Add(ref scanBaseRef, xLeft);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - prev);
                sum += ImageMaths.FastAbs(unchecked((sbyte)res));
            }

            sum -= 1;
        }
    }
}
