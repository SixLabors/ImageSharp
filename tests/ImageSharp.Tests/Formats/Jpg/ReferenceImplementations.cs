// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests.Formats.Jpg
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Formats;

    /// <summary>
    /// This class contains simplified (unefficient) reference implementations to produce verification data for unit tests
    /// DCT code Ported from https://github.com/norishigefukushima/dct_simd
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

        /// <summary>
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L200
        /// </summary>
        /// <param name="y"></param>
        /// <param name="x"></param>
        private static void iDCT1Dllm_32f(MutableSpan<float> y, MutableSpan<float> x)
        {
            float a0, a1, a2, a3, b0, b1, b2, b3;
            float z0, z1, z2, z3, z4;

            float r1 = 1.387040f;
            float r2 = 1.306563f;
            float r3 = 1.175876f;
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
    }
}