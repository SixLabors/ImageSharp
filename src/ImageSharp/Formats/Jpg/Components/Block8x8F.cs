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
        public const int VectorCount = 16;
        public const int ScalarCount = VectorCount * 4;

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

#pragma warning disable SA1310 // FieldNamesMustNotContainUnderscore
        private static readonly Vector4 C_1_175876 = new Vector4(1.175876f);
        private static readonly Vector4 C_1_961571 = new Vector4(-1.961571f);
        private static readonly Vector4 C_0_390181 = new Vector4(-0.390181f);
        private static readonly Vector4 C_0_899976 = new Vector4(-0.899976f);
        private static readonly Vector4 C_2_562915 = new Vector4(-2.562915f);
        private static readonly Vector4 C_0_298631 = new Vector4(0.298631f);
        private static readonly Vector4 C_2_053120 = new Vector4(2.053120f);
        private static readonly Vector4 C_3_072711 = new Vector4(3.072711f);
        private static readonly Vector4 C_1_501321 = new Vector4(1.501321f);
        private static readonly Vector4 C_0_541196 = new Vector4(0.541196f);
        private static readonly Vector4 C_1_847759 = new Vector4(-1.847759f);
        private static readonly Vector4 C_0_765367 = new Vector4(0.765367f);

        private static readonly Vector4 C_0_125 = new Vector4(0.1250f);
