// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static partial class ReferenceImplementations
    {
        /// <summary>
        /// Contains port of non-optimized methods in:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp
        ///
        /// *** Paper ***
        /// paper LLM89
        /// C. Loeffler, A. Ligtenberg, and G. S. Moschytz,
        /// "Practical fast 1-D DCT algorithms with 11 multiplications,"
        /// Proc. Int'l. Conf. on Acoustics, Speech, and Signal Processing (ICASSP89), pp. 988-991, 1989.
        ///
        /// The main purpose of this code is testing and documentation, it is intended to be similar to it's original counterpart.
        /// DO NOT clean it!
        /// DO NOT StyleCop it!
        /// </summary>
        internal static class LLM_FloatingPoint_DCT
        {
            public static Block8x8F TransformIDCT(ref Block8x8F source)
            {
                float[] s = new float[64];
                source.ScaledCopyTo(s);
                float[] d = new float[64];
                float[] temp = new float[64];

                IDCT2D_llm(s, d, temp);
                Block8x8F result = default;
                result.LoadFrom(d);
                return result;
            }

            public static Block8x8F TransformFDCT_UpscaleBy8(ref Block8x8F source)
            {
                float[] s = new float[64];
                source.ScaledCopyTo(s);
                float[] d = new float[64];
                float[] temp = new float[64];

                FDCT2D_llm(s, d, temp);
                Block8x8F result = default;
                result.LoadFrom(d);
                return result;
            }

            private static double Cos(double x) => Math.Cos(x);

            private const double M_PI = Math.PI;

            private static readonly double M_SQRT2 = Math.Sqrt(2);

            public static float[] PrintConstants(ITestOutputHelper output)
            {
                float[] r = new float[8];
                for (int i = 0; i < 8; i++)
                {
                    r[i] = (float)(Cos(i / 16.0 * M_PI) * M_SQRT2);
                    output?.WriteLine($"float r{i} = {r[i]:R}f;");
                }

                return r;
            }

#pragma warning disable 219

            /// <summary>
            /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L200
            /// </summary>
            private static void IDCT1Dllm_32f(Span<float> y, Span<float> x)
            {
                float a0, a1, a2, a3, b0, b1, b2, b3;
                float z0, z1, z2, z3, z4;

                // see: PrintConstants()
                float r0 = 1.41421354f;
                float r1 = 1.3870399f;
                float r2 = 1.306563f;
                float r3 = 1.17587554f;
                float r4 = 1f;
                float r5 = 0.785694957f;
                float r6 = 0.5411961f;
                float r7 = 0.27589938f;

                z0 = y[1] + y[7];
                z1 = y[3] + y[5];
                z2 = y[3] + y[7];
                z3 = y[1] + y[5];
                z4 = (z0 + z1) * r3;

                z0 = z0 * (-r3 + r7);
                z1 = z1 * (-r3 - r1);
                z2 = (z2 * (-r3 - r5)) + z4;
                z3 = (z3 * (-r3 + r5)) + z4;

                b3 = (y[7] * (-r1 + r3 + r5 - r7)) + z0 + z2;
                b2 = (y[5] * (r1 + r3 - r5 + r7)) + z1 + z3;
                b1 = (y[3] * (r1 + r3 + r5 - r7)) + z1 + z2;
                b0 = (y[1] * (r1 + r3 - r5 - r7)) + z0 + z3;

                z4 = (y[2] + y[6]) * r6;
                z0 = y[0] + y[4];
                z1 = y[0] - y[4];
                z2 = z4 - (y[6] * (r2 + r6));
                z3 = z4 + (y[2] * (r2 - r6));
                a0 = z0 + z3;
                a3 = z0 - z3;
                a1 = z1 + z2;
                a2 = z1 - z2;

                x[0] = a0 + b0;
                x[7] = a0 - b0;
                x[1] = a1 + b1;
                x[6] = a1 - b1;
                x[2] = a2 + b2;
                x[5] = a2 - b2;
                x[3] = a3 + b3;
                x[4] = a3 - b3;
            }

            /// <summary>
            /// Original: https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L239
            /// Applies IDCT transformation on "s" copying transformed values to "d", using temporary block "temp"
            /// </summary>
            internal static void IDCT2D_llm(Span<float> s, Span<float> d, Span<float> temp)
            {
                int j;

                for (j = 0; j < 8; j++)
                {
                    IDCT1Dllm_32f(s.Slice(j * 8), temp.Slice(j * 8));
                }

                Transpose8x8(temp, d);

                for (j = 0; j < 8; j++)
                {
                    IDCT1Dllm_32f(d.Slice(j * 8), temp.Slice(j * 8));
                }

                Transpose8x8(temp, d);

                for (j = 0; j < 64; j++)
                {
                    d[j] *= 0.125f;
                }
            }

            /// <summary>
            /// Original:
            /// <see>
            ///     <cref>https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L15</cref>
            /// </see>
            /// </summary>
            /// <param name="s">Source</param>
            /// <param name="d">Destination</param>
            public static void FDCT2D8x4_32f(Span<float> s, Span<float> d)
            {
                Vector4 c0 = _mm_load_ps(s, 0);
                Vector4 c1 = _mm_load_ps(s, 56);
                Vector4 t0 = c0 + c1;
                Vector4 t7 = c0 - c1;

                c1 = _mm_load_ps(s, 48);
                c0 = _mm_load_ps(s, 8);
                Vector4 t1 = c0 + c1;
                Vector4 t6 = c0 - c1;

                c1 = _mm_load_ps(s, 40);
                c0 = _mm_load_ps(s, 16);
                Vector4 t2 = c0 + c1;
                Vector4 t5 = c0 - c1;

                c0 = _mm_load_ps(s, 24);
                c1 = _mm_load_ps(s, 32);
                Vector4 t3 = c0 + c1;
                Vector4 t4 = c0 - c1;

                /*
                c1 = x[0]; c2 = x[7]; t0 = c1 + c2; t7 = c1 - c2;
                c1 = x[1]; c2 = x[6]; t1 = c1 + c2; t6 = c1 - c2;
                c1 = x[2]; c2 = x[5]; t2 = c1 + c2; t5 = c1 - c2;
                c1 = x[3]; c2 = x[4]; t3 = c1 + c2; t4 = c1 - c2;
                */

                c0 = t0 + t3;
                Vector4 c3 = t0 - t3;
                c1 = t1 + t2;
                Vector4 c2 = t1 - t2;

                /*
                c0 = t0 + t3; c3 = t0 - t3;
                c1 = t1 + t2; c2 = t1 - t2;
                */

                _mm_store_ps(d, 0, c0 + c1);

                _mm_store_ps(d, 32, c0 - c1);

                /*y[0] = c0 + c1;
                y[4] = c0 - c1;*/

                var w0 = new Vector4(0.541196f);
                var w1 = new Vector4(1.306563f);

                _mm_store_ps(d, 16, (w0 * c2) + (w1 * c3));

                _mm_store_ps(d, 48, (w0 * c3) - (w1 * c2));
                /*
                y[2] = c2 * r[6] + c3 * r[2];
                y[6] = c3 * r[6] - c2 * r[2];
                */

                w0 = new Vector4(1.175876f);
                w1 = new Vector4(0.785695f);
                c3 = (w0 * t4) + (w1 * t7);
                c0 = (w0 * t7) - (w1 * t4);
                /*
                c3 = t4 * r[3] + t7 * r[5];
                c0 = t7 * r[3] - t4 * r[5];
                */

                w0 = new Vector4(1.387040f);
                w1 = new Vector4(0.275899f);
                c2 = (w0 * t5) + (w1 * t6);
                c1 = (w0 * t6) - (w1 * t5);
                /*
                c2 = t5 * r[1] + t6 * r[7];
                c1 = t6 * r[1] - t5 * r[7];
                */

                _mm_store_ps(d, 24, c0 - c2);

                _mm_store_ps(d, 40, c3 - c1);

                // y[5] = c3 - c1; y[3] = c0 - c2;
                var invsqrt2 = new Vector4(0.707107f);
                c0 = (c0 + c2) * invsqrt2;
                c3 = (c3 + c1) * invsqrt2;

                // c0 = (c0 + c2) * invsqrt2;
                // c3 = (c3 + c1) * invsqrt2;
                _mm_store_ps(d, 8, c0 + c3);
                _mm_store_ps(d, 56, c0 - c3);

                // y[1] = c0 + c3; y[7] = c0 - c3;
                /*for(i = 0;i < 8;i++)
                {
                y[i] *= invsqrt2h;
                }*/
            }

            public static void FDCT8x8_llm_sse(Span<float> s, Span<float> d, Span<float> temp)
            {
                Transpose8x8(s, temp);

                FDCT2D8x4_32f(temp, d);

                FDCT2D8x4_32f(temp.Slice(4), d.Slice(4));

                Transpose8x8(d, temp);

                FDCT2D8x4_32f(temp, d);

                FDCT2D8x4_32f(temp.Slice(4), d.Slice(4));

                var c = new Vector4(0.1250f);

#pragma warning disable SA1107 // Code should not contain multiple statements on one line
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 0
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 1
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 2
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 3
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 4
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 5
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 6
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 7
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 8
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 9
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 10
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 11
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 12
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 13
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 14
                _mm_store_ps(d, 0, _mm_load_ps(d, 0) * c); d = d.Slice(4); // 15
#pragma warning restore SA1107 // Code should not contain multiple statements on one line
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
            private static Vector4 _mm_load_ps(Span<float> src, int offset)
#pragma warning restore SA1300 // Element should begin with upper-case letter
            {
                src = src.Slice(offset);
                return new Vector4(src[0], src[1], src[2], src[3]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
            private static void _mm_store_ps(Span<float> dest, int offset, Vector4 src)
#pragma warning restore SA1300 // Element should begin with upper-case letter
            {
                dest = dest.Slice(offset);
                dest[0] = src.X;
                dest[1] = src.Y;
                dest[2] = src.Z;
                dest[3] = src.W;
            }

            // Accurate variants of constants from:
            // https://github.com/mozilla/mozjpeg/blob/master/simd/jfdctint-altivec.c
#pragma warning disable SA1309 // Field names should not begin with underscore
            private static readonly Vector4 _1_175876 = new Vector4(1.175875602f);

            private static readonly Vector4 _1_961571 = new Vector4(-1.961570560f);

            private static readonly Vector4 _0_390181 = new Vector4(-0.390180644f);

            private static readonly Vector4 _0_899976 = new Vector4(-0.899976223f);

            private static readonly Vector4 _2_562915 = new Vector4(-2.562915447f);

            private static readonly Vector4 _0_298631 = new Vector4(0.298631336f);

            private static readonly Vector4 _2_053120 = new Vector4(2.053119869f);

            private static readonly Vector4 _3_072711 = new Vector4(3.072711026f);

            private static readonly Vector4 _1_501321 = new Vector4(1.501321110f);

            private static readonly Vector4 _0_541196 = new Vector4(0.541196100f);

            private static readonly Vector4 _1_847759 = new Vector4(-1.847759065f);

            private static readonly Vector4 _0_765367 = new Vector4(0.765366865f);
#pragma warning restore SA1309 // Field names should not begin with underscore

            /// <summary>
            /// Original:
            /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
            /// Does a part of the IDCT job on the given parts of the blocks
            /// </summary>
            internal static void IDCT2D8x4_32f(Span<float> y, Span<float> x)
            {
                /*
                float a0,a1,a2,a3,b0,b1,b2,b3; float z0,z1,z2,z3,z4; float r[8]; int i;
                for(i = 0;i < 8;i++){ r[i] = (float)(cos((double)i / 16.0 * M_PI) * M_SQRT2); }
                */
                /*
                0: 1.414214
                1: 1.387040
                2: 1.306563
                3:
                4: 1.000000
                5: 0.785695
                6:
                7: 0.275899
                */

                Vector4 my1 = _mm_load_ps(y, 8);
                Vector4 my7 = _mm_load_ps(y, 56);
                Vector4 mz0 = my1 + my7;

                Vector4 my3 = _mm_load_ps(y, 24);
                Vector4 mz2 = my3 + my7;
                Vector4 my5 = _mm_load_ps(y, 40);
                Vector4 mz1 = my3 + my5;
                Vector4 mz3 = my1 + my5;

                Vector4 mz4 = (mz0 + mz1) * _1_175876;

                // z0 = y[1] + y[7]; z1 = y[3] + y[5]; z2 = y[3] + y[7]; z3 = y[1] + y[5];
                // z4 = (z0 + z1) * r[3];
                mz2 = (mz2 * _1_961571) + mz4;
                mz3 = (mz3 * _0_390181) + mz4;
                mz0 = mz0 * _0_899976;
                mz1 = mz1 * _2_562915;

                /*
                -0.899976
                -2.562915
                -1.961571
                -0.390181
                z0 = z0 * (-r[3] + r[7]);
                z1 = z1 * (-r[3] - r[1]);
                z2 = z2 * (-r[3] - r[5]) + z4;
                z3 = z3 * (-r[3] + r[5]) + z4;*/

                Vector4 mb3 = (my7 * _0_298631) + mz0 + mz2;
                Vector4 mb2 = (my5 * _2_053120) + mz1 + mz3;
                Vector4 mb1 = (my3 * _3_072711) + mz1 + mz2;
                Vector4 mb0 = (my1 * _1_501321) + mz0 + mz3;

                /*
                0.298631
                2.053120
                3.072711
                1.501321
                b3 = y[7] * (-r[1] + r[3] + r[5] - r[7]) + z0 + z2;
                b2 = y[5] * ( r[1] + r[3] - r[5] + r[7]) + z1 + z3;
                b1 = y[3] * ( r[1] + r[3] + r[5] - r[7]) + z1 + z2;
                b0 = y[1] * ( r[1] + r[3] - r[5] - r[7]) + z0 + z3;
                */

                Vector4 my2 = _mm_load_ps(y, 16);
                Vector4 my6 = _mm_load_ps(y, 48);
                mz4 = (my2 + my6) * _0_541196;
                Vector4 my0 = _mm_load_ps(y, 0);
                Vector4 my4 = _mm_load_ps(y, 32);
                mz0 = my0 + my4;
                mz1 = my0 - my4;

                mz2 = mz4 + (my6 * _1_847759);
                mz3 = mz4 + (my2 * _0_765367);

                my0 = mz0 + mz3;
                my3 = mz0 - mz3;
                my1 = mz1 + mz2;
                my2 = mz1 - mz2;
                /*
                1.847759
                0.765367
                z4 = (y[2] + y[6]) * r[6];
                z0 = y[0] + y[4]; z1 = y[0] - y[4];
                z2 = z4 - y[6] * (r[2] + r[6]);
                z3 = z4 + y[2] * (r[2] - r[6]);
                a0 = z0 + z3; a3 = z0 - z3;
                a1 = z1 + z2; a2 = z1 - z2;
                */

                _mm_store_ps(x, 0, my0 + mb0);

                _mm_store_ps(x, 56, my0 - mb0);

                _mm_store_ps(x, 8, my1 + mb1);

                _mm_store_ps(x, 48, my1 - mb1);

                _mm_store_ps(x, 16, my2 + mb2);

                _mm_store_ps(x, 40, my2 - mb2);

                _mm_store_ps(x, 24, my3 + mb3);

                _mm_store_ps(x, 32, my3 - mb3);
                /*
                x[0] = a0 + b0; x[7] = a0 - b0;
                x[1] = a1 + b1; x[6] = a1 - b1;
                x[2] = a2 + b2; x[5] = a2 - b2;
                x[3] = a3 + b3; x[4] = a3 - b3;
                for(i = 0;i < 8;i++){ x[i] *= 0.353554f; }
                */
            }

            internal static void FDCT1Dllm_32f(Span<float> x, Span<float> y)
            {
                float t0, t1, t2, t3, t4, t5, t6, t7;
                float c0, c1, c2, c3;
                float[] r = new float[8];

                // for(i = 0;i < 8;i++){ r[i] = (float)(cos((double)i / 16.0 * M_PI) * M_SQRT2); }
                r[0] = 1.414214f;
                r[1] = 1.387040f;
                r[2] = 1.306563f;
                r[3] = 1.175876f;
                r[4] = 1.000000f;
                r[5] = 0.785695f;
                r[6] = 0.541196f;
                r[7] = 0.275899f;

                const float invsqrt2 = 0.707107f; // (float)(1.0f / M_SQRT2);

                // const float invsqrt2h = 0.353554f; //invsqrt2*0.5f;
                c1 = x[0];
                c2 = x[7];
                t0 = c1 + c2;
                t7 = c1 - c2;
                c1 = x[1];
                c2 = x[6];
                t1 = c1 + c2;
                t6 = c1 - c2;
                c1 = x[2];
                c2 = x[5];
                t2 = c1 + c2;
                t5 = c1 - c2;
                c1 = x[3];
                c2 = x[4];
                t3 = c1 + c2;
                t4 = c1 - c2;

                c0 = t0 + t3;
                c3 = t0 - t3;
                c1 = t1 + t2;
                c2 = t1 - t2;

                y[0] = c0 + c1;
                y[4] = c0 - c1;
                y[2] = (c2 * r[6]) + (c3 * r[2]);
                y[6] = (c3 * r[6]) - (c2 * r[2]);

                c3 = (t4 * r[3]) + (t7 * r[5]);
                c0 = (t7 * r[3]) - (t4 * r[5]);
                c2 = (t5 * r[1]) + (t6 * r[7]);
                c1 = (t6 * r[1]) - (t5 * r[7]);

                y[5] = c3 - c1;
                y[3] = c0 - c2;
                c0 = (c0 + c2) * invsqrt2;
                c3 = (c3 + c1) * invsqrt2;
                y[1] = c0 + c3;
                y[7] = c0 - c3;
            }

            internal static void FDCT2D_llm(
                Span<float> s,
                Span<float> d,
                Span<float> temp,
                bool downscaleBy8 = false,
                bool subtract128FromSource = false)
            {
                Span<float> sWorker = subtract128FromSource ? s.AddScalarToAllValues(-128f) : s;

                for (int j = 0; j < 8; j++)
                {
                    FDCT1Dllm_32f(sWorker.Slice(j * 8), temp.Slice(j * 8));
                }

                Transpose8x8(temp, d);

                for (int j = 0; j < 8; j++)
                {
                    FDCT1Dllm_32f(d.Slice(j * 8), temp.Slice(j * 8));
                }

                Transpose8x8(temp, d);

                if (downscaleBy8)
                {
                    for (int j = 0; j < 64; j++)
                    {
                        d[j] *= 0.125f;
                    }
                }
            }
        }
    }
}
