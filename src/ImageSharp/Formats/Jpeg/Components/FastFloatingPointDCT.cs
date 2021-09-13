// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if SUPPORTS_RUNTIME_INTRINSICS
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
#pragma warning restore SA1310 // FieldNamesMustNotContainUnderscore

        /// <summary>
        /// Gets reciprocal coefficients for jpeg quantization tables calculation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Current FDCT implementation expects its results to be multiplied by
        /// a reciprocal quantization table. To get 8x8 reciprocal block values in this
        /// table must be divided by quantization table values scaled with quality settings.
        /// </para>
        /// <para>
        /// These values were calculates with this formula:
        /// <code>
        /// value[row * 8 + col] = scalefactor[row] * scalefactor[col] * 8;
        /// </code>
        /// Where:
        /// <code>
        /// scalefactor[0] = 1
        /// </code>
        /// <code>
        /// scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
        /// </code>
        /// Values are also scaled by 8 so DCT code won't do unnecessary division.
        /// </para>
        /// </remarks>
        public static readonly float[] DctReciprocalAdjustmentCoefficients = new float[]
        {
            0.125f, 0.09011998f, 0.09567086f, 0.10630376f, 0.125f, 0.15909483f, 0.23096988f, 0.45306373f,
            0.09011998f, 0.064972885f, 0.068974845f, 0.07664074f, 0.09011998f, 0.11470097f, 0.16652f, 0.32664075f,
            0.09567086f, 0.068974845f, 0.07322331f, 0.081361376f, 0.09567086f, 0.121765904f, 0.17677669f, 0.34675997f,
            0.10630376f, 0.07664074f, 0.081361376f, 0.09040392f, 0.10630376f, 0.13529903f, 0.19642374f, 0.38529903f,
            0.125f, 0.09011998f, 0.09567086f, 0.10630376f, 0.125f, 0.15909483f, 0.23096988f, 0.45306373f,
            0.15909483f, 0.11470097f, 0.121765904f, 0.13529903f, 0.15909483f, 0.2024893f, 0.2939689f, 0.5766407f,
            0.23096988f, 0.16652f, 0.17677669f, 0.19642374f, 0.23096988f, 0.2939689f, 0.4267767f, 0.8371526f,
            0.45306373f, 0.32664075f, 0.34675997f, 0.38529903f, 0.45306373f, 0.5766407f, 0.8371526f, 1.642134f,
        };

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

            // TODO: This can be fused into quantization table step
            block.MultiplyInPlace(C_0_125);
        }

        /// <summary>
        /// Apply 2D floating point FDCT inplace.
        /// </summary>
        /// <param name="block">Input matrix.</param>
        public static void TransformFDCT(ref Block8x8F block)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse.IsSupported)
            {
                ForwardTransformSimd(ref block);
            }
            else
#endif
            {
                ForwardTransformScalar(ref block);
            }
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
    }
}
