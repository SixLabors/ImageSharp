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
        private static readonly Vector256<float> C_V_1_1758 = Vector256.Create(1.175876f);

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
#endif
#pragma warning restore SA1310 // FieldNamesMustNotContainUnderscore
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
        /// Apply floating point IDCT inplace.
        /// Ported from https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L239.
        /// </summary>
        /// <param name="block">Input matrix.</param>
        /// <param name="temp">Matrix to store temporal results.</param>
        public static void TransformIDCT(ref Block8x8F block, ref Block8x8F temp)
        {
            block.Transpose();
            IDCT8x8(ref block, ref temp);
            temp.Transpose();
            IDCT8x8(ref temp, ref block);

            // TODO: What if we leave the blocks in a scaled-by-x8 state until final color packing?
            block.MultiplyInPlace(C_0_125);
        }

        /// <summary>
        /// Apply 2D floating point FDCT inplace using scalar operations.
        /// </summary>
        /// <remarks>
        /// Ported from libjpeg-turbo https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/jfdctflt.c.
        /// </remarks>
        /// <param name="block">Input matrix.</param>
        private static void ForwardTransformScalar(ref Block8x8F block)
        {
            const int dctSize = 8;

            float tmp0, tmp1, tmp2, tmp3, tmp4, tmp5, tmp6, tmp7;
            float tmp10, tmp11, tmp12, tmp13;
            float z1, z2, z3, z4, z5, z11, z13;

            // First pass - process rows
            ref float dataRef = ref Unsafe.As<Block8x8F, float>(ref block);
            for (int ctr = 7; ctr >= 0; ctr--)
            {
                tmp0 = Unsafe.Add(ref dataRef, 0) + Unsafe.Add(ref dataRef, 7);
                tmp7 = Unsafe.Add(ref dataRef, 0) - Unsafe.Add(ref dataRef, 7);
                tmp1 = Unsafe.Add(ref dataRef, 1) + Unsafe.Add(ref dataRef, 6);
                tmp6 = Unsafe.Add(ref dataRef, 1) - Unsafe.Add(ref dataRef, 6);
                tmp2 = Unsafe.Add(ref dataRef, 2) + Unsafe.Add(ref dataRef, 5);
                tmp5 = Unsafe.Add(ref dataRef, 2) - Unsafe.Add(ref dataRef, 5);
                tmp3 = Unsafe.Add(ref dataRef, 3) + Unsafe.Add(ref dataRef, 4);
                tmp4 = Unsafe.Add(ref dataRef, 3) - Unsafe.Add(ref dataRef, 4);

                // Even part
                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                Unsafe.Add(ref dataRef, 0) = tmp10 + tmp11;
                Unsafe.Add(ref dataRef, 4) = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781f;
                Unsafe.Add(ref dataRef, 2) = tmp13 + z1;
                Unsafe.Add(ref dataRef, 6) = tmp13 - z1;

                // Odd part
                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                z5 = (tmp10 - tmp12) * 0.382683433f;
                z2 = (0.541196100f * tmp10) + z5;
                z4 = (1.306562965f * tmp12) + z5;
                z3 = tmp11 * 0.707106781f;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                Unsafe.Add(ref dataRef, 5) = z13 + z2;
                Unsafe.Add(ref dataRef, 3) = z13 - z2;
                Unsafe.Add(ref dataRef, 1) = z11 + z4;
                Unsafe.Add(ref dataRef, 7) = z11 - z4;

                dataRef = ref Unsafe.Add(ref dataRef, dctSize);
            }

            // Second pass - process columns
            dataRef = ref Unsafe.As<Block8x8F, float>(ref block);
            for (int ctr = 7; ctr >= 0; ctr--)
            {
                tmp0 = Unsafe.Add(ref dataRef, dctSize * 0) + Unsafe.Add(ref dataRef, dctSize * 7);
                tmp7 = Unsafe.Add(ref dataRef, dctSize * 0) - Unsafe.Add(ref dataRef, dctSize * 7);
                tmp1 = Unsafe.Add(ref dataRef, dctSize * 1) + Unsafe.Add(ref dataRef, dctSize * 6);
                tmp6 = Unsafe.Add(ref dataRef, dctSize * 1) - Unsafe.Add(ref dataRef, dctSize * 6);
                tmp2 = Unsafe.Add(ref dataRef, dctSize * 2) + Unsafe.Add(ref dataRef, dctSize * 5);
                tmp5 = Unsafe.Add(ref dataRef, dctSize * 2) - Unsafe.Add(ref dataRef, dctSize * 5);
                tmp3 = Unsafe.Add(ref dataRef, dctSize * 3) + Unsafe.Add(ref dataRef, dctSize * 4);
                tmp4 = Unsafe.Add(ref dataRef, dctSize * 3) - Unsafe.Add(ref dataRef, dctSize * 4);

                // Even part
                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                Unsafe.Add(ref dataRef, dctSize * 0) = tmp10 + tmp11;
                Unsafe.Add(ref dataRef, dctSize * 4) = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781f;
                Unsafe.Add(ref dataRef, dctSize * 2) = tmp13 + z1;
                Unsafe.Add(ref dataRef, dctSize * 6) = tmp13 - z1;

                // Odd part
                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                z5 = (tmp10 - tmp12) * 0.382683433f;
                z2 = (0.541196100f * tmp10) + z5;
                z4 = (1.306562965f * tmp12) + z5;
                z3 = tmp11 * 0.707106781f;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                Unsafe.Add(ref dataRef, dctSize * 5) = z13 + z2;
                Unsafe.Add(ref dataRef, dctSize * 3) = z13 - z2;
                Unsafe.Add(ref dataRef, dctSize * 1) = z11 + z4;
                Unsafe.Add(ref dataRef, dctSize * 7) = z11 - z4;

                dataRef = ref Unsafe.Add(ref dataRef, 1);
            }
        }

        /// <summary>
        /// Apply 2D floating point FDCT inplace.
        /// </summary>
        /// <param name="block">Input matrix.</param>
        public static void TransformFDCT(ref Block8x8F block)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx.IsSupported || Sse.IsSupported)
            {
                ForwardTransformSimd(ref block);
            }
            else
#endif
            {
                ForwardTransformScalar(ref block);
            }
        }
    }
}
