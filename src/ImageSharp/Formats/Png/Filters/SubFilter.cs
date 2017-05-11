// <copyright file="SubFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decode(BufferSpan<byte> scanline, int bytesPerPixel)
        {
            ref byte scanBaseRef = ref scanline.DangerousGetPinnableReference();

            // Sub(x) + Raw(x-bpp)
            for (int x = 1; x < scanline.Length; x++)
            {
                if (x - bytesPerPixel < 1)
                {
                    ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                    scan = (byte)(scan % 256);
                }
                else
                {
                    ref byte scan = ref Unsafe.Add(ref scanBaseRef, x);
                    byte prev = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                    scan = (byte)((scan + prev) % 256);
                }
            }
        }

        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(BufferSpan<byte> scanline, BufferSpan<byte> result, int bytesPerPixel)
        {
            Guard.MustBeSizedAtLeast(result, scanline, nameof(result));

            ref byte scanBaseRef = ref scanline.DangerousGetPinnableReference();
            ref byte resultBaseRef = ref result.DangerousGetPinnableReference();

            // Sub(x) = Raw(x) - Raw(x-bpp)
            resultBaseRef = 1;

            for (int x = 0; x < scanline.Length; x++)
            {
                if (x - bytesPerPixel < 0)
                {
                    byte scan = Unsafe.Add(ref scanBaseRef, x);
                    ref byte res = ref Unsafe.Add(ref resultBaseRef, x + 1);
                    res = (byte)(scan % 256);
                }
                else
                {
                    byte scan = Unsafe.Add(ref scanBaseRef, x);
                    byte prev = Unsafe.Add(ref scanBaseRef, x - bytesPerPixel);
                    ref byte res = ref Unsafe.Add(ref resultBaseRef, x + 1);
                    res = (byte)((scan - prev) % 256);
                }
            }
        }
    }
}
