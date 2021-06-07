// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
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
#pragma warning disable SA1310 // FieldNamesMustNotContainUnderscore
        private const float C_1_175876 = 1.175875602f;

        private const float C_1_961571 = -1.961570560f;

        private const float C_0_390181 = -0.390180644f;

        private const float C_0_899976 = -0.899976223f;

        private const float C_2_562915 = -2.562915447f;

        private const float C_0_298631 = 0.298631336f;

        private const float C_2_053120 = 2.053119869f;

        private const float C_3_072711 = 3.072711026f;

        private const float C_1_501321 = 1.501321110f;

        private const float C_0_541196 = 0.541196100f;

        private const float C_1_847759 = -1.847759065f;

        private const float C_0_765367 = 0.765366865f;

        private const float C_0_125 = 0.1250f;

#if SUPPORTS_RUNTIME_INTRINSICS
        private static readonly Vector256<float> C_V_0_5411 = Vector256.Create(0.541196f);
        private static readonly Vector256<float> C_V_1_3065 = Vector256.Create(1.306563f);
        private static readonly Vector256<float> C_V_1_1758 = Vector256.Create(1.175876f);
        private static readonly Vector256<float> C_V_0_7856 = Vector256.Create(0.785695f);
        private static readonly Vector256<float> C_V_1_3870 = Vector256.Create(1.387040f);
        private static readonly Vector256<float> C_V_0_2758 = Vector256.Create(0.275899f);

        private static readonly Vector256<float> C_V_n1_9615 = Vector256.Create(-1.961570560f);
        private static readonly Vector256<float> C_V_n0_3901 = Vector256.Create(-0.390180644f);
        private static readonly Vector256<float> C_V_n0_8999 = Vector256.Create(-0.899976223f);
        private static readonly Vector256<float> C_V_n2_5629 = Vector256.Create(-2.562915447f);
        private static readonly Vector256<float> C_V_0_2986 = Vector256.Create(0.298631336f);
        private static readonly Vector256<float> C_V_2_0531 = Vector256.Create(2.053119869f);
        private static readonly Vector256<float> C_V_3_0727 = Vector256.Create(3.072711026f);
        private static readonly Vector256<float> C_V_1_5013 = Vector256.Create(1.501321110f);
        private static readonly Vector256<float> C_V_n1_8477 = Vector256.Create(-1.847759065f);
        private static readonly Vector256<float> C_V_0_7653 = Vector256.Create(0.765366865f);

        private static Vector256<float> C_V_InvSqrt2 = Vector256.Create(0.707107f);
