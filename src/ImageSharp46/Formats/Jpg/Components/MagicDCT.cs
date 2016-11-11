using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ImageSharp.Formats
{
    public struct Span<T>
        where T : struct
    {
        public T[] Data;
        public int Offset;

        public Span(int size, int offset = 0)
        {
            Data = new T[size];
            Offset = offset;
        }

        public Span(T[] data, int offset = 0)
        {
            Data = data;
            Offset = offset;
        }

        public T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return Data[idx + Offset]; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)] set { Data[idx + Offset] = value; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Slice(int offset)
        {
            return new Span<T>(Data, Offset + offset);
        }

        public static implicit operator Span<T>(T[] data) => new Span<T>(data, 0);

        private static readonly ArrayPool<T> Pool = ArrayPool<T>.Create(128, 10);

        public static Span<T> RentFromPool(int size, int offset = 0)
        {
            return new Span<T>(Pool.Rent(size), offset);
        }

        public void ReturnToPool()
        {
            Pool.Return(Data, true);
            Data = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int offset)
        {
            Offset += offset;
        }
    }

    public static class MagicDCT
    {
        private static readonly ArrayPool<float> FloatArrayPool = ArrayPool<float>.Create(Block.BlockSize, 50);
       

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 Load(Span<float> src, int x, int y)
        {
            int b0 = y*8 + x;
            y++;
            int b1 = y*8 + x;
            y++;
            int b2 = y*8 + x;
            y++;
            int b3 = y*8 + x;

            return new Matrix4x4(
                src[b0], src[b0 + 1], src[b0 + 2], src[b0 + 3],
                src[b1], src[b1 + 1], src[b1 + 2], src[b1 + 3],
                src[b2], src[b2 + 1], src[b2 + 2], src[b2 + 3],
                src[b3], src[b3 + 1], src[b3 + 2], src[b3 + 3]
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Matrix4x4 s, Span<float> d, int x, int y)
        {
            int b0 = y*8 + x;
            y++;
            int b1 = y*8 + x;
            y++;
            int b2 = y*8 + x;
            y++;
            int b3 = y*8 + x;

            d[b0] = s.M11;
            d[b0 + 1] = s.M12;
            d[b0 + 2] = s.M13;
            d[b0 + 3] = s.M14;
            d[b1] = s.M21;
            d[b1 + 1] = s.M22;
            d[b1 + 2] = s.M23;
            d[b1 + 3] = s.M24;
            d[b2] = s.M31;
            d[b2 + 1] = s.M32;
            d[b2 + 2] = s.M33;
            d[b2 + 3] = s.M34;
            d[b3] = s.M41;
            d[b3 + 1] = s.M42;
            d[b3 + 2] = s.M43;
            d[b3 + 3] = s.M44;
        }

        public static void Transpose8x8_SSE_Slow(Span<float> data)
        {
            Matrix4x4 a11 = Load(data, 0, 0);
            Matrix4x4 a12 = Load(data, 4, 0);
            Matrix4x4 a21 = Load(data, 0, 4);
            Matrix4x4 a22 = Load(data, 4, 4);

            a11 = Matrix4x4.Transpose(a11);
            a12 = Matrix4x4.Transpose(a12);
            a21 = Matrix4x4.Transpose(a21);
            a22 = Matrix4x4.Transpose(a22);

            Store(a11, data, 0, 0);
            Store(a21, data, 4, 0);
            Store(a12, data, 0, 4);
            Store(a22, data, 4, 4);
        }

        public static void Transpose8x8_SSE_Slow(Span<float> src, Span<float> dest)
        {
            Matrix4x4 a11 = Load(src, 0, 0);
            Matrix4x4 a12 = Load(src, 4, 0);
            Matrix4x4 a21 = Load(src, 0, 4);
            Matrix4x4 a22 = Load(src, 4, 4);

            a11 = Matrix4x4.Transpose(a11);
            a12 = Matrix4x4.Transpose(a12);
            a21 = Matrix4x4.Transpose(a21);
            a22 = Matrix4x4.Transpose(a22);

            Store(a11, dest, 0, 0);
            Store(a21, dest, 4, 0);
            Store(a12, dest, 0, 4);
            Store(a22, dest, 4, 4);
        }

        public static void Transpose8x8(Span<float> data)
        {
            for (int i = 1; i < 8; i++)
            {
                int i8 = i*8;
                for (int j = 0; j < i; j++)
                {
                    float tmp = data[i8 + j];
                    data[i8 + j] = data[j*8 + i];
                    data[j*8 + i] = tmp;
                }
            }
        }

        public static void Transpose8x8(Span<float> src, Span<float> dest)
        {
            for (int i = 0; i < 8; i++)
            {
                int i8 = i*8;
                for (int j = 0; j < 8; j++)
                {
                    dest[j*8 + i] = src[i8 + j];
                }
            }

            //Matrix4x4 a11 = Load(src, 0, 0);
            //Matrix4x4 a12 = Load(src, 4, 0);
            //Matrix4x4 a21 = Load(src, 0, 4);
            //Matrix4x4 a22 = Load(src, 4, 4);

            //a11 = Matrix4x4.Transpose(a11);
            //a12 = Matrix4x4.Transpose(a12);
            //a21 = Matrix4x4.Transpose(a21);
            //a22 = Matrix4x4.Transpose(a22);

            //Store(a11, dest, 0, 0);
            //Store(a21, dest, 4, 0);
            //Store(a12, dest, 0, 4);
            //Store(a22, dest, 4, 4);
        }

        public static void iDCT1Dllm_32f(Span<float> y, Span<float> x)
        {
            float a0, a1, a2, a3, b0, b1, b2, b3;
            float z0, z1, z2, z3, z4;

            float r0 = 1.414214f;
            float r1 = 1.387040f;
            float r2 = 1.306563f;
            float r3 = 1.175876f;
            float r4 = 1.000000f;
            float r5 = 0.785695f;
            float r6 = 0.541196f;
            float r7 = 0.275899f;

            z0 = y[1] + y[7];
            z1 = y[3] + y[5];
            z2 = y[3] + y[7];
            z3 = y[1] + y[5];
            z4 = (z0 + z1)*r3;

            z0 = z0*(-r3 + r7);
            z1 = z1*(-r3 - r1);
            z2 = z2*(-r3 - r5) + z4;
            z3 = z3*(-r3 + r5) + z4;

            b3 = y[7]*(-r1 + r3 + r5 - r7) + z0 + z2;
            b2 = y[5]*(r1 + r3 - r5 + r7) + z1 + z3;
            b1 = y[3]*(r1 + r3 + r5 - r7) + z1 + z2;
            b0 = y[1]*(r1 + r3 - r5 - r7) + z0 + z3;

            z4 = (y[2] + y[6])*r6;
            z0 = y[0] + y[4];
            z1 = y[0] - y[4];
            z2 = z4 - y[6]*(r2 + r6);
            z3 = z4 + y[2]*(r2 - r6);
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

        public static void iDCT2D_llm(Span<float> s, Span<float> d, Span<float> temp)
        {
            int j;

            for (j = 0; j < 8; j++)
            {
                iDCT1Dllm_32f(s.Slice(j*8), temp.Slice(j*8));
            }

            Transpose8x8(temp, d);

            for (j = 0; j < 8; j++)
            {
                iDCT1Dllm_32f(d.Slice(j*8), temp.Slice(j*8));
            }

            Transpose8x8(temp, d);

            for (j = 0; j < 64; j++)
            {
                d[j] *= 0.125f;
            }
        }

        public static void IDCT(ref Block block)
        {
            Span<float> src = Span<float>.RentFromPool(64);

            for (int i = 0; i < 64; i++)
            {
                src[i] = block[i];
            }

            Span<float> dest = Span<float>.RentFromPool(64);
            Span<float> temp  = Span<float>.RentFromPool(64);
            
            //iDCT2D_llm(src, dest, temp);
            //iDCT8x8GT(src, dest);
            iDCT8x8_llm_sse(src, dest, temp);

            for (int i = 0; i < 64; i++)
            {
                block[i] = (int) (dest[i] + 0.5f);
            }

            src.ReturnToPool();
            dest.ReturnToPool();
            temp.ReturnToPool();
        }

        public static void iDCT8x8GT(Span<float> s, Span<float> d)
        {
            idct81d_sse_GT(s, d);

            Transpose8x8(d);

            idct81d_sse_GT(d, d);

            Transpose8x8(d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 _mm_load_ps(Span<float> src, int offset)
        {
            src = src.Slice(offset);
            return new Vector4(src[0], src[1], src[2], src[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 _mm_load_ps(Span<float> src)
        {
            return new Vector4(src[0], src[1], src[2], src[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _mm_store_ps(Span<float> dest, int offset, Vector4 src)
        {
            dest = dest.Slice(offset);
            dest[0] = src.X;
            dest[1] = src.Y;
            dest[2] = src.Z;
            dest[3] = src.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _mm_store_ps(Span<float> dest, Vector4 src)
        {
            dest[0] = src.X;
            dest[1] = src.Y;
            dest[2] = src.Z;
            dest[3] = src.W;
        }


        public static void idct81d_sse_GT(Span<float> src, Span<float> dst)
        {
            Vector4 c1414 = new Vector4(1.4142135623731f);
            Vector4 c0250 = new Vector4(0.25f);
            Vector4 c0353 = new Vector4(0.353553390593274f);
            Vector4 c0707 = new Vector4(0.707106781186547f);

            for (int i = 0; i < 2; i++)
            {
                Vector4 ms0 = _mm_load_ps(src, 0);
                Vector4 ms1 = _mm_load_ps(src, 8);
                Vector4 ms2 = _mm_load_ps(src, 16);
                Vector4 ms3 = _mm_load_ps(src, 24);
                Vector4 ms4 = _mm_load_ps(src, 32);
                Vector4 ms5 = _mm_load_ps(src, 40);
                Vector4 ms6 = _mm_load_ps(src, 48);
                Vector4 ms7 = _mm_load_ps(src, 56);

                Vector4 mx00 = (c1414*ms0);

                Vector4 mx01 = ((new Vector4(1.38703984532215f)*ms1) + (new Vector4(0.275899379282943f)*ms7));
                Vector4 mx02 = ((new Vector4(1.30656296487638f)*ms2) + (new Vector4(0.541196100146197f)*ms6));
                Vector4 mx03 = ((new Vector4(1.17587560241936f)*ms3) + (new Vector4(0.785694958387102f)*ms5));

                Vector4 mx04 = (c1414*ms4);

                Vector4 mx05 = ((new Vector4(-0.785694958387102f)*ms3) + (new Vector4(+1.17587560241936f)*ms5));
                Vector4 mx06 = ((new Vector4(0.541196100146197f)*ms2) + (new Vector4(-1.30656296487638f)*ms6));
                Vector4 mx07 = ((new Vector4(-0.275899379282943f)*ms1) + (new Vector4(1.38703984532215f)*ms7));
                Vector4 mx09 = (mx00 + mx04);
                Vector4 mx0a = (mx01 + mx03);

                Vector4 mx0b = (c1414*mx02);

                Vector4 mx0c = (mx00 - mx04);
                Vector4 mx0d = (mx01 - mx03);

                Vector4 mx0e = (c0353*(mx09 - mx0b));
                Vector4 mx0f = (c0353*(mx0c - mx0d));
                Vector4 mx10 = (c0353*(mx0c - mx0d));
                Vector4 mx11 = (c1414*mx06);

                Vector4 mx12 = (mx05 + mx07);

                Vector4 mx13 = (mx05 - mx07);

                Vector4 mx14 = (c0353*(mx11 + mx12));
                Vector4 mx15 = (c0353*(mx11 - mx12));
                Vector4 mx16 = (new Vector4(0.5f)*mx13);

                _mm_store_ps(dst, 0, ((c0250 + (mx09 + mx0b))*(c0353*mx0a)));
                _mm_store_ps(dst, 8, (c0707*(mx0f + mx15)));
                _mm_store_ps(dst, 16, (c0707*(mx0f - mx15)));
                _mm_store_ps(dst, 24, (c0707*(mx0e + mx16)));
                _mm_store_ps(dst, 32, (c0707*(mx0e - mx16)));
                _mm_store_ps(dst, 40, (c0707*(mx10 - mx14)));
                _mm_store_ps(dst, 48, (c0707*(mx10 + mx14)));

                _mm_store_ps(dst, 56, ((c0250*(mx09 + mx0b)) - (c0353*mx0a)));

                dst = dst.Slice(4);
                src = src.Slice(4);
            }
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

        public static void iDCT2D8x4_32f(Span<float> y, Span<float> x)
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
            
            Vector4 mz4 = ((mz0 + mz1)* _1_175876);
            //z0 = y[1] + y[7]; z1 = y[3] + y[5]; z2 = y[3] + y[7]; z3 = y[1] + y[5];
            //z4 = (z0 + z1) * r[3];
            
            mz2 = mz2* _1_961571 + mz4;
            mz3 = mz3* _0_390181 + mz4;
            mz0 = mz0* _0_899976;
            mz1 = mz1* _2_562915;
            
            /*
            -0.899976
            -2.562915
            -1.961571
            -0.390181
            z0 = z0 * (-r[3] + r[7]);
            z1 = z1 * (-r[3] - r[1]);
            z2 = z2 * (-r[3] - r[5]) + z4;
            z3 = z3 * (-r[3] + r[5]) + z4;*/

            
            Vector4 mb3 = my7* _0_298631 + mz0 + mz2;
            Vector4 mb2 = my5* _2_053120 + mz1 + mz3;
            Vector4 mb1 = my3* _3_072711 + mz1 + mz2;
            Vector4 mb0 = my1* _1_501321 + mz0 + mz3;

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
            mz4 = (my2 + my6)* _0_541196;
            Vector4 my0 = _mm_load_ps(y, 0);
            Vector4 my4 = _mm_load_ps(y, 32);
            mz0 = my0 + my4;
            mz1 = my0 - my4;

            mz2 = mz4 + my6* _1_847759;
            mz3 = mz4 + my2* _0_765367;

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

        public static void iDCT8x8_llm_sse(Span<float> s, Span<float> d, Span<float> temp)
        {
            Transpose8x8(s, temp);
            iDCT2D8x4_32f(temp, d);

            iDCT2D8x4_32f(temp.Slice(4), d.Slice(4));
            
            Transpose8x8(d, temp);

            iDCT2D8x4_32f(temp, d);

            iDCT2D8x4_32f(temp.Slice(4), d.Slice(4));

            Vector4 c = new Vector4(0.1250f);

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//0

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//1

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//2

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//3

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//4

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//5

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//6

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//7

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//8

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//9

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//10

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//11

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//12

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//13

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//14

            _mm_store_ps(d, (_mm_load_ps(d)*c));d.AddOffset(4);//15
        }
}
}