using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace ImageSharp.Formats
{
    /// <summary>
    /// DCT code Ported from https://github.com/norishigefukushima/dct_simd
    /// </summary>
    internal partial struct Block8x8F
    {
        public Vector4 V0L;
        public Vector4 V0R;

        public Vector4 V1L;
        public Vector4 V1R;

        public Vector4 V2L;
        public Vector4 V2R;

        public Vector4 V3L;
        public Vector4 V3R;

        public Vector4 V4L;
        public Vector4 V4R;

        public Vector4 V5L;
        public Vector4 V5R;

        public Vector4 V6L;
        public Vector4 V6R;

        public Vector4 V7L;
        public Vector4 V7R;


        public const int VectorCount = 16;
        public const int ScalarCount = VectorCount*4;

        private static readonly ArrayPool<float> ScalarArrayPool = ArrayPool<float>.Create(ScalarCount, 50);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void LoadFrom(MutableSpan<float> source)
        {
            fixed (Vector4* ptr = &V0L)
            {
                Marshal.Copy(source.Data, source.Offset, (IntPtr) ptr, ScalarCount);
                //float* fp = (float*)ptr;
                //for (int i = 0; i < ScalarCount; i++)
                //{
                //    fp[i] = source[i];
                //}
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(MutableSpan<float> dest)
        {
            fixed (Vector4* ptr = &V0L)
            {
                Marshal.Copy((IntPtr) ptr, dest.Data, dest.Offset, ScalarCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(float[] dest)
        {
            fixed (Vector4* ptr = &V0L)
            {
                Marshal.Copy((IntPtr) ptr, dest, 0, ScalarCount);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void LoadFrom(Block8x8F* blockPtr, MutableSpan<float> source)
        {
            Marshal.Copy(source.Data, source.Offset, (IntPtr) blockPtr, ScalarCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTo(Block8x8F* blockPtr, MutableSpan<float> dest)
        {
            Marshal.Copy((IntPtr) blockPtr, dest.Data, dest.Offset, ScalarCount);
        }


        internal unsafe void LoadFrom(MutableSpan<int> source)
        {
            fixed (Vector4* ptr = &V0L)
            {
                float* fp = (float*) ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    fp[i] = source[i];
                }
            }
        }

        internal unsafe void CopyTo(MutableSpan<int> dest)
        {
            fixed (Vector4* ptr = &V0L)
            {
                float* fp = (float*) ptr;
                for (int i = 0; i < ScalarCount; i++)
                {
                    dest[i] = (int) fp[i];
                }
            }
        }

        public unsafe void TransposeInplace()
        {
            fixed (Vector4* ptr = &V0L)
            {
                float* data = (float*) ptr;

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

        }

        /// <summary>
        /// Reference implementation we can benchmark against
        /// </summary>
        internal unsafe void TransposeInto_PinningImpl(ref Block8x8F destination)
        {
            fixed (Vector4* sPtr = &V0L)
            {
                float* src = (float*) sPtr;

                fixed (Vector4* dPtr = &destination.V0L)
                {
                    float* dest = (float*) dPtr;

                    for (int i = 0; i < 8; i++)
                    {
                        int i8 = i*8;
                        for (int j = 0; j < 8; j++)
                        {
                            dest[j*8 + i] = src[i8 + j];
                        }
                    }
                }
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void TransposeInto(Block8x8F* sourcePtr, Block8x8F* destPtr)
        {
            float* src = (float*) sourcePtr;
            float* dest = (float*) destPtr;

            for (int i = 0; i < 8; i++)
            {
                int i8 = i*8;
                for (int j = 0; j < 8; j++)
                {
                    dest[j*8 + i] = src[i8 + j];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAllInplace(Vector4 s)
        {
            V0L *= s;
            V0R *= s;
            V1L *= s;
            V1R *= s;
            V2L *= s;
            V2R *= s;
            V3L *= s;
            V3R *= s;
            V4L *= s;
            V4R *= s;
            V5L *= s;
            V5R *= s;
            V6L *= s;
            V6R *= s;
            V7L *= s;
            V7R *= s;
        }

        // ReSharper disable once InconsistentNaming
        public void IDCTInto(ref Block8x8F dest, ref Block8x8F temp)
        {
            TransposeInto(ref temp);
            temp.iDCT2D8x4_LeftPart(ref dest);
            temp.iDCT2D8x4_RightPart(ref dest);

            dest.TransposeInto(ref temp);

            temp.iDCT2D8x4_LeftPart(ref dest);
            temp.iDCT2D8x4_RightPart(ref dest);

            dest.MultiplyAllInplace(_0_125);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IDCTInplace()
        {
            Block8x8F result = new Block8x8F();
            Block8x8F temp = new Block8x8F();
            IDCTInto(ref result, ref temp);
            this = result;
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
        private static readonly Vector4 _0_125 = new Vector4(0.1250f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void iDCT2D8x4_LeftPart(ref Block8x8F d)
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

            Vector4 my1 = V1L;
            Vector4 my7 = V7L;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = V3L;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = V5L;
            Vector4 mz1 = my3 + my5;
            Vector4 mz3 = my1 + my5;

            Vector4 mz4 = ((mz0 + mz1)*_1_175876);
            //z0 = y[1] + y[7]; z1 = y[3] + y[5]; z2 = y[3] + y[7]; z3 = y[1] + y[5];
            //z4 = (z0 + z1) * r[3];

            mz2 = mz2*_1_961571 + mz4;
            mz3 = mz3*_0_390181 + mz4;
            mz0 = mz0*_0_899976;
            mz1 = mz1*_2_562915;

            /*
            -0.899976
            -2.562915
            -1.961571
            -0.390181
            z0 = z0 * (-r[3] + r[7]);
            z1 = z1 * (-r[3] - r[1]);
            z2 = z2 * (-r[3] - r[5]) + z4;
            z3 = z3 * (-r[3] + r[5]) + z4;*/


            Vector4 mb3 = my7*_0_298631 + mz0 + mz2;
            Vector4 mb2 = my5*_2_053120 + mz1 + mz3;
            Vector4 mb1 = my3*_3_072711 + mz1 + mz2;
            Vector4 mb0 = my1*_1_501321 + mz0 + mz3;

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

            Vector4 my2 = V2L;
            Vector4 my6 = V6L;
            mz4 = (my2 + my6)*_0_541196;
            Vector4 my0 = V0L;
            Vector4 my4 = V4L;
            mz0 = my0 + my4;
            mz1 = my0 - my4;

            mz2 = mz4 + my6*_1_847759;
            mz3 = mz4 + my2*_0_765367;

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

            d.V0L = my0 + mb0;
            d.V7L = my0 - mb0;
            d.V1L = my1 + mb1;
            d.V6L = my1 - mb1;
            d.V2L = my2 + mb2;
            d.V5L = my2 - mb2;
            d.V3L = my3 + mb3;
            d.V4L = my3 - mb3;
            /*
            x[0] = a0 + b0; x[7] = a0 - b0;
            x[1] = a1 + b1; x[6] = a1 - b1;
            x[2] = a2 + b2; x[5] = a2 - b2;
            x[3] = a3 + b3; x[4] = a3 - b3;
            for(i = 0;i < 8;i++){ x[i] *= 0.353554f; }
            */
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void iDCT2D8x4_RightPart(ref Block8x8F d)
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

            Vector4 my1 = V1R;
            Vector4 my7 = V7R;
            Vector4 mz0 = my1 + my7;

            Vector4 my3 = V3R;
            Vector4 mz2 = my3 + my7;
            Vector4 my5 = V5R;
            Vector4 mz1 = my3 + my5;
            Vector4 mz3 = my1 + my5;

            Vector4 mz4 = ((mz0 + mz1)*_1_175876);
            //z0 = y[1] + y[7]; z1 = y[3] + y[5]; z2 = y[3] + y[7]; z3 = y[1] + y[5];
            //z4 = (z0 + z1) * r[3];

            mz2 = mz2*_1_961571 + mz4;
            mz3 = mz3*_0_390181 + mz4;
            mz0 = mz0*_0_899976;
            mz1 = mz1*_2_562915;

            /*
            -0.899976
            -2.562915
            -1.961571
            -0.390181
            z0 = z0 * (-r[3] + r[7]);
            z1 = z1 * (-r[3] - r[1]);
            z2 = z2 * (-r[3] - r[5]) + z4;
            z3 = z3 * (-r[3] + r[5]) + z4;*/


            Vector4 mb3 = my7*_0_298631 + mz0 + mz2;
            Vector4 mb2 = my5*_2_053120 + mz1 + mz3;
            Vector4 mb1 = my3*_3_072711 + mz1 + mz2;
            Vector4 mb0 = my1*_1_501321 + mz0 + mz3;

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

            Vector4 my2 = V2R;
            Vector4 my6 = V6R;
            mz4 = (my2 + my6)*_0_541196;
            Vector4 my0 = V0R;
            Vector4 my4 = V4R;
            mz0 = my0 + my4;
            mz1 = my0 - my4;

            mz2 = mz4 + my6*_1_847759;
            mz3 = mz4 + my2*_0_765367;

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

            d.V0R = my0 + mb0;
            d.V7R = my0 - mb0;
            d.V1R = my1 + mb1;
            d.V6R = my1 - mb1;
            d.V2R = my2 + mb2;
            d.V5R = my2 - mb2;
            d.V3R = my3 + mb3;
            d.V4R = my3 - mb3;
            /*
            x[0] = a0 + b0; x[7] = a0 - b0;
            x[1] = a1 + b1; x[6] = a1 - b1;
            x[2] = a2 + b2; x[5] = a2 - b2;
            x[3] = a3 + b3; x[4] = a3 - b3;
            for(i = 0;i < 8;i++){ x[i] *= 0.353554f; }
            */
        }

        internal static void SuchIDCT(ref Block block)
        {
            Block8x8F source = new Block8x8F();
            source.LoadFrom(block.Data);

            Block8x8F dest = new Block8x8F();
            Block8x8F temp = new Block8x8F();

            source.IDCTInto(ref dest, ref temp);
            dest.CopyTo(block.Data);
        }

        internal static void SuchIDCT(ref BlockF block)
        {
            Block8x8F source = new Block8x8F();
            source.LoadFrom(block.Data);

            Block8x8F dest = new Block8x8F();
            Block8x8F temp = new Block8x8F();

            source.IDCTInto(ref dest, ref temp);
            dest.CopyTo(block.Data);
        }

        public unsafe float this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (Block8x8F* p = &this)
                {
                    float* fp = (float*) p;
                    return fp[idx];
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                fixed (Block8x8F* p = &this)
                {
                    float* fp = (float*) p;
                    fp[idx] = value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe float GetScalarAt(Block8x8F* blockPtr, int idx)
        {
            float* fp = (float*) blockPtr;
            return fp[idx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void SetScalarAt(Block8x8F* blockPtr, int idx, float value)
        {
            float* fp = (float*) blockPtr;
            fp[idx] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear()
        {
            this = new Block8x8F(); // LOL C# Plz!
        }

        internal void LoadFrom(ref BlockF legacyBlock)
        {
            LoadFrom(legacyBlock.Data);
        }

        internal void CopyTo(ref BlockF legacyBlock)
        {
            CopyTo(legacyBlock.Data);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToColorByte(float c)
        {
            if (c < -128)
            {
                return 0;
            }
            else if (c > 127)
            {
                return 255;
            }
            else
            {
                c += 128;
                return (byte) c;
            }
        }



        internal unsafe void CopyColorsTo(MutableSpan<byte> buffer, int stride)
        {
            fixed (Block8x8F* p = &this)
            {
                float* b = (float*) p;

                for (int y = 0; y < 8; y++)
                {
                    int y8 = y*8;
                    int yStride = y*stride;

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

                        buffer[yStride + x] = (byte) c;
                    }
                }
            }

        }
        
        private static readonly Vector4 CMin4 = new Vector4(-128f);
        private static readonly Vector4 CMax4 = new Vector4(127f);
        private static readonly Vector4 COff4 = new Vector4(128f);
        
        /// <summary>
        /// Level shift by +128, clip to [0, 255], and write to buffer. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void CopyColorsTo(
            MutableSpan<byte> buffer, 
            int stride,
            Block8x8F* temp)
        {
            ColorifyInto(ref *temp);

            float* src = (float*) temp;
            for (int i = 0; i < 8; i++)
            {
                buffer[0] = (byte) src[0];
                buffer[1] = (byte) src[1];
                buffer[2] = (byte) src[2];
                buffer[3] = (byte) src[3];
                buffer[4] = (byte) src[4];
                buffer[5] = (byte) src[5];
                buffer[6] = (byte) src[6];
                buffer[7] = (byte) src[7];
                buffer.AddOffset(stride);
                src += 8;
            }
        }

        
    }
}