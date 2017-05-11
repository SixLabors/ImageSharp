// <copyright file="NoneFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Runtime.CompilerServices;

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
        public static void Encode(BufferSpan<byte> scanline, BufferSpan<byte> result)
        {
            // Insert a byte before the data.
            result[0] = 0;
            result = result.Slice(1);
            BufferSpan.Copy(scanline, result);
        }
    }
}