#endif
#pragma warning restore SA1310 // FieldNamesMustNotContainUnderscore
        private static readonly Vector4 InvSqrt2 = new Vector4(0.707107f);

        /// <summary>
        /// Original:
        /// <see>
        ///     <cref>https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L15</cref>
        /// </see>
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void FDCT8x4_LeftPart(ref Block8x8F s, ref Block8x8F d)
        {
            Vector4 c0 = s.V0L;
            Vector4 c1 = s.V7L;
            Vector4 t0 = c0 + c1;
            Vector4 t7 = c0 - c1;

            c1 = s.V6L;
            c0 = s.V1L;
            Vector4 t1 = c0 + c1;
            Vector4 t6 = c0 - c1;

            c1 = s.V5L;
            c0 = s.V2L;
            Vector4 t2 = c0 + c1;
            Vector4 t5 = c0 - c1;

            c0 = s.V3L;
            c1 = s.V4L;
            Vector4 t3 = c0 + c1;
            Vector4 t4 = c0 - c1;

            c0 = t0 + t3;
            Vector4 c3 = t0 - t3;
            c1 = t1 + t2;
            Vector4 c2 = t1 - t2;

            d.V0L = c0 + c1;
            d.V4L = c0 - c1;

            float w0 = 0.541196f;
            float w1 = 1.306563f;

            d.V2L = (w0 * c2) + (w1 * c3);
            d.V6L = (w0 * c3) - (w1 * c2);

            w0 = 1.175876f;
            w1 = 0.785695f;
            c3 = (w0 * t4) + (w1 * t7);
            c0 = (w0 * t7) - (w1 * t4);

            w0 = 1.387040f;
            w1 = 0.275899f;
            c2 = (w0 * t5) + (w1 * t6);
            c1 = (w0 * t6) - (w1 * t5);

            d.V3L = c0 - c2;
            d.V5L = c3 - c1;

            float invsqrt2 = 0.707107f;
            c0 = (c0 + c2) * invsqrt2;
            c3 = (c3 + c1) * invsqrt2;

            d.V1L = c0 + c3;
            d.V7L = c0 - c3;
        }

        /// <summary>
        /// Original:
        /// <see>
        ///     <cref>https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L15</cref>
        /// </see>
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void FDCT8x4_RightPart(ref Block8x8F s, ref Block8x8F d)
        {
            Vector4 c0 = s.V0R;
            Vector4 c1 = s.V7R;
            Vector4 t0 = c0 + c1;
            Vector4 t7 = c0 - c1;

            c1 = s.V6R;
            c0 = s.V1R;
            Vector4 t1 = c0 + c1;
            Vector4 t6 = c0 - c1;

            c1 = s.V5R;
            c0 = s.V2R;
            Vector4 t2 = c0 + c1;
            Vector4 t5 = c0 - c1;

            c0 = s.V3R;
            c1 = s.V4R;
            Vector4 t3 = c0 + c1;
            Vector4 t4 = c0 - c1;

            c0 = t0 + t3;
            Vector4 c3 = t0 - t3;
            c1 = t1 + t2;
            Vector4 c2 = t1 - t2;

            d.V0R = c0 + c1;
            d.V4R = c0 - c1;

            float w0 = 0.541196f;
            float w1 = 1.306563f;

            d.V2R = (w0 * c2) + (w1 * c3);
            d.V6R = (w0 * c3) - (w1 * c2);

            w0 = 1.175876f;
            w1 = 0.785695f;
            c3 = (w0 * t4) + (w1 * t7);
            c0 = (w0 * t7) - (w1 * t4);

            w0 = 1.387040f;
            w1 = 0.275899f;
            c2 = (w0 * t5) + (w1 * t6);
            c1 = (w0 * t6) - (w1 * t5);

            d.V3R = c0 - c2;
            d.V5R = c3 - c1;

            c0 = (c0 + c2) * InvSqrt2;
            c3 = (c3 + c1) * InvSqrt2;

            d.V1R = c0 + c3;
            d.V7R = c0 - c3;
        }

        /// <summary>
        /// Combined operation of <see cref="FDCT8x4_LeftPart(ref Block8x8F, ref Block8x8F)"/> and <see cref="FDCT8x4_RightPart(ref Block8x8F, ref Block8x8F)"/>
        /// using AVX commands.
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void FDCT8x8_Avx(ref Block8x8F s, ref Block8x8F d)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            Debug.Assert(Avx.IsSupported, "AVX is required to execute this method");

            Vector256<float> t0 = Avx.Add(s.V0, s.V7);
            Vector256<float> t7 = Avx.Subtract(s.V0, s.V7);
            Vector256<float> t1 = Avx.Add(s.V1, s.V6);
            Vector256<float> t6 = Avx.Subtract(s.V1, s.V6);
            Vector256<float> t2 = Avx.Add(s.V2, s.V5);
            Vector256<float> t5 = Avx.Subtract(s.V2, s.V5);
            Vector256<float> t3 = Avx.Add(s.V3, s.V4);
            Vector256<float> t4 = Avx.Subtract(s.V3, s.V4);

            Vector256<float> c0 = Avx.Add(t0, t3);
            Vector256<float> c1 = Avx.Add(t1, t2);

            // 0 4
            d.V0 = Avx.Add(c0, c1);
            d.V4 = Avx.Subtract(c0, c1);

            Vector256<float> c3 = Avx.Subtract(t0, t3);
            Vector256<float> c2 = Avx.Subtract(t1, t2);

            // 2 6
            d.V2 = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(c2, C_V_0_5411), c3, C_V_1_3065);
            d.V6 = SimdUtils.HwIntrinsics.MultiplySubstract(Avx.Multiply(c2, C_V_1_3065), c3, C_V_0_5411);

            c3 = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(t4, C_V_1_1758), t7, C_V_0_7856);
            c0 = SimdUtils.HwIntrinsics.MultiplySubstract(Avx.Multiply(t4, C_V_0_7856), t7, C_V_1_1758);

            c2 = SimdUtils.HwIntrinsics.MultiplyAdd(Avx.Multiply(t5, C_V_1_3870), C_V_0_2758, t6);
            c1 = SimdUtils.HwIntrinsics.MultiplySubstract(Avx.Multiply(C_V_0_2758, t5), t6, C_V_1_3870);

            // 3 5
            d.V3 = Avx.Subtract(c0, c2);
            d.V5 = Avx.Subtract(c3, c1);

            c0 = Avx.Multiply(Avx.Add(c0, c2), C_V_InvSqrt2);
            c3 = Avx.Multiply(Avx.Add(c3, c1), C_V_InvSqrt2);

            // 1 7
            d.V1 = Avx.Add(c0, c3);
            d.V7 = Avx.Subtract(c0, c3);
