// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png.Filters
{
    /// <summary>
    /// The None filter, the scanline is transmitted unmodified; it is only necessary to
    /// insert a filter type byte before the data.
    /// <see href="https://www.w3.org/TR/PNG-Filters.html"/>
    /// </summary>
    internal static class NoneFilter
    {
        /// <summary>
        /// Encodes the scanline
        /// </summary>
        /// <param name="scanline">The scanline to encode</param>
        /// <param name="result">The filtered scanline result.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(ReadOnlySpan<byte> scanline, Span<byte> result)
        {
            // Insert a byte before the data.
            result[0] = 0;
            result = result.Slice(1);
            scanline.Slice(0, Math.Min(scanline.Length, result.Length)).CopyTo(result);
        }
    }
}