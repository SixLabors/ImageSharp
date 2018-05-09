// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="byte"/> struct buffers.
    /// </summary>
    internal static class ByteExtensions
    {
        /// <summary>
        /// Returns a reference to the given position of the span unsafe casted to <see cref="ImageSharp.PixelFormats.Rgb24"/>.
        /// </summary>
        /// <param name="bytes">The byte span.</param>
        /// <returns>The <see cref="ImageSharp.PixelFormats.Rgb24"/> reference at the given offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Rgb24 AsRgb24(this Span<byte> bytes)
        {
            return ref Unsafe.As<byte, Rgb24>(ref bytes[0]);
        }     
    }
}