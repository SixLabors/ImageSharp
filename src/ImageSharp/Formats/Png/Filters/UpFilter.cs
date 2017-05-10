// <copyright file="UpFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The Up filter is just like the Sub filter except that the pixel immediately above the current pixel,
    /// rather than just to its left, is used as the predictor.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static unsafe class UpFilter
    {
        /// <summary>
        /// Decodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to decode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(BufferSpan<byte> scanline, BufferSpan<byte> previousScanline, int bytesPerScanline)
        {
            ref byte scanPointer = ref scanline.DangerousGetPinnableReference();
            ref byte prevPointer = ref previousScanline.DangerousGetPinnableReference();

            // Up(x) + Prior(x)
            for (int x = 1; x < bytesPerScanline; x++)
            {
                ref byte scan = ref Unsafe.Add(ref scanPointer, x);
                byte above = Unsafe.Add(ref prevPointer, x);
                scan = (byte)((scan + above) % 256);
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(BufferSpan<byte> scanline, BufferSpan<byte> previousScanline, BufferSpan<byte> result, int bytesPerScanline)
        {
            ref byte scanPointer = ref scanline.DangerousGetPinnableReference();
            ref byte prevPointer = ref previousScanline.DangerousGetPinnableReference();
            ref byte resultPointer = ref result.DangerousGetPinnableReference();

            // Up(x) = Raw(x) - Prior(x)
            resultPointer = 2;

            for (int x = 0; x < bytesPerScanline; x++)
            {
                byte scan = Unsafe.Add(ref scanPointer, x);
                byte above = Unsafe.Add(ref prevPointer, x);
                ref byte res = ref Unsafe.Add(ref resultPointer, x + 1);
                res = (byte)((scan - above) % 256);
            }
        }
    }
}
