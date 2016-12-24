// <copyright file="Block8x8F.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
// ReSharper disable InconsistentNaming
namespace ImageSharp.Formats
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
        /// Index into the block
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
        /// Multiply in place
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
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <returns>The scaleVec value at the specified index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float GetScalarAt(Block8x8F* blockPtr, int idx)
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
        internal static unsafe void SetScalarAt(Block8x8F* blockPtr, int idx, float value)
        {
            float* fp = (float*)blockPtr;
            fp[idx] = value;
        }

        /// <summary>
        /// Un-zig
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="qtPtr">Qt pointer</param>
        /// <param name="unzigPtr">Unzig pointer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UnZig(Block8x8F* blockPtr, Block8x8F* qtPtr, int* unzigPtr)
        {
            float* b = (float*)blockPtr;
            float* qtp = (float*)qtPtr;
            for (int zig = 0; zig < BlockF.BlockSize; zig++)
            {
                float* unzigPos = b + unzigPtr[zig];
                float val = *unzigPos;
                val *= qtp[zig];
                *unzigPos = val;
            }
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        /// <param name="dest">Destination</param>
        internal unsafe void CopyTo(MutableSpan<int> dest)
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
        /// Load raw 32bit floating point data from source
        /// </summary>
        /// <param name="source">Source</param>
        internal unsafe void LoadFrom(MutableSpan<int> source)
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
        /// Fill the block with defaults (zeroes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear()
        {
            // The cheapest way to do this in C#:
            this = default(Block8x8F);
        }

        /// <summary>
        /// TODO: Should be removed when BlockF goes away
        /// </summary>
        /// <param name="legacyBlock">Legacy block</param>
        internal void LoadFrom(ref BlockF legacyBlock)
        {
            this.LoadFrom(legacyBlock.Data);
        }

        /// <summary>
        /// TODO: Should be removed when BlockF goes away
        /// </summary>
        /// <param name="legacyBlock">Legacy block</param>
        internal void CopyTo(ref BlockF legacyBlock)
        {
            this.CopyTo(legacyBlock.Data);
        }

        
        /// <summary>
        /// Level shift by +128, clip to [0, 255], and write to buffer.
        /// </summary>
        /// <param name="buffer">Color buffer</param>
        /// <param name="stride">Stride offset</param>
        /// <param name="tempBlockPtr">Temp Block pointer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CopyColorsTo(MutableSpan<byte> buffer, int stride, Block8x8F* tempBlockPtr)
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
    }
}