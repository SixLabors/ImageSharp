// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The Up filter is just like the Sub filter except that the pixel immediately above the current pixel,
    /// rather than just to its left, is used as the predictor.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class UpFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(Span<byte> scanline, Span<byte> previousScanline)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);

            // Up(x) + Prior(x)
            for (int x = 1; x < scanline.Length; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                scan = (byte)(scan + above);
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="sum">The sum of the total variance of the filtered row</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(Span<byte> scanline, Span<byte> previousScanline, Span<byte> result, out int sum)
        {
            DebugGuard.MustBeSameSized(scanline, previousScanline, nameof(scanline));
            DebugGuard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref MemoryMarshal.GetReference(scanline);
            ref byte prevBaseRef = ref MemoryMarshal.GetReference(previousScanline);
            ref byte resultBaseRef = ref MemoryMarshal.GetReference(result);
            sum = 0;

            // Up(x) = Raw(x) - Prior(x)
            resultBaseRef = 2;

            for (int x = 0; x < scanline.Length; /* Note: ++x happens in the body to avoid one add operation */) {
                byte scan = Unsafe.Add(ref scanBaseRef, x);
                byte above = Unsafe.Add(ref prevBaseRef, x);
                ++x;
                ref byte res = ref Unsafe.Add(ref resultBaseRef, x);
                res = (byte)(scan - above);
                sum += ImageMaths.FastAbs(unchecked((sbyte)res));
            }

            sum -= 2;
        }
    }
}