#endif
        }

        /// <summary>
        /// Performs 8x8 matrix Forward Discrete Cosine Transform
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void FDCT8x8(ref Block8x8F s, ref Block8x8F d)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported)
            {
                FDCT8x8_Avx(ref s, ref d);
            }
            else
#endif
            {
                FDCT8x4_LeftPart(ref s, ref d);
                FDCT8x4_RightPart(ref s, ref d);
            }
        }

        /// <summary>
        /// Apply floating point FDCT from src into dest
        /// </summary>
        /// <remarks></remarks>
        /// <param name="src">Source</param>
        /// <param name="dest">Destination</param>
        /// <param name="temp">Temporary block provided by the caller for optimization</param>
        /// <param name="offsetSourceByNeg128">If true, a constant -128.0 offset is applied for all values before FDCT </param>
        public static void TransformFDCT(
            ref Block8x8F src,
            ref Block8x8F dest,
            ref Block8x8F temp,
            bool offsetSourceByNeg128 = true)
        {
            src.TransposeInto(ref temp);
            if (offsetSourceByNeg128)
            {
                temp.AddInPlace(-128F);
            }

            FDCT8x8(ref temp, ref dest);

            dest.TransposeInto(ref temp);

            FDCT8x8(ref temp, ref dest);

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

        /// <summary>
        /// Combined operation of <see cref="IDCT8x4_LeftPart(ref Block8x8F, ref Block8x8F)"/> and <see cref="IDCT8x4_RightPart(ref Block8x8F, ref Block8x8F)"/>
        /// using AVX commands.
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void IDCT8x8_Avx(ref Block8x8F s, ref Block8x8F d)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            Debug.Assert(Avx.IsSupported, "AVX is required to execute this method");

            Vector256<float> my1 = s.V1;
            Vector256<float> my7 = s.V7;
            Vector256<float> mz0 = Avx.Add(my1, my7);

            Vector256<float> my3 = s.V3;
            Vector256<float> mz2 = Avx.Add(my3, my7);
            Vector256<float> my5 = s.V5;
            Vector256<float> mz1 = Avx.Add(my3, my5);
            Vector256<float> mz3 = Avx.Add(my1, my5);

            Vector256<float> mz4 = Avx.Multiply(Avx.Add(mz0, mz1), C_V_1_1758);

            mz2 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, mz2, C_V_n1_9615);
            mz3 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, mz3, C_V_n0_3901);
            mz0 = Avx.Multiply(mz0, C_V_n0_8999);
            mz1 = Avx.Multiply(mz1, C_V_n2_5629);

            Vector256<float> mb3 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz0, my7, C_V_0_2986), mz2);
            Vector256<float> mb2 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz1, my5, C_V_2_0531), mz3);
            Vector256<float> mb1 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz1, my3, C_V_3_0727), mz2);
            Vector256<float> mb0 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz0, my1, C_V_1_5013), mz3);


            Vector256<float> my2 = s.V2;
            Vector256<float> my6 = s.V6;
            mz4 = Avx.Multiply(Avx.Add(my2, my6), C_V_0_5411);
            Vector256<float> my0 = s.V0;
            Vector256<float> my4 = s.V4;
            mz0 = Avx.Add(my0, my4);
            mz1 = Avx.Subtract(my0, my4);
            mz2 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, my6, C_V_n1_8477);
            mz3 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, my2, C_V_0_7653);

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
#endif
        }

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
    }
}
