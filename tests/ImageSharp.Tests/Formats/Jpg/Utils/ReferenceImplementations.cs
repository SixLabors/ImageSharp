// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    /// <summary>
    /// This class contains simplified (inefficient) reference implementations to produce verification data for unit tests
    /// Floating point DCT code Ported from https://github.com/norishigefukushima/dct_simd
    /// </summary>
    internal static partial class ReferenceImplementations
    {
        public static unsafe void DequantizeBlock(Block8x8F* blockPtr, Block8x8F* qtPtr, byte* unzigPtr)
        {
            float* b = (float*)blockPtr;
            float* qtp = (float*)qtPtr;
            for (int qtIndex = 0; qtIndex < Block8x8F.Size; qtIndex++)
            {
                byte i = unzigPtr[qtIndex];
                float* unzigPos = b + i;

                float val = *unzigPos;
                val *= qtp[qtIndex];
                *unzigPos = val;
            }
        }

        /// <summary>
        /// Transpose 8x8 block stored linearly in a <see cref="Span{T}"/> (inplace)
        /// </summary>
        internal static void Transpose8x8(Span<float> data)
        {
            for (int i = 1; i < 8; i++)
            {
                int i8 = i * 8;
                for (int j = 0; j < i; j++)
                {
                    float tmp = data[i8 + j];
                    data[i8 + j] = data[(j * 8) + i];
                    data[(j * 8) + i] = tmp;
                }
            }
        }

        /// <summary>
        /// Transpose 8x8 block stored linearly in a  <see cref="Span{T}"/>
        /// </summary>
        internal static void Transpose8x8(Span<float> src, Span<float> dest)
        {
            for (int i = 0; i < 8; i++)
            {
                int i8 = i * 8;
                for (int j = 0; j < 8; j++)
                {
                    dest[(j * 8) + i] = src[i8 + j];
                }
            }
        }

        /// <summary>
        /// Copies color values from block to the destination image buffer.
        /// </summary>
        internal static unsafe void CopyColorsTo(ref Block8x8F block, Span<byte> buffer, int stride)
        {
            fixed (Block8x8F* p = &block)
            {
                float* b = (float*)p;

                for (int y = 0; y < 8; y++)
                {
                    int y8 = y * 8;
                    int yStride = y * stride;

                    for (int x = 0; x < 8; x++)
                    {
                        float c = b[y8 + x];

                        if (c < -128)
                        {
                            c = 0;
                        }
                        else if (c > 127)
                        {
                            c = 255;
                        }
                        else
                        {
                            c += 128;
                        }

                        buffer[yStride + x] = (byte)c;
                    }
                }
            }
        }

        /// <summary>
        /// Reference implementation to test <see cref="Block8x8F.Quantize"/>.
        /// Rounding is done used an integer-based algorithm defined in <see cref="RationalRound(int,int)"/>.
        /// </summary>
        /// <param name="src">The input block</param>
        /// <param name="dest">The destination block of integers</param>
        /// <param name="qt">The quantization table</param>
        /// <param name="unzigPtr">Pointer to <see cref="ZigZag.Data"/> </param>
        public static unsafe void QuantizeRational(Block8x8F* src, int* dest, Block8x8F* qt, byte* unzigPtr)
        {
            float* s = (float*)src;
            float* q = (float*)qt;

            for (int zig = 0; zig < Block8x8F.Size; zig++)
            {
                int a = (int)s[unzigPtr[zig]];
                int b = (int)q[zig];

                int val = RationalRound(a, b);
                dest[zig] = val;
            }
        }

        /// <summary>
        /// Rounds a rational number defined as dividend/divisor into an integer.
        /// </summary>
        /// <param name="dividend">The dividend.</param>
        /// <param name="divisor">The divisor.</param>
        /// <returns>The rounded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RationalRound(int dividend, int divisor)
        {
            if (dividend >= 0)
            {
                return (dividend + (divisor >> 1)) / divisor;
            }

            return -((-dividend + (divisor >> 1)) / divisor);
        }
    }
}
