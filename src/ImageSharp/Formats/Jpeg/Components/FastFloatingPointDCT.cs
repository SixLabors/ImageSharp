// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Contains inaccurate, but fast forward and inverse DCT implementations.
    /// </summary>
    internal static class FastFloatingPointDCT
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
        private static readonly Vector4 InvSqrt2 = new Vector4(0.707107f);

        /// <summary>
        /// Apply floating point IDCT transformation into dest, using a temporary block 'temp' provided by the caller (optimization).
        /// Ported from https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L239
        /// </summary>
        /// <param name="src">Source</param>
        /// <param name="dest">Destination</param>
        /// <param name="temp">Temporary block provided by the caller</param>
        public static void TransformIDCT(ref Block8x8F src, ref Block8x8F dest, ref Block8x8F temp)
        {
            // TODO: Transpose is a bottleneck now. We need full AVX support to optimize it:
            // https://github.com/dotnet/corefx/issues/22940
            src.TransposeInto(ref temp);

            IDCT8x4_LeftPart(ref temp, ref dest);
            IDCT8x4_RightPart(ref temp, ref dest);

            dest.TransposeInto(ref temp);

            IDCT8x4_LeftPart(ref temp, ref dest);
            IDCT8x4_RightPart(ref temp, ref dest);

            // TODO: What if we leave the blocks in a scaled-by-x8 state until final color packing?
            dest.MultiplyInplace(C_0_125);
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
        /// Apply floating point IDCT transformation into dest, using a temporary block 'temp' provided by the caller (optimization)
        /// </summary>
        /// <param name="src">Source</param>
        /// <param name="dest">Destination</param>
        /// <param name="temp">Temporary block provided by the caller</param>
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
                temp.AddToAllInplace(new Vector4(-128));
            }

            FDCT8x4_LeftPart(ref temp, ref dest);
            FDCT8x4_RightPart(ref temp, ref dest);

            dest.TransposeInto(ref temp);

            FDCT8x4_LeftPart(ref temp, ref dest);
            FDCT8x4_RightPart(ref temp, ref dest);

            dest.MultiplyInplace(C_0_125);
        }
    }
}