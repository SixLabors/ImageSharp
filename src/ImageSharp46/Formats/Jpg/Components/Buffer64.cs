using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ImageSharp.Formats
{
    public struct Buffer64
    {
        public Vector4 V00;
        public Vector4 V01;
        public Vector4 V02;
        public Vector4 V03;

        public Vector4 V10;
        public Vector4 V11;
        public Vector4 V12;
        public Vector4 V13;

        public Vector4 V20;
        public Vector4 V21;
        public Vector4 V22;
        public Vector4 V23;

        public Vector4 V30;
        public Vector4 V31;
        public Vector4 V32;
        public Vector4 V33;


        public const int VectorCount = 16;
        public const int ScalarCount = VectorCount * 4;

        public unsafe void LoadFrom(Span<float> source)
        {
            fixed (Vector4* ptr = &V00)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    fp[i] = source[i];
                }
            }
        }

        public unsafe void CopyTo(Span<float> dest)
        {
            fixed (Vector4* ptr = &V00)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    dest[i] = fp[i];
                }
            }
        }

        internal unsafe void LoadFrom(Span<int> source)
        {
            fixed (Vector4* ptr = &V00)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    fp[i] = source[i];
                }
            }
        }

        internal unsafe void CopyTo(Span<int> dest)
        {
            fixed (Vector4* ptr = &V00)
            {
                float* fp = (float*)ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    dest[i] = (int) fp[i];
                }
            }
        }

        public unsafe void TransposeInplace()
        {
            fixed (Vector4* ptr = &V00)
            {
                float* data = (float*) ptr;

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
           
        }

        public unsafe void TranposeInto(ref Buffer64 destination)
        {
            fixed (Vector4* sPtr = &V00)
            {
                float* src = (float*)sPtr;

                fixed (Vector4* dPtr = &destination.V00)
                {
                    float* dest = (float*) dPtr;

                    for (int i = 0; i < 8; i++)
                    {
                        int i8 = i * 8;
                        for (int j = 0; j < 8; j++)
                        {
                            dest[j * 8 + i] = src[i8 + j];
                        }
                    }
                }
            }
        }

        //public struct Matrix
        //{
        //    public Matrix4x4 A, B, C, D;

        //    public void LoadFrom(ref Buffer64 b)
        //    {
        //        fixed (Vector4*)
        //    }
        //}

        public void TransposeIntoSafe(ref Buffer64 destination)
        {
            Matrix4x4 a;
            

        }

        private static readonly Vector4 _c = new Vector4(0.1250f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAllInplace(Vector4 s)
        {
            V00 *= s; V01 *= s; V02 *= s; V03 *= s;
            V10 *= s; V11 *= s; V12 *= s; V13 *= s;
            V20 *= s; V21 *= s; V22 *= s; V23 *= s;
            V30 *= s; V31 *= s; V32 *= s; V33 *= s;
        }

        // ReSharper disable once InconsistentNaming
        public void TransformIDCTInto(ref Buffer64 dest, ref Buffer64 temp)
        {
            TranposeInto(ref temp);
            temp.iDCT2D8x4_LeftPart(ref dest);
            temp.iDCT2D8x4_RightPart(ref dest);
            
            dest.TranposeInto(ref temp);
            
            temp.iDCT2D8x4_LeftPart(ref dest);
            temp.iDCT2D8x4_RightPart(ref dest);

            dest.MultiplyAllInplace(new Vector4(0.1250f));
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

        internal void iDCT2D8x4_LeftPart(ref Buffer64 d)
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
            
            Vector4 my1 = V02;
            Vector4 my7 = V32;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = V12;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = V22;
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

            Vector4 my2 = V10;
            Vector4 my6 = V30;
            mz4 = (my2 + my6) * _0_541196;
            Vector4 my0 = V00;
            Vector4 my4 = V20;
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
            
            d.V00 = my0 + mb0;
            d.V32 = my0 - mb0;
            d.V02 = my1 + mb1;
            d.V30 = my1 - mb1;
            d.V10 = my2 + mb2;
            d.V22 = my2 - mb2;
            d.V12 = my3 + mb3;
            d.V20 = my3 - mb3;
            /*
            x[0] = a0 + b0; x[7] = a0 - b0;
            x[1] = a1 + b1; x[6] = a1 - b1;
            x[2] = a2 + b2; x[5] = a2 - b2;
            x[3] = a3 + b3; x[4] = a3 - b3;
            for(i = 0;i < 8;i++){ x[i] *= 0.353554f; }
            */
        }


        internal void iDCT2D8x4_RightPart(ref Buffer64 d)
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

            Vector4 my1 = V03;
            Vector4 my7 = V33;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = V13;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = V23;
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

            Vector4 my2 = V11;
            Vector4 my6 = V31;
            mz4 = (my2 + my6) * _0_541196;
            Vector4 my0 = V01;
            Vector4 my4 = V21;
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

            d.V01 = my0 + mb0;
            d.V33 = my0 - mb0;
            d.V03 = my1 + mb1;
            d.V31 = my1 - mb1;
            d.V11 = my2 + mb2;
            d.V23 = my2 - mb2;
            d.V13 = my3 + mb3;
            d.V21 = my3 - mb3;
            /*
            x[0] = a0 + b0; x[7] = a0 - b0;
            x[1] = a1 + b1; x[6] = a1 - b1;
            x[2] = a2 + b2; x[5] = a2 - b2;
            x[3] = a3 + b3; x[4] = a3 - b3;
            for(i = 0;i < 8;i++){ x[i] *= 0.353554f; }
            */
        }

        public static void SuchIDCT(ref Block block)
        {
            Buffer64 source = new Buffer64();
            source.LoadFrom(block.Data);

            Buffer64 dest = new Buffer64();
            Buffer64 temp = new Buffer64();
            
            source.TransformIDCTInto(ref dest, ref temp);
            dest.CopyTo(block.Data);
        }
    }
}