// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Contains inaccurate, but fast forward and inverse DCT implementations.
    /// </summary>
    internal static partial class FastFloatingPointDCT
    {
        /// <summary>
        /// Apply floating point IDCT transformation into dest, using a temporary block 'temp' provided by the caller (optimization).
        /// Ported from https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L239
        /// </summary>
        /// <param name="src">Source</param>
        /// <param name="dest">Destination</param>
        /// <param name="temp">Temporary block provided by the caller</param>
        public static void TransformIDCT(ref Block8x8F src, ref Block8x8F dest, ref Block8x8F temp)
        {
            src.TransposeInto(ref temp);

            IDCT8x8(ref temp, ref dest);
            dest.TransposeInto(ref temp);
            IDCT8x8(ref temp, ref dest);

            // TODO: What if we leave the blocks in a scaled-by-x8 state until final color packing?
            dest.MultiplyInPlace(C_0_125);
        }

        /// <summary>
        /// Performs 8x8 matrix Inverse Discrete Cosine Transform
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void IDCT8x8(ref Block8x8F s, ref Block8x8F d)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                IDCT8x8_Avx(ref s, ref d);
            }
            else
#endif
            {
                IDCT8x4_LeftPart(ref s, ref d);
                IDCT8x4_RightPart(ref s, ref d);
            }
        }

        /// <summary>
        /// Do IDCT internal operations on the left part of the block. Original src:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
        /// </summary>
        /// <param name="s">The source block</param>
        /// <param name="d">Destination block</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IDCT8x4_LeftPart(ref Block8x8F s, ref Block8x8F d)
        {
            Vector4 my1 = s.V1L;
            Vector4 my7 = s.V7L;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = s.V3L;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = s.V5L;
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

            Vector4 my2 = s.V2L;
            Vector4 my6 = s.V6L;
            mz4 = (my2 + my6) * C_0_541196;
            Vector4 my0 = s.V0L;
            Vector4 my4 = s.V4L;
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
        /// Original src:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
        /// </summary>
        /// <param name="s">The source block</param>
        /// <param name="d">The destination block</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IDCT8x4_RightPart(ref Block8x8F s, ref Block8x8F d)
        {
            Vector4 my1 = s.V1R;
            Vector4 my7 = s.V7R;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = s.V3R;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = s.V5R;
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

            Vector4 my2 = s.V2R;
            Vector4 my6 = s.V6R;
            mz4 = (my2 + my6) * C_0_541196;
            Vector4 my0 = s.V0R;
            Vector4 my4 = s.V4R;
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

#if SUPPORTS_RUNTIME_INTRINSICS
        /// <summary>
        /// Do IDCT internal operations on the given block.
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void IDCT8x8_Avx(ref Block8x8F s, ref Block8x8F d)
        {
            Vector256<float> my1 = s.V1;
            Vector256<float> my7 = s.V7;
            Vector256<float> mz0 = Avx.Add(my1, my7);

            Vector256<float> my3 = s.V3;
            Vector256<float> mz2 = Avx.Add(my3, my7);
            Vector256<float> my5 = s.V5;
            Vector256<float> mz1 = Avx.Add(my3, my5);
            Vector256<float> mz3 = Avx.Add(my1, my5);

            Vector256<float> mz4 = Avx.Multiply(Avx.Add(mz0, mz1), C_V_1_1758);

            if (Fma.IsSupported)
            {
                mz2 = Fma.MultiplyAdd(mz2, C_V_n1_9615, mz4);
                mz3 = Fma.MultiplyAdd(mz3, C_V_n0_3901, mz4);
            }
            else
            {
                mz2 = Avx.Add(Avx.Multiply(mz2, C_V_n1_9615), mz4);
                mz3 = Avx.Add(Avx.Multiply(mz3, C_V_n0_3901), mz4);
            }

            mz0 = Avx.Multiply(mz0, C_V_n0_8999);
            mz1 = Avx.Multiply(mz1, C_V_n2_5629);


            Unsafe.SkipInit(out Vector256<float> mb3);
            Unsafe.SkipInit(out Vector256<float> mb2);
            Unsafe.SkipInit(out Vector256<float> mb1);
            Unsafe.SkipInit(out Vector256<float> mb0);

            if (Fma.IsSupported)
            {
                mb3 = Avx.Add(Fma.MultiplyAdd(my7, C_V_0_2986, mz0), mz2);
                mb2 = Avx.Add(Fma.MultiplyAdd(my5, C_V_2_0531, mz1), mz3);
                mb1 = Avx.Add(Fma.MultiplyAdd(my3, C_V_3_0727, mz1), mz2);
                mb0 = Avx.Add(Fma.MultiplyAdd(my1, C_V_1_5013, mz0), mz3);
            }
            else
            {
                mb3 = Avx.Add(Avx.Add(Avx.Multiply(my7, C_V_0_2986), mz0), mz2);
                mb2 = Avx.Add(Avx.Add(Avx.Multiply(my5, C_V_2_0531), mz1), mz3);
                mb1 = Avx.Add(Avx.Add(Avx.Multiply(my3, C_V_3_0727), mz1), mz2);
                mb0 = Avx.Add(Avx.Add(Avx.Multiply(my1, C_V_1_5013), mz0), mz3);
            }

            Vector256<float> my2 = s.V2;
            Vector256<float> my6 = s.V6;
            mz4 = Avx.Multiply(Avx.Add(my2, my6), C_V_0_5411);
            Vector256<float> my0 = s.V0;
            Vector256<float> my4 = s.V4;
            mz0 = Avx.Add(my0, my4);
            mz1 = Avx.Subtract(my0, my4);

            if (Fma.IsSupported)
            {
                mz2 = Fma.MultiplyAdd(my6, C_V_n1_8477, mz4);
                mz3 = Fma.MultiplyAdd(my2, C_V_0_7653, mz4);
            }
            else
            {
                mz2 = Avx.Add(Avx.Multiply(my6, C_V_n1_8477), mz4);
                mz3 = Avx.Add(Avx.Multiply(my2, C_V_0_7653), mz4);
            }

            my0 = Avx.Add(mz0, mz3);
            my3 = Avx.Subtract(mz0, mz3);
            my1 = Avx.Add(mz1, mz2);
            my2 = Avx.Subtract(mz1, mz2);

            d.V0 = Avx.Add(my0, mb0);
            d.V7 = Avx.Subtract(my0, mb0);
            d.V1 = Avx.Add(my1, mb1);
            d.V6 = Avx.Subtract(my1, mb1);
            d.V2 = Avx.Add(my2, mb2);
            d.V5 = Avx.Subtract(my2, mb2);
            d.V3 = Avx.Add(my3, mb3);
            d.V4 = Avx.Subtract(my3, mb3);
        }
#endif
    }
}