#pragma warning restore SA1310 // FieldNamesMustNotContainUnderscore

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void LoadFrom(Block8x8F* blockPtr, MutableSpan<float> source)
        {
            Marshal.Copy(source.Data, source.Offset, (IntPtr)blockPtr, ScalarCount);
        }

        /// <summary>
        /// Copy raw 32bit floating point data to dest
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, MutableSpan<float> dest)
        {
            Marshal.Copy((IntPtr)blockPtr, dest.Data, dest.Offset, ScalarCount);
        }

        /// <summary>
        /// Load raw 32bit floating point data from source
        /// </summary>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(float[] dest)
        {
            fixed (void* ptr = &this.V0L)
            {
                Marshal.Copy((IntPtr)ptr, dest, 0, ScalarCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAllInplace(Vector4 s)
        {
            this.V0L *= s;
            this.V0R *= s;
            this.V1L *= s;
            this.V1R *= s;
            this.V2L *= s;
            this.V2R *= s;
            this.V3L *= s;
            this.V3R *= s;
            this.V4L *= s;
            this.V4R *= s;
            this.V5L *= s;
            this.V5R *= s;
            this.V6L *= s;
            this.V6R *= s;
            this.V7L *= s;
            this.V7R *= s;
        }

        /// <summary>
        /// Apply floating point IDCT transformation into dest, using a temporary block 'temp' provided by the caller (optimization)
        /// </summary>
        /// <param name="dest">Destination</param>
        /// <param name="temp">Temporary block provided by the caller</param>
        public void TransformIDCTInto(ref Block8x8F dest, ref Block8x8F temp)
        {
            this.TransposeInto(ref temp);
            temp.IDCT8x4_LeftPart(ref dest);
            temp.IDCT8x4_RightPart(ref dest);

            dest.TransposeInto(ref temp);

            temp.IDCT8x4_LeftPart(ref dest);
            temp.IDCT8x4_RightPart(ref dest);

            dest.MultiplyAllInplace(C_0_125);
        }

        /// <summary>
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float GetScalarAt(Block8x8F* blockPtr, int idx)
        {
            float* fp = (float*)blockPtr;
            return fp[idx];
        }

        /// <summary>
        /// Pointer-based "Indexer" (setter part)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void SetScalarAt(Block8x8F* blockPtr, int idx, float value)
        {
            float* fp = (float*)blockPtr;
            fp[idx] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UnZig(Block8x8F* block, Block8x8F* qt, int* unzigPtr)
        {
            float* b = (float*)block;
            float* qtp = (float*)qt;
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
        /// Do IDCT internal operations on the left part of the block. Original source:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
        /// </summary>
        /// <param name="d">Destination block</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IDCT8x4_LeftPart(ref Block8x8F d)
        {
            Vector4 my1 = this.V1L;
            Vector4 my7 = this.V7L;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = this.V3L;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = this.V5L;
            Vector4 mz1 = my3 + my5;
            Vector4 mz3 = my1 + my5;

            Vector4 mz4 = (mz0 + mz1) * C_1_175876;

            mz2 = (mz2 * C_1_961571) + mz4;
            mz3 = (mz3 * C_0_390181) + mz4;
            mz0 = mz0 * C_0_899976;
            mz1 = mz1 * C_2_562915;

            Vector4 mb3 = (my7 * C_0_298631) + mz0 + mz2;
            Vector4 mb2 = (my5 * C_2_053120) + mz1 + mz3;
            Vector4 mb1 = (my3 * C_3_072711) + mz1 + mz2;
            Vector4 mb0 = (my1 * C_1_501321) + mz0 + mz3;

            Vector4 my2 = this.V2L;
            Vector4 my6 = this.V6L;
            mz4 = (my2 + my6) * C_0_541196;
            Vector4 my0 = this.V0L;
            Vector4 my4 = this.V4L;
            mz0 = my0 + my4;
            mz1 = my0 - my4;

            mz2 = mz4 + (my6 * C_1_847759);
            mz3 = mz4 + (my2 * C_0_765367);

            my0 = mz0 + mz3;
            my3 = mz0 - mz3;
            my1 = mz1 + mz2;
            my2 = mz1 - mz2;

            d.V0L = my0 + mb0;
            d.V7L = my0 - mb0;
            d.V1L = my1 + mb1;
            d.V6L = my1 - mb1;
            d.V2L = my2 + mb2;
            d.V5L = my2 - mb2;
            d.V3L = my3 + mb3;
            d.V4L = my3 - mb3;
        }

        /// <summary>
        /// Do IDCT internal operations on the right part of the block.
        /// Original source:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IDCT8x4_RightPart(ref Block8x8F d)
        {
            Vector4 my1 = this.V1R;
            Vector4 my7 = this.V7R;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = this.V3R;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = this.V5R;
            Vector4 mz1 = my3 + my5;
            Vector4 mz3 = my1 + my5;

            Vector4 mz4 = (mz0 + mz1) * C_1_175876;

            mz2 = (mz2 * C_1_961571) + mz4;
            mz3 = (mz3 * C_0_390181) + mz4;
            mz0 = mz0 * C_0_899976;
            mz1 = mz1 * C_2_562915;

            Vector4 mb3 = (my7 * C_0_298631) + mz0 + mz2;
            Vector4 mb2 = (my5 * C_2_053120) + mz1 + mz3;
            Vector4 mb1 = (my3 * C_3_072711) + mz1 + mz2;
            Vector4 mb0 = (my1 * C_1_501321) + mz0 + mz3;

            Vector4 my2 = this.V2R;
            Vector4 my6 = this.V6R;
            mz4 = (my2 + my6) * C_0_541196;
            Vector4 my0 = this.V0R;
            Vector4 my4 = this.V4R;
            mz0 = my0 + my4;
            mz1 = my0 - my4;

            mz2 = mz4 + (my6 * C_1_847759);
            mz3 = mz4 + (my2 * C_0_765367);

            my0 = mz0 + mz3;
            my3 = mz0 - mz3;
            my1 = mz1 + mz2;
            my2 = mz1 - mz2;

            d.V0R = my0 + mb0;
            d.V7R = my0 - mb0;
            d.V1R = my1 + mb1;
            d.V6R = my1 - mb1;
            d.V2R = my2 + mb2;
            d.V5R = my2 - mb2;
            d.V3R = my3 + mb3;
            d.V4R = my3 - mb3;
        }

        /// <summary>
        /// Fill the block with defaults (zeroes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear()
        {
            // The cheapest way to do this in C#:
            this = new Block8x8F();
        }

        /// <summary>
        /// TODO: Should be removed when BlockF goes away
        /// </summary>
        /// <param name="legacyBlock"></param>
        internal void LoadFrom(ref BlockF legacyBlock)
        {
            this.LoadFrom(legacyBlock.Data);
        }

        /// <summary>
        /// TODO: Should be removed when BlockF goes away
        /// </summary>
        /// <param name="legacyBlock"></param>
        internal void CopyTo(ref BlockF legacyBlock)
        {
            this.CopyTo(legacyBlock.Data);
        }

        /// <summary>
        /// Level shift by +128, clip to [0, 255], and write to buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CopyColorsTo(MutableSpan<byte> buffer, int stride, Block8x8F* temp)
        {
            this.TransformByteConvetibleColorValuesInto(ref *temp);

            float* src = (float*)temp;
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