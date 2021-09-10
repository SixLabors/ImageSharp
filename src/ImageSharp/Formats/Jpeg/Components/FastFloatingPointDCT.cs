// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
    }
}
