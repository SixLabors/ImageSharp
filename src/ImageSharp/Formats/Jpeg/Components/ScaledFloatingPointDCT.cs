// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Contains floating point forward DCT implementations with built-in scaling.
    /// </summary>
    /// <remarks>
    /// Based on "Loeffler, Ligtenberg, and Moschytz" algorithm.
    /// </remarks>
    internal static class ScaledFloatingPointDCT
    {
#pragma warning disable SA1310 // naming rules violation warnings
        private const float F32_0_541196100 = 0.541196100f;
        private const float F32_0_765366865 = 0.765366865f;
        private const float F32_1_847759065 = 1.847759065f;
#pragma warning restore SA1310

        /// <summary>
        /// Adjusts given quantization table for usage with IDCT algorithms
        /// from <see cref="ScaledFloatingPointDCT"/>.
        /// </summary>
        /// <param name="quantTable">Quantization table to adjust.</param>
        public static void AdjustToIDCT(ref Block8x8F quantTable)
        {
            ref float tableRef = ref Unsafe.As<Block8x8F, float>(ref quantTable);
            for (nint i = 0; i < Block8x8F.Size; i++)
            {
                ref float elemRef = ref Unsafe.Add(ref tableRef, i);
                elemRef = 0.125f * elemRef;
            }

            // Spectral macroblocks are transposed before quantization
            // so we must transpose quantization table
            quantTable.TransposeInplace();
        }

        public static float TransformIDCT_1x1(float dc, float dequantizer, float normalizationValue, float maxValue)
            => MathF.Round(Numerics.Clamp((dc * dequantizer) + normalizationValue, 0, maxValue));

        public static void TransformIDCT_2x2(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
        {
            float tmp4 = block[0] * dequantTable[0];
            float tmp5 = block[1] * dequantTable[1];
            float tmp0 = tmp4 + tmp5;
            float tmp2 = tmp4 - tmp5;

            tmp4 = block[8] * dequantTable[8];
            tmp5 = block[9] * dequantTable[9];
            float tmp1 = tmp4 + tmp5;
            float tmp3 = tmp4 - tmp5;

            block[0] = MathF.Round(Numerics.Clamp(tmp0 + tmp1 + normalizationValue, 0, maxValue));
            block[1] = MathF.Round(Numerics.Clamp(tmp0 - tmp1 + normalizationValue, 0, maxValue));
            block[8] = MathF.Round(Numerics.Clamp(tmp2 + tmp3 + normalizationValue, 0, maxValue));
            block[9] = MathF.Round(Numerics.Clamp(tmp2 - tmp3 + normalizationValue, 0, maxValue));
        }

        public static void TransformIDCT_4x4(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
        {
            for (int ctr = 0; ctr < 4; ctr++)
            {
                // Even part
                float tmp0 = block[ctr * 8] * dequantTable[ctr * 8];
                float tmp2 = block[(ctr * 8) + 2] * dequantTable[(ctr * 8) + 2];

                float tmp10 = tmp0 + tmp2;
                float tmp12 = tmp0 - tmp2;

                // Odd part
                float z2 = block[(ctr * 8) + 1] * dequantTable[(ctr * 8) + 1];
                float z3 = block[(ctr * 8) + 3] * dequantTable[(ctr * 8) + 3];

                float z1 = (z2 + z3) * F32_0_541196100;
                tmp0 = z1 + (z2 * F32_0_765366865);
                tmp2 = z1 - (z3 * F32_1_847759065);

                /* Final output stage */
                block[ctr + 4] = tmp10 + tmp0;
                block[ctr + 28] = tmp10 - tmp0;
                block[ctr + 12] = tmp12 + tmp2;
                block[ctr + 20] = tmp12 - tmp2;
            }

            for (int ctr = 0; ctr < 4; ctr++)
            {
                // Even part
                float tmp0 = block[(ctr * 8) + 0 + 4];
                float tmp2 = block[(ctr * 8) + 2 + 4];

                float tmp10 = tmp0 + tmp2;
                float tmp12 = tmp0 - tmp2;

                // Odd part
                float z2 = block[(ctr * 8) + 1 + 4];
                float z3 = block[(ctr * 8) + 3 + 4];

                float z1 = (z2 + z3) * F32_0_541196100;
                tmp0 = z1 + (z2 * F32_0_765366865);
                tmp2 = z1 - (z3 * F32_1_847759065);

                /* Final output stage */
                block[(ctr * 8) + 0] = MathF.Round(Numerics.Clamp(tmp10 + tmp0 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 3] = MathF.Round(Numerics.Clamp(tmp10 - tmp0 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 1] = MathF.Round(Numerics.Clamp(tmp12 + tmp2 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 2] = MathF.Round(Numerics.Clamp(tmp12 - tmp2 + normalizationValue, 0, maxValue));
            }
        }
    }
}
