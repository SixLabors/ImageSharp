// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The helper methods for <see cref="PngEncoderCore"/> class.
    /// </summary>
    internal static class PngEncoderHelpers
    {
        /// <summary>
        /// Packs the given 8 bit array into and array of <paramref name="bits"/> depths.
        /// </summary>
        /// <param name="source">The source span in 8 bits.</param>
        /// <param name="result">The resultant span in <paramref name="bits"/>.</param>
        /// <param name="bits">The bit depth.</param>
        /// <param name="scale">The scaling factor.</param>
        public static void ScaleDownFrom8BitArray(ReadOnlySpan<byte> source, Span<byte> result, int bits, float scale = 1)
        {
            ref byte sourceRef = ref MemoryMarshal.GetReference(source);
            ref byte resultRef = ref MemoryMarshal.GetReference(result);

            int shift = 8 - bits;
            byte mask = (byte)(0xFF >> shift);
            byte shift0 = (byte)shift;
            int v = 0;
            int resultOffset = 0;

            for (int i = 0; i < source.Length; i++)
            {
                int value = ((int)MathF.Round(Unsafe.Add(ref sourceRef, i) / scale)) & mask;
                v |= value << shift;

                if (shift == 0)
                {
                    shift = shift0;
                    Unsafe.Add(ref resultRef, resultOffset) = (byte)v;
                    resultOffset++;
                    v = 0;
                }
                else
                {
                    shift -= bits;
                }
            }

            if (shift != shift0)
            {
                Unsafe.Add(ref resultRef, resultOffset) = (byte)v;
            }
        }
    }
}
