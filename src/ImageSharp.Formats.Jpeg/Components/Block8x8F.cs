// <copyright file="Block8x8F.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
// ReSharper disable InconsistentNaming
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// DCT code Ported from https://github.com/norishigefukushima/dct_simd
    /// </summary>
    internal partial struct Block8x8F
    {
        // Most of the static methods of this struct are instance methods by actual semantics: they use Block8x8F* as their first parameter.
        // Example: GetScalarAt() and SetScalarAt() are really just other (optimized) versions of the indexer.
        // It's much cleaner, easier and safer to work with the code, if the methods with same semantics are next to each other.
#pragma warning disable SA1204 // StaticElementsMustAppearBeforeInstanceElements

        /// <summary>
        /// Vector count
        /// </summary>
        public const int VectorCount = 16;

        /// <summary>
        /// Scalar count
        /// </summary>
        public const int ScalarCount = VectorCount * 4;

#pragma warning disable SA1600 // ElementsMustBeDocumented
        public Vector4 V0L;
        public Vector4 V0R;

        public Vector4 V1L;
        public Vector4 V1R;

        public Vector4 V2L;
        public Vector4 V2R;

        public Vector4 V3L;
        public Vector4 V3R;

        public Vector4 V4L;
        public Vector4 V4R;

        public Vector4 V5L;
        public Vector4 V5R;

        public Vector4 V6L;
        public Vector4 V6R;

        public Vector4 V7L;
        public Vector4 V7R;
#pragma warning restore SA1600 // ElementsMustBeDocumented

        /// <summary>
        /// Get/Set scalar elements at a given index
        /// </summary>
        /// <param name="idx">The index</param>
        /// <returns>The float value at the specified index</returns>
        public unsafe float this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (Block8x8F* p = &this)
                {
                    float* fp = (float*)p;
                    return fp[idx];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                fixed (Block8x8F* p = &this)
                {
                    float* fp = (float*)p;
                    fp[idx] = value;
                }
            }
        }

        /// <summary>
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <returns>The scaleVec value at the specified index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float GetScalarAt(Block8x8F* blockPtr, int idx)
        {
            float* fp = (float*)blockPtr;
            return fp[idx];
        }

        /// <summary>
        /// Pointer-based "Indexer" (setter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <param name="value">Value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetScalarAt(Block8x8F* blockPtr, int idx, float value)
        {
            float* fp = (float*)blockPtr;
            fp[idx] = value;
        }

        /// <summary>
        /// Fill the block with defaults (zeroes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            // The cheapest way to do this in C#:
            this = default(Block8x8F);
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void LoadFrom(MutableSpan<float> source)
        {
            fixed (void* ptr = &this.V0L)
            {
                Marshal.Copy(source.Data, source.Offset, (IntPtr)ptr, ScalarCount);
            }
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="source">Source</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void LoadFrom(Block8x8F* blockPtr, MutableSpan<float> source)
        {
            Marshal.Copy(source.Data, source.Offset, (IntPtr)blockPtr, ScalarCount);
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        public unsafe void LoadFrom(MutableSpan<int> source)
        {
            fixed (Vector4* ptr = &this.V0L)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    fp[i] = source[i];
                }
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(MutableSpan<float> dest)
        {
            fixed (void* ptr = &this.V0L)
            {
                Marshal.Copy((IntPtr)ptr, dest.Data, dest.Offset, ScalarCount);
            }
        }

        /// <summary>
        /// Convert salars to byte-s and copy to dest
        /// </summary>
        /// <param name="blockPtr">Pointer to block</param>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, MutableSpan<byte> dest)
        {
            float* fPtr = (float*)blockPtr;
            for (int i = 0; i < ScalarCount; i++)
            {
                dest[i] = (byte)*fPtr;
                fPtr++;
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, MutableSpan<float> dest)
        {
            Marshal.Copy((IntPtr)blockPtr, dest.Data, dest.Offset, ScalarCount);
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(float[] dest)
        {
            fixed (void* ptr = &this.V0L)
            {
                Marshal.Copy((IntPtr)ptr, dest, 0, ScalarCount);
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        public unsafe void CopyTo(MutableSpan<int> dest)
        {
            fixed (Vector4* ptr = &this.V0L)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    dest[i] = (int)fp[i];
                }
            }
        }

        /// <summary>
        /// Multiply all elements of the block.
        /// </summary>
        /// <param name="scaleVec">Vector to multiply by</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAllInplace(Vector4 scaleVec)
        {
            this.V0L *= scaleVec;
            this.V0R *= scaleVec;
            this.V1L *= scaleVec;
            this.V1R *= scaleVec;
            this.V2L *= scaleVec;
            this.V2R *= scaleVec;
            this.V3L *= scaleVec;
            this.V3R *= scaleVec;
            this.V4L *= scaleVec;
            this.V4R *= scaleVec;
            this.V5L *= scaleVec;
            this.V5R *= scaleVec;
            this.V6L *= scaleVec;
            this.V6R *= scaleVec;
            this.V7L *= scaleVec;
            this.V7R *= scaleVec;
        }

        /// <summary>
        /// Adds a vector to all elements of the block.
        /// </summary>
        /// <param name="diff">The added vector</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToAllInplace(Vector4 diff)
        {
            this.V0L += diff;
            this.V0R += diff;
            this.V1L += diff;
            this.V1R += diff;
            this.V2L += diff;
            this.V2R += diff;
            this.V3L += diff;
            this.V3R += diff;
            this.V4L += diff;
            this.V4R += diff;
            this.V5L += diff;
            this.V5R += diff;
            this.V6L += diff;
            this.V6R += diff;
            this.V7L += diff;
            this.V7R += diff;
        }

        /// <summary>
        /// Un-zig
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="qtPtr">Qt pointer</param>
        /// <param name="unzigPtr">Unzig pointer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void UnZig(Block8x8F* blockPtr, Block8x8F* qtPtr, int* unzigPtr)
        {
            float* b = (float*)blockPtr;
            float* qtp = (float*)qtPtr;
            for (int zig = 0; zig < ScalarCount; zig++)
            {
                float* unzigPos = b + unzigPtr[zig];
                float val = *unzigPos;
                val *= qtp[zig];
                *unzigPos = val;
            }
        }

        /// <summary>
        /// Level shift by +128, clip to [0, 255], and write to buffer.
        /// </summary>
        /// <param name="buffer">Color buffer</param>
        /// <param name="stride">Stride offset</param>
        /// <param name="tempBlockPtr">Temp Block pointer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyColorsTo(MutableSpan<byte> buffer, int stride, Block8x8F* tempBlockPtr)
        {
            this.TransformByteConvetibleColorValuesInto(ref *tempBlockPtr);

            float* src = (float*)tempBlockPtr;
            for (int i = 0; i < 8; i++)
            {
                buffer[0] = (byte)src[0];
                buffer[1] = (byte)src[1];
                buffer[2] = (byte)src[2];
                buffer[3] = (byte)src[3];
                buffer[4] = (byte)src[4];
                buffer[5] = (byte)src[5];
                buffer[6] = (byte)src[6];
                buffer[7] = (byte)src[7];
                buffer.AddOffset(stride);
                src += 8;
            }
        }

        /// <summary>
        /// Unzig the elements of block into dest, while dividing them by elements of qt and "pre-rounding" the values.
        /// To finish the rounding it's enough to (int)-cast these values.
        /// </summary>
        /// <param name="block">Source block</param>
        /// <param name="dest">Destination block</param>
        /// <param name="qt">The quantization table</param>
        /// <param name="unzigPtr">Pointer to elements of <see cref="UnzigData"/></param>
        public static unsafe void UnzigDivRound(
            Block8x8F* block,
            Block8x8F* dest,
            Block8x8F* qt,
            int* unzigPtr)
        {
            float* s = (float*)block;
            float* d = (float*)dest;

            for (int zig = 0; zig < ScalarCount; zig++)
            {
                d[zig] = s[unzigPtr[zig]];
            }

            DivideRoundAll(ref *dest, ref *qt);
        }

        /// <summary>
        /// Scales the 16x16 region represented by the 4 source blocks to the 8x8  DST block.
        /// </summary>
        /// <param name="destination">The destination block.</param>
        /// <param name="source">The source block.</param>
        public static unsafe void Scale16X16To8X8(Block8x8F* destination, Block8x8F* source)
        {
            float* d = (float*)destination;
            for (int i = 0; i < 4; i++)
            {
                int dstOff = ((i & 2) << 4) | ((i & 1) << 2);

                float* iSource = (float*)(source + i);

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int j = (16 * y) + (2 * x);
                        float sum = iSource[j] + iSource[j + 1] + iSource[j + 8] + iSource[j + 9];
                        d[(8 * y) + x + dstOff] = (sum + 2) / 4;
                    }
                }
            }
        }

        /// <summary>
        /// Performs division and rounding of a rational number represented by a dividend and a divisior into an integer.
        /// </summary>
        /// <param name="dividend">The dividend</param>
        /// <param name="divisor">The divisor</param>
        /// <returns>The result integer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideRound(int dividend, int divisor)
        {
            if (dividend >= 0)
            {
                return (dividend + (divisor >> 1)) / divisor;
            }

            return -((-dividend + (divisor >> 1)) / divisor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DivideRoundAll(ref Block8x8F a, ref Block8x8F b)
        {
            a.V0L = DivideRound(a.V0L, b.V0L);
            a.V0R = DivideRound(a.V0R, b.V0R);
            a.V1L = DivideRound(a.V1L, b.V1L);
            a.V1R = DivideRound(a.V1R, b.V1R);
            a.V2L = DivideRound(a.V2L, b.V2L);
            a.V2R = DivideRound(a.V2R, b.V2R);
            a.V3L = DivideRound(a.V3L, b.V3L);
            a.V3R = DivideRound(a.V3R, b.V3R);
            a.V4L = DivideRound(a.V4L, b.V4L);
            a.V4R = DivideRound(a.V4R, b.V4R);
            a.V5L = DivideRound(a.V5L, b.V5L);
            a.V5R = DivideRound(a.V5R, b.V5R);
            a.V6L = DivideRound(a.V6L, b.V6L);
            a.V6R = DivideRound(a.V6R, b.V6R);
            a.V7L = DivideRound(a.V7L, b.V7L);
            a.V7R = DivideRound(a.V7R, b.V7R);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 DivideRound(Vector4 dividend, Vector4 divisor)
        {
            Vector4 sign = Vector4.Min(dividend, Vector4.One);
            sign = Vector4.Max(sign, new Vector4(-1));

            return (dividend / divisor) + (sign * new Vector4(0.5f));
        }
    }
}