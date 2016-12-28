// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Jpg;
    
    /// <summary>
    /// This class contains simplified (unefficient) reference implementations to produce verification data for unit tests
    /// Floating point DCT code Ported from https://github.com/norishigefukushima/dct_simd
    /// </summary>
    internal static class ReferenceImplementations
    {
        /// <summary>
        /// Transpose 8x8 block stored linearly in a span (inplace)
        /// </summary>
        /// <param name="data"></param>
        internal static void Transpose8x8(MutableSpan<float> data)
        {
            for (int i = 1; i < 8; i++)
            {
                int i8 = i * 8;
                for (int j = 0; j < i; j++)
                {
                    float tmp = data[i8 + j];
                    data[i8 + j] = data[j * 8 + i];
                    data[j * 8 + i] = tmp;
                }
            }
        }

        /// <summary>
        /// Transpose 8x8 block stored linearly in a span
        /// </summary>
        internal static void Transpose8x8(MutableSpan<float> src, MutableSpan<float> dest)
        {
            for (int i = 0; i < 8; i++)
            {
                int i8 = i * 8;
                for (int j = 0; j < 8; j++)
                {
                    dest[j * 8 + i] = src[i8 + j];
                }
            }
        }

        public static class IntegerReferenceDCT
        {
            private const int fix_0_298631336 = 2446;
            private const int fix_0_390180644 = 3196;
            private const int fix_0_541196100 = 4433;
            private const int fix_0_765366865 = 6270;
            private const int fix_0_899976223 = 7373;
            private const int fix_1_175875602 = 9633;
            private const int fix_1_501321110 = 12299;
            private const int fix_1_847759065 = 15137;
            private const int fix_1_961570560 = 16069;
            private const int fix_2_053119869 = 16819;
            private const int fix_2_562915447 = 20995;
            private const int fix_3_072711026 = 25172;

            /// <summary>
            /// The number of bits
            /// </summary>
            private const int Bits = 13;

            /// <summary>
            /// The number of bits to shift by on the first pass.
            /// </summary>
            private const int Pass1Bits = 2;

            /// <summary>
            /// The value to shift by
            /// </summary>
            private const int CenterJSample = 128;

            /// <summary>
            /// Performs a forward DCT on an 8x8 block of coefficients, including a level shift.
            /// Leave results scaled up by an overall factor of 8.
            /// </summary>
            /// <param name="block">The block of coefficients.</param>
            public static void TransformFDCTInplace(MutableSpan<int> block)
            {
                // Pass 1: process rows.
                for (int y = 0; y < 8; y++)
                {
                    int y8 = y * 8;

                    int x0 = block[y8];
                    int x1 = block[y8 + 1];
                    int x2 = block[y8 + 2];
                    int x3 = block[y8 + 3];
                    int x4 = block[y8 + 4];
                    int x5 = block[y8 + 5];
                    int x6 = block[y8 + 6];
                    int x7 = block[y8 + 7];

                    int tmp0 = x0 + x7;
                    int tmp1 = x1 + x6;
                    int tmp2 = x2 + x5;
                    int tmp3 = x3 + x4;

                    int tmp10 = tmp0 + tmp3;
                    int tmp12 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp13 = tmp1 - tmp2;

                    tmp0 = x0 - x7;
                    tmp1 = x1 - x6;
                    tmp2 = x2 - x5;
                    tmp3 = x3 - x4;

                    block[y8] = (tmp10 + tmp11 - (8 * CenterJSample)) << Pass1Bits;
                    block[y8 + 4] = (tmp10 - tmp11) << Pass1Bits;
                    int z1 = (tmp12 + tmp13) * fix_0_541196100;
                    z1 += 1 << (Bits - Pass1Bits - 1);
                    block[y8 + 2] = (z1 + (tmp12 * fix_0_765366865)) >> (Bits - Pass1Bits);
                    block[y8 + 6] = (z1 - (tmp13 * fix_1_847759065)) >> (Bits - Pass1Bits);

                    tmp10 = tmp0 + tmp3;
                    tmp11 = tmp1 + tmp2;
                    tmp12 = tmp0 + tmp2;
                    tmp13 = tmp1 + tmp3;
                    z1 = (tmp12 + tmp13) * fix_1_175875602;
                    z1 += 1 << (Bits - Pass1Bits - 1);
                    tmp0 = tmp0 * fix_1_501321110;
                    tmp1 = tmp1 * fix_3_072711026;
                    tmp2 = tmp2 * fix_2_053119869;
                    tmp3 = tmp3 * fix_0_298631336;
                    tmp10 = tmp10 * -fix_0_899976223;
                    tmp11 = tmp11 * -fix_2_562915447;
                    tmp12 = tmp12 * -fix_0_390180644;
                    tmp13 = tmp13 * -fix_1_961570560;

                    tmp12 += z1;
                    tmp13 += z1;
                    block[y8 + 1] = (tmp0 + tmp10 + tmp12) >> (Bits - Pass1Bits);
                    block[y8 + 3] = (tmp1 + tmp11 + tmp13) >> (Bits - Pass1Bits);
                    block[y8 + 5] = (tmp2 + tmp11 + tmp12) >> (Bits - Pass1Bits);
                    block[y8 + 7] = (tmp3 + tmp10 + tmp13) >> (Bits - Pass1Bits);
                }

                // Pass 2: process columns.
                // We remove pass1Bits scaling, but leave results scaled up by an overall factor of 8.
                for (int x = 0; x < 8; x++)
                {
                    int tmp0 = block[x] + block[56 + x];
                    int tmp1 = block[8 + x] + block[48 + x];
                    int tmp2 = block[16 + x] + block[40 + x];
                    int tmp3 = block[24 + x] + block[32 + x];

                    int tmp10 = tmp0 + tmp3 + (1 << (Pass1Bits - 1));
                    int tmp12 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp13 = tmp1 - tmp2;

                    tmp0 = block[x] - block[56 + x];
                    tmp1 = block[8 + x] - block[48 + x];
                    tmp2 = block[16 + x] - block[40 + x];
                    tmp3 = block[24 + x] - block[32 + x];

                    block[x] = (tmp10 + tmp11) >> Pass1Bits;
                    block[32 + x] = (tmp10 - tmp11) >> Pass1Bits;

                    int z1 = (tmp12 + tmp13) * fix_0_541196100;
                    z1 += 1 << (Bits + Pass1Bits - 1);
                    block[16 + x] = (z1 + (tmp12 * fix_0_765366865)) >> (Bits + Pass1Bits);
                    block[48 + x] = (z1 - (tmp13 * fix_1_847759065)) >> (Bits + Pass1Bits);

                    tmp10 = tmp0 + tmp3;
                    tmp11 = tmp1 + tmp2;
                    tmp12 = tmp0 + tmp2;
                    tmp13 = tmp1 + tmp3;
                    z1 = (tmp12 + tmp13) * fix_1_175875602;
                    z1 += 1 << (Bits + Pass1Bits - 1);
                    tmp0 = tmp0 * fix_1_501321110;
                    tmp1 = tmp1 * fix_3_072711026;
                    tmp2 = tmp2 * fix_2_053119869;
                    tmp3 = tmp3 * fix_0_298631336;
                    tmp10 = tmp10 * -fix_0_899976223;
                    tmp11 = tmp11 * -fix_2_562915447;
                    tmp12 = tmp12 * -fix_0_390180644;
                    tmp13 = tmp13 * -fix_1_961570560;

                    tmp12 += z1;
                    tmp13 += z1;
                    block[8 + x] = (tmp0 + tmp10 + tmp12) >> (Bits + Pass1Bits);
                    block[24 + x] = (tmp1 + tmp11 + tmp13) >> (Bits + Pass1Bits);
                    block[40 + x] = (tmp2 + tmp11 + tmp12) >> (Bits + Pass1Bits);
                    block[56 + x] = (tmp3 + tmp10 + tmp13) >> (Bits + Pass1Bits);
                }

            }
            private const int w1 = 2841; // 2048*sqrt(2)*cos(1*pi/16)
            private const int w2 = 2676; // 2048*sqrt(2)*cos(2*pi/16)
            private const int w3 = 2408; // 2048*sqrt(2)*cos(3*pi/16)
            private const int w5 = 1609; // 2048*sqrt(2)*cos(5*pi/16)
            private const int w6 = 1108; // 2048*sqrt(2)*cos(6*pi/16)
            private const int w7 = 565;  // 2048*sqrt(2)*cos(7*pi/16)

            private const int w1pw7 = w1 + w7;
            private const int w1mw7 = w1 - w7;
            private const int w2pw6 = w2 + w6;
            private const int w2mw6 = w2 - w6;
            private const int w3pw5 = w3 + w5;
            private const int w3mw5 = w3 - w5;

            private const int r2 = 181; // 256/sqrt(2)

            /// <summary>
            /// Performs a 2-D Inverse Discrete Cosine Transformation.
            /// <para>
            /// The input coefficients should already have been multiplied by the
            /// appropriate quantization table. We use fixed-point computation, with the
            /// number of bits for the fractional component varying over the intermediate
            /// stages.
            /// </para>
            /// For more on the actual algorithm, see Z. Wang, "Fast algorithms for the
            /// discrete W transform and for the discrete Fourier transform", IEEE Trans. on
            /// ASSP, Vol. ASSP- 32, pp. 803-816, Aug. 1984.
            /// </summary>
            /// <param name="src">The source block of coefficients</param>
            public static void TransformIDCTInplace(MutableSpan<int> src)
            {
                // Horizontal 1-D IDCT.
                for (int y = 0; y < 8; y++)
                {
                    int y8 = y * 8;

                    // If all the AC components are zero, then the IDCT is trivial.
                    if (src[y8 + 1] == 0 && src[y8 + 2] == 0 && src[y8 + 3] == 0 &&
                        src[y8 + 4] == 0 && src[y8 + 5] == 0 && src[y8 + 6] == 0 && src[y8 + 7] == 0)
                    {
                        int dc = src[y8 + 0] << 3;
                        src[y8 + 0] = dc;
                        src[y8 + 1] = dc;
                        src[y8 + 2] = dc;
                        src[y8 + 3] = dc;
                        src[y8 + 4] = dc;
                        src[y8 + 5] = dc;
                        src[y8 + 6] = dc;
                        src[y8 + 7] = dc;
                        continue;
                    }

                    // Prescale.
                    int x0 = (src[y8 + 0] << 11) + 128;
                    int x1 = src[y8 + 4] << 11;
                    int x2 = src[y8 + 6];
                    int x3 = src[y8 + 2];
                    int x4 = src[y8 + 1];
                    int x5 = src[y8 + 7];
                    int x6 = src[y8 + 5];
                    int x7 = src[y8 + 3];

                    // Stage 1.
                    int x8 = w7 * (x4 + x5);
                    x4 = x8 + (w1mw7 * x4);
                    x5 = x8 - (w1pw7 * x5);
                    x8 = w3 * (x6 + x7);
                    x6 = x8 - (w3mw5 * x6);
                    x7 = x8 - (w3pw5 * x7);

                    // Stage 2.
                    x8 = x0 + x1;
                    x0 -= x1;
                    x1 = w6 * (x3 + x2);
                    x2 = x1 - (w2pw6 * x2);
                    x3 = x1 + (w2mw6 * x3);
                    x1 = x4 + x6;
                    x4 -= x6;
                    x6 = x5 + x7;
                    x5 -= x7;

                    // Stage 3.
                    x7 = x8 + x3;
                    x8 -= x3;
                    x3 = x0 + x2;
                    x0 -= x2;
                    x2 = ((r2 * (x4 + x5)) + 128) >> 8;
                    x4 = ((r2 * (x4 - x5)) + 128) >> 8;

                    // Stage 4.
                    src[y8 + 0] = (x7 + x1) >> 8;
                    src[y8 + 1] = (x3 + x2) >> 8;
                    src[y8 + 2] = (x0 + x4) >> 8;
                    src[y8 + 3] = (x8 + x6) >> 8;
                    src[y8 + 4] = (x8 - x6) >> 8;
                    src[y8 + 5] = (x0 - x4) >> 8;
                    src[y8 + 6] = (x3 - x2) >> 8;
                    src[y8 + 7] = (x7 - x1) >> 8;
                }

                // Vertical 1-D IDCT.
                for (int x = 0; x < 8; x++)
                {
                    // Similar to the horizontal 1-D IDCT case, if all the AC components are zero, then the IDCT is trivial.
                    // However, after performing the horizontal 1-D IDCT, there are typically non-zero AC components, so
                    // we do not bother to check for the all-zero case.

                    // Prescale.
                    int y0 = (src[x] << 8) + 8192;
                    int y1 = src[32 + x] << 8;
                    int y2 = src[48 + x];
                    int y3 = src[16 + x];
                    int y4 = src[8 + x];
                    int y5 = src[56 + x];
                    int y6 = src[40 + x];
                    int y7 = src[24 + x];

                    // Stage 1.
                    int y8 = (w7 * (y4 + y5)) + 4;
                    y4 = (y8 + (w1mw7 * y4)) >> 3;
                    y5 = (y8 - (w1pw7 * y5)) >> 3;
                    y8 = (w3 * (y6 + y7)) + 4;
                    y6 = (y8 - (w3mw5 * y6)) >> 3;
                    y7 = (y8 - (w3pw5 * y7)) >> 3;

                    // Stage 2.
                    y8 = y0 + y1;
                    y0 -= y1;
                    y1 = (w6 * (y3 + y2)) + 4;
                    y2 = (y1 - (w2pw6 * y2)) >> 3;
                    y3 = (y1 + (w2mw6 * y3)) >> 3;
                    y1 = y4 + y6;
                    y4 -= y6;
                    y6 = y5 + y7;
                    y5 -= y7;

                    // Stage 3.
                    y7 = y8 + y3;
                    y8 -= y3;
                    y3 = y0 + y2;
                    y0 -= y2;
                    y2 = ((r2 * (y4 + y5)) + 128) >> 8;
                    y4 = ((r2 * (y4 - y5)) + 128) >> 8;

                    // Stage 4.
                    src[x] = (y7 + y1) >> 14;
                    src[8 + x] = (y3 + y2) >> 14;
                    src[16 + x] = (y0 + y4) >> 14;
                    src[24 + x] = (y8 + y6) >> 14;
                    src[32 + x] = (y8 - y6) >> 14;
                    src[40 + x] = (y0 - y4) >> 14;
                    src[48 + x] = (y3 - y2) >> 14;
                    src[56 + x] = (y7 - y1) >> 14;
                }
            }
        }

        /// <summary>
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L200
        /// </summary>
        /// <param name="y"></param>
        /// <param name="x"></param>
        private static void iDCT1Dllm_32f(MutableSpan<float> y, MutableSpan<float> x)
        {
            float a0, a1, a2, a3, b0, b1, b2, b3;
            float z0, z1, z2, z3, z4;

            //float r0 = 1.414214f;
            float r1 = 1.387040f;
            float r2 = 1.306563f;
            float r3 = 1.175876f;
            //float r4 = 1.000000f;
            float r5 = 0.785695f;
            float r6 = 0.541196f;
            float r7 = 0.275899f;

            z0 = y[1] + y[7];
            z1 = y[3] + y[5];
            z2 = y[3] + y[7];
            z3 = y[1] + y[5];
            z4 = (z0 + z1) * r3;

            z0 = z0 * (-r3 + r7);
            z1 = z1 * (-r3 - r1);
            z2 = z2 * (-r3 - r5) + z4;
            z3 = z3 * (-r3 + r5) + z4;

            b3 = y[7] * (-r1 + r3 + r5 - r7) + z0 + z2;
            b2 = y[5] * (r1 + r3 - r5 + r7) + z1 + z3;
            b1 = y[3] * (r1 + r3 + r5 - r7) + z1 + z2;
            b0 = y[1] * (r1 + r3 - r5 - r7) + z0 + z3;

            z4 = (y[2] + y[6]) * r6;
            z0 = y[0] + y[4];
            z1 = y[0] - y[4];
            z2 = z4 - y[6] * (r2 + r6);
            z3 = z4 + y[2] * (r2 - r6);
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
        /// Applyies IDCT transformation on "s" copying transformed values to "d", using temporal block "temp"
        /// </summary>
        /// <param name="s"></param>
        /// <param name="d"></param>
        /// <param name="temp"></param>
        internal static void iDCT2D_llm(MutableSpan<float> s, MutableSpan<float> d, MutableSpan<float> temp)
        {
            int j;

            for (j = 0; j < 8; j++)
            {
                iDCT1Dllm_32f(s.Slice(j * 8), temp.Slice(j * 8));
            }

            Transpose8x8(temp, d);

            for (j = 0; j < 8; j++)
            {
                iDCT1Dllm_32f(d.Slice(j * 8), temp.Slice(j * 8));
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
        public static void fDCT2D8x4_32f(MutableSpan<float> s, MutableSpan<float> d)
        {
            Vector4 c0 = _mm_load_ps(s, 0);
            Vector4 c1 = _mm_load_ps(s, 56);
            Vector4 t0 = (c0 + c1);
            Vector4 t7 = (c0 - c1);

            c1 = _mm_load_ps(s, 48);
            c0 = _mm_load_ps(s, 8);
            Vector4 t1 = (c0 + c1);
            Vector4 t6 = (c0 - c1);

            c1 = _mm_load_ps(s, 40);
            c0 = _mm_load_ps(s, 16);
            Vector4 t2 = (c0 + c1);
            Vector4 t5 = (c0 - c1);

            c0 = _mm_load_ps(s, 24);
            c1 = _mm_load_ps(s, 32);
            Vector4 t3 = (c0 + c1);
            Vector4 t4 = (c0 - c1);

            /*
            c1 = x[0]; c2 = x[7]; t0 = c1 + c2; t7 = c1 - c2;
            c1 = x[1]; c2 = x[6]; t1 = c1 + c2; t6 = c1 - c2;
            c1 = x[2]; c2 = x[5]; t2 = c1 + c2; t5 = c1 - c2;
            c1 = x[3]; c2 = x[4]; t3 = c1 + c2; t4 = c1 - c2;
            */

            c0 = (t0 + t3);
            Vector4 c3 = (t0 - t3);
            c1 = (t1 + t2);
            Vector4 c2 = (t1 - t2);

            /*
	        c0 = t0 + t3; c3 = t0 - t3;
	        c1 = t1 + t2; c2 = t1 - t2;
	        */

            _mm_store_ps(d, 0, (c0 + c1));

            _mm_store_ps(d, 32, (c0 - c1));

            /*y[0] = c0 + c1;
            y[4] = c0 - c1;*/

            Vector4 w0 = new Vector4(0.541196f);
            Vector4 w1 = new Vector4(1.306563f);

            _mm_store_ps(d, 16, ((w0 * c2) + (w1 * c3)));

            _mm_store_ps(d, 48, ((w0 * c3) - (w1 * c2)));
            /*
            y[2] = c2 * r[6] + c3 * r[2];
            y[6] = c3 * r[6] - c2 * r[2];
            */

            w0 = new Vector4(1.175876f);
            w1 = new Vector4(0.785695f);
            c3 = ((w0 * t4) + (w1 * t7));
            c0 = ((w0 * t7) - (w1 * t4));
            /*
            c3 = t4 * r[3] + t7 * r[5];
            c0 = t7 * r[3] - t4 * r[5];
            */

            w0 = new Vector4(1.387040f);
            w1 = new Vector4(0.275899f);
            c2 = ((w0 * t5) + (w1 * t6));
            c1 = ((w0 * t6) - (w1 * t5));
            /*
	        c2 = t5 * r[1] + t6 * r[7];
	        c1 = t6 * r[1] - t5 * r[7];
	        */

            _mm_store_ps(d, 24, (c0 - c2));

            _mm_store_ps(d, 40, (c3 - c1));
            //y[5] = c3 - c1; y[3] = c0 - c2;

            Vector4 invsqrt2 = new Vector4(0.707107f);
            c0 = ((c0 + c2) * invsqrt2);
            c3 = ((c3 + c1) * invsqrt2);
            //c0 = (c0 + c2) * invsqrt2;
            //c3 = (c3 + c1) * invsqrt2;

            _mm_store_ps(d, 8, (c0 + c3));

            _mm_store_ps(d, 56, (c0 - c3));
            //y[1] = c0 + c3; y[7] = c0 - c3;

            /*for(i = 0;i < 8;i++)
            { 
            y[i] *= invsqrt2h; 
            }*/
        }

        public static void fDCT8x8_llm_sse(MutableSpan<float> s, MutableSpan<float> d, MutableSpan<float> temp)
        {
            Transpose8x8(s, temp);

            fDCT2D8x4_32f(temp, d);

            fDCT2D8x4_32f(temp.Slice(4), d.Slice(4));

            Transpose8x8(d, temp);

            fDCT2D8x4_32f(temp, d);

            fDCT2D8x4_32f(temp.Slice(4), d.Slice(4));

            Vector4 c = new Vector4(0.1250f);

            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//0
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//1
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//2
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//3
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//4
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//5
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//6
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//7
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//8
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//9
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//10
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//11
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//12
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//13
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//14
            _mm_store_ps(d, 0, (_mm_load_ps(d, 0) * c)); d.AddOffset(4);//15
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 _mm_load_ps(MutableSpan<float> src, int offset)
        {
            src = src.Slice(offset);
            return new Vector4(src[0], src[1], src[2], src[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _mm_store_ps(MutableSpan<float> dest, int offset, Vector4 src)
        {
            dest = dest.Slice(offset);
            dest[0] = src.X;
            dest[1] = src.Y;
            dest[2] = src.Z;
            dest[3] = src.W;
        }

        private static readonly Vector4 _1_175876 = new Vector4(1.175876f);

        private static readonly Vector4 _1_961571 = new Vector4(-1.961571f);

        private static readonly Vector4 _0_390181 = new Vector4(-0.390181f);

        private static readonly Vector4 _0_899976 = new Vector4(-0.899976f);

        private static readonly Vector4 _2_562915 = new Vector4(-2.562915f);

        private static readonly Vector4 _0_298631 = new Vector4(0.298631f);

        private static readonly Vector4 _2_053120 = new Vector4(2.053120f);

        private static readonly Vector4 _3_072711 = new Vector4(3.072711f);

        private static readonly Vector4 _1_501321 = new Vector4(1.501321f);

        private static readonly Vector4 _0_541196 = new Vector4(0.541196f);

        private static readonly Vector4 _1_847759 = new Vector4(-1.847759f);

        private static readonly Vector4 _0_765367 = new Vector4(0.765367f);

        /// <summary>
        /// Original:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L261
        /// Does a part of the IDCT job on the given parts of the blocks
        /// </summary>
        /// <param name="y"></param>
        /// <param name="x"></param>
        internal static void iDCT2D8x4_32f(MutableSpan<float> y, MutableSpan<float> x)
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

            Vector4 mz4 = ((mz0 + mz1) * _1_175876);
            //z0 = y[1] + y[7]; z1 = y[3] + y[5]; z2 = y[3] + y[7]; z3 = y[1] + y[5];
            //z4 = (z0 + z1) * r[3];

            mz2 = mz2 * _1_961571 + mz4;
            mz3 = mz3 * _0_390181 + mz4;
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

            Vector4 mb3 = my7 * _0_298631 + mz0 + mz2;
            Vector4 mb2 = my5 * _2_053120 + mz1 + mz3;
            Vector4 mb1 = my3 * _3_072711 + mz1 + mz2;
            Vector4 mb0 = my1 * _1_501321 + mz0 + mz3;

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

            mz2 = mz4 + my6 * _1_847759;
            mz3 = mz4 + my2 * _0_765367;

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

        /// <summary>
        /// Copies color values from block to the destination image buffer.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <param name="stride"></param>
        internal static unsafe void CopyColorsTo(ref Block8x8F block, MutableSpan<byte> buffer, int stride)
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

        internal static void fDCT1Dllm_32f(MutableSpan<float> x, MutableSpan<float> y)
        {
            float t0, t1, t2, t3, t4, t5, t6, t7;
            float c0, c1, c2, c3;
            float[] r = new float[8];

            //for(i = 0;i < 8;i++){ r[i] = (float)(cos((double)i / 16.0 * M_PI) * M_SQRT2); }
            r[0] = 1.414214f;
            r[1] = 1.387040f;
            r[2] = 1.306563f;
            r[3] = 1.175876f;
            r[4] = 1.000000f;
            r[5] = 0.785695f;
            r[6] = 0.541196f;
            r[7] = 0.275899f;

            const float invsqrt2 = 0.707107f; //(float)(1.0f / M_SQRT2);
            //const float invsqrt2h = 0.353554f; //invsqrt2*0.5f;

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
            y[2] = c2 * r[6] + c3 * r[2];
            y[6] = c3 * r[6] - c2 * r[2];

            c3 = t4 * r[3] + t7 * r[5];
            c0 = t7 * r[3] - t4 * r[5];
            c2 = t5 * r[1] + t6 * r[7];
            c1 = t6 * r[1] - t5 * r[7];

            y[5] = c3 - c1;
            y[3] = c0 - c2;
            c0 = (c0 + c2) * invsqrt2;
            c3 = (c3 + c1) * invsqrt2;
            y[1] = c0 + c3;
            y[7] = c0 - c3;
        }

        internal static void fDCT2D_llm(
            MutableSpan<float> s,
            MutableSpan<float> d,
            MutableSpan<float> temp,
            bool downscaleBy8 = false,
            bool offsetSourceByNeg128 = false)
        {
            MutableSpan<float> sWorker = offsetSourceByNeg128 ? s.AddScalarToAllValues(-128f) : s;
            
            for (int j = 0; j < 8; j++)
            {
                fDCT1Dllm_32f(sWorker.Slice(j * 8), temp.Slice(j * 8));
            }

            Transpose8x8(temp, d);

            for (int j = 0; j < 8; j++)
            {
                fDCT1Dllm_32f(d.Slice(j * 8), temp.Slice(j * 8));
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