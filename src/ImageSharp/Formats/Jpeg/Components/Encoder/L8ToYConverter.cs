// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// Provides 8-bit lookup tables for converting from L8 to Y colorspace.
    /// </summary>
    internal unsafe struct L8ToYConverter
    {
        /// <summary>
        /// Initializes
        /// </summary>
        /// <returns>The initialized <see cref="L8ToYConverter"/></returns>
        public static L8ToYConverter Create()
        {
            L8ToYConverter converter = default;
            return converter;
        }

        /// <summary>
        /// Optimized method to allocates the correct y, cb, and cr values to the DCT blocks from the given r, g, b values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertPixelInto(
            int l,
            ref Block8x8F yResult,
            int i) => yResult[i] = l;

        public void Convert(Span<L8> l8Span, ref Block8x8F yBlock)
        {
            ref L8 l8Start = ref l8Span[0];

            for (int i = 0; i < 64; i++)
            {
                ref L8 c = ref Unsafe.Add(ref l8Start, i);

                this.ConvertPixelInto(
                    c.PackedValue,
                    ref yBlock,
                    i);
            }
        }
    }
}
