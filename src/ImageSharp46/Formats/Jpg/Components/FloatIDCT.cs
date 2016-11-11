using System;
using System.Buffers;

namespace ImageSharp.Formats
{
    internal class FloatIDCT
    {
        //private float[] _temp = new float[64];

        // Cosine matrix and transposed cosine matrix
        private static readonly float[] c = buildC();
        private static readonly float[] cT = buildCT();

        internal FloatIDCT()
        {
#if DYNAMIC_IDCT
            dynamicIDCT = dynamicIDCT ?? EmitIDCT();
#endif
        }

        /// <summary>
        /// Precomputes cosine terms in A.3.3 of 
        /// http://www.w3.org/Graphics/JPEG/itu-t81.pdf
        /// 
        /// Closely follows the term precomputation in the
        /// Java Advanced Imaging library.
        /// </summary>
        private static float[] buildC()
        {
            float[] c = new float[64];

            for (int i = 0; i < 8; i++) // i == u or v
            {
                for (int j = 0; j < 8; j++) // j == x or y
                {
                    c[i*8 + j] = i == 0 ?
                        0.353553391f : /* 1 / SQRT(8) */
                        (float)(0.5 * Math.Cos(((2.0 * j + 1) * i * Math.PI) / 16.0));
                }
            }

            return c;
        }
        private static float[] buildCT()
        {
            // Transpose i,k <-- j,i
            float[] cT = new float[64];
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    cT[j * 8 + i] = c[i * 8 + j];
            return cT;
        }

        public static void SetValueClipped(byte[,] arr, int i, int j, float val)
        {
            // Clip into the 0...255 range & round
            arr[i, j] = val < 0 ? (byte)0
                : val > 255 ? (byte)255
                    : (byte)(val + 0.5);
        }

        public static void Transform(ref Block block) => FastIDCT(block.Data);

        /// See figure A.3.3 IDCT (informative) on A-5.
        /// http://www.w3.org/Graphics/JPEG/itu-t81.pdf
        public static void FastIDCT(int[] output)
        {
            //byte[,] output = new byte[8, 8];
            //int[] output = new int[64];

            float[] _temp = ArrayPool<float>.Shared.Rent(64);

            float[] input = ArrayPool<float>.Shared.Rent(64);

            for (int i = 0; i < output.Length; i++)
            {
                input[i] = output[i];
            }

            float temp, val = 0;
            int idx = 0;
            for (int i = 0; i < 8; i++)
            {
                int i8 = i * 8;
                for (int j = 0; j < 8; j++)
                {
                    val = 0;

                    for (int k = 0; k < 8; k++)
                    {
                        val += input[i8 + k] * c[k*8 + j];
                    }

                    _temp[idx++] = val;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                int i8 = i*8;
                for (int j = 0; j < 8; j++)
                {
                    temp = 128f;

                    for (int k = 0; k < 8; k++)
                    {
                        temp += cT[i*8 + k] * _temp[k * 8 + j];
                    }

                    if (temp < 0) output[i8 + j] = 0;
                    else if (temp > 255) output[i8+ j] = 255;
                    else output[i8 + j] = (int)(temp + 0.5); // Implements rounding
                }
            }
            
            ArrayPool<float>.Shared.Return(input, true);
            ArrayPool<float>.Shared.Return(_temp, true);
        }



#if DYNAMIC_IDCT

/// <summary>
/// Generates a pure-IL nonbranching stream of instructions
/// that perform the inverse DCT.  Relies on helper function
/// SetValueClipped.
/// </summary>
/// <returns>A delegate to the DynamicMethod</returns>
        private static IDCTFunc EmitIDCT()
        {
            Type[] args = { typeof(float[]), typeof(float[]), typeof(byte[,]) };

            DynamicMethod idctMethod = new DynamicMethod("dynamicIDCT",
                null,        // no return type
                args); // input arrays

            ILGenerator il = idctMethod.GetILGenerator();

            int idx = 0;

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    il.Emit(OpCodes.Ldarg_1);                           // 1  {temp}
                    il.Emit(OpCodes.Ldc_I4_S, (short)idx++);            // 3  {temp, idx}

                    for (int k = 0; k < 8; k++)
                    {
                        il.Emit(OpCodes.Ldarg_0);                       // {in} 
                        il.Emit(OpCodes.Ldc_I4_S, (short)(i * 8 + k));  // {in,idx}
                        il.Emit(OpCodes.Ldelem_R4);                     // {in[idx]}
                        il.Emit(OpCodes.Ldc_R4, c[k, j]);               // {in[idx],c[k,j]}
                        il.Emit(OpCodes.Mul);                           // {in[idx]*c[k,j]}
                        if (k != 0) il.Emit(OpCodes.Add);
                    }

                    il.Emit(OpCodes.Stelem_R4);                         // {}
                }
            }

            var meth = typeof(DCT).GetMethod("SetValueClipped",
                BindingFlags.Static | BindingFlags.Public, null,
                CallingConventions.Standard,
                new Type[] { 
                    typeof(byte[,]),    // arr
                    typeof(int),        // i
                    typeof(int),        // j
                    typeof(float) }     // val
                , null);

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    il.Emit(OpCodes.Ldarg_2);               //   {output}
                    il.Emit(OpCodes.Ldc_I4_S, (short)i);    //   {output,i}
                    il.Emit(OpCodes.Ldc_I4_S, (short)j);    // X={output,i,j}

                    il.Emit(OpCodes.Ldc_R4, 128.0f);        // {X,128.0f}

                    for (int k = 0; k < 8; k++)
                    {
                        il.Emit(OpCodes.Ldarg_1);           // {X,temp} 
                        il.Emit(OpCodes.Ldc_I4_S,
                            (short)(k * 8 + j));            // {X,temp,idx}
                        il.Emit(OpCodes.Ldelem_R4);         // {X,temp[idx]}
                        il.Emit(OpCodes.Ldc_R4, cT[i, k]);  // {X,temp[idx],cT[i,k]}
                        il.Emit(OpCodes.Mul);               // {X,in[idx]*c[k,j]}
                        il.Emit(OpCodes.Add);
                    }

                    il.EmitCall(OpCodes.Call, meth, null);
                }
            }

            il.Emit(OpCodes.Ret);

            return (IDCTFunc)idctMethod.CreateDelegate(typeof(IDCTFunc));
        }

        private delegate void IDCTFunc(float[] input, float[] temp, byte[,] output);
        private static IDCTFunc dynamicIDCT = null;
#endif


    }
}