// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

#pragma warning disable IDE0078
namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Contains floating point forward DCT implementations with built-in scaling.
/// </summary>
/// <remarks>
/// Based on "Loeffler, Ligtenberg, and Moschytz" algorithm.
/// </remarks>
internal static class ScaledFloatingPointDCT
{
#pragma warning disable SA1310
    private const float FP32_0_541196100 = 0.541196100f;
    private const float FP32_0_765366865 = 0.765366865f;
    private const float FP32_1_847759065 = 1.847759065f;
    private const float FP32_0_211164243 = 0.211164243f;
    private const float FP32_1_451774981 = 1.451774981f;
    private const float FP32_2_172734803 = 2.172734803f;
    private const float FP32_1_061594337 = 1.061594337f;
    private const float FP32_0_509795579 = 0.509795579f;
    private const float FP32_0_601344887 = 0.601344887f;
    private const float FP32_0_899976223 = 0.899976223f;
    private const float FP32_2_562915447 = 2.562915447f;
    private const float FP32_0_720959822 = 0.720959822f;
    private const float FP32_0_850430095 = 0.850430095f;
    private const float FP32_1_272758580 = 1.272758580f;
    private const float FP32_3_624509785 = 3.624509785f;
#pragma warning restore SA1310

    /// <summary>
    /// Adjusts given quantization table for usage with IDCT algorithms
    /// from <see cref="ScaledFloatingPointDCT"/>.
    /// </summary>
    /// <param name="quantTable">Quantization table to adjust.</param>
    public static void AdjustToIDCT(ref Block8x8F quantTable)
    {
        ref float tableRef = ref Unsafe.As<Block8x8F, float>(ref quantTable);
        for (nuint i = 0; i < Block8x8F.Size; i++)
        {
            ref float elemRef = ref Unsafe.Add(ref tableRef, i);
            elemRef = 0.125f * elemRef;
        }

        // Spectral macroblocks are transposed before quantization
        // so we must transpose quantization table
        quantTable.TransposeInPlace();
    }

    /// <summary>
    /// Apply 2D floating point 'donwscaling' IDCT inplace producing
    /// 8x8 -> 4x4 result.
    /// </summary>
    /// <remarks>
    /// Resulting matrix is stored in the top left 4x4 part of the
    /// <paramref name="block"/>.
    /// </remarks>
    /// <param name="block">Input block.</param>
    /// <param name="dequantTable">Dequantization table adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="normalizationValue">Output range normalization value, 1/2 of the <paramref name="maxValue"/>.</param>
    /// <param name="maxValue">Maximum value of the output range.</param>
    public static void TransformIDCT_4x4(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
    {
        for (int ctr = 0; ctr < 8; ctr++)
        {
            // Don't process row 4, second pass doesn't use it
            if (ctr == 4)
            {
                continue;
            }

            // Even part
            float tmp0 = block[(ctr * 8) + 0] * dequantTable[(ctr * 8) + 0] * 2;

            float z2 = block[(ctr * 8) + 2] * dequantTable[(ctr * 8) + 2];
            float z3 = block[(ctr * 8) + 6] * dequantTable[(ctr * 8) + 6];

            float tmp2 = (z2 * FP32_1_847759065) + (z3 * -FP32_0_765366865);

            float tmp10 = tmp0 + tmp2;
            float tmp12 = tmp0 - tmp2;

            // Odd part
            float z1 = block[(ctr * 8) + 7] * dequantTable[(ctr * 8) + 7];
            z2 = block[(ctr * 8) + 5] * dequantTable[(ctr * 8) + 5];
            z3 = block[(ctr * 8) + 3] * dequantTable[(ctr * 8) + 3];
            float z4 = block[(ctr * 8) + 1] * dequantTable[(ctr * 8) + 1];

            tmp0 = (z1 * -FP32_0_211164243) +
                   (z2 * FP32_1_451774981) +
                   (z3 * -FP32_2_172734803) +
                   (z4 * FP32_1_061594337);

            tmp2 = (z1 * -FP32_0_509795579) +
                   (z2 * -FP32_0_601344887) +
                   (z3 * FP32_0_899976223) +
                   (z4 * FP32_2_562915447);

            // temporal result is saved to +4 shifted indices
            // because result is saved into the top left 2x2 region of the
            // input block
            block[(ctr * 8) + 0 + 4] = (tmp10 + tmp2) * 0.5F;
            block[(ctr * 8) + 3 + 4] = (tmp10 - tmp2) * 0.5F;
            block[(ctr * 8) + 1 + 4] = (tmp12 + tmp0) * 0.5F;
            block[(ctr * 8) + 2 + 4] = (tmp12 - tmp0) * 0.5F;
        }

        for (int ctr = 0; ctr < 4; ctr++)
        {
            // Even part
            float tmp0 = block[ctr + (8 * 0) + 4] * 2;

            float tmp2 = (block[ctr + (8 * 2) + 4] * FP32_1_847759065) + (block[ctr + (8 * 6) + 4] * -FP32_0_765366865);

            float tmp10 = tmp0 + tmp2;
            float tmp12 = tmp0 - tmp2;

            // Odd part
            float z1 = block[ctr + (8 * 7) + 4];
            float z2 = block[ctr + (8 * 5) + 4];
            float z3 = block[ctr + (8 * 3) + 4];
            float z4 = block[ctr + (8 * 1) + 4];

            tmp0 = (z1 * -FP32_0_211164243) +
                   (z2 * FP32_1_451774981) +
                   (z3 * -FP32_2_172734803) +
                   (z4 * FP32_1_061594337);

            tmp2 = (z1 * -FP32_0_509795579) +
                   (z2 * -FP32_0_601344887) +
                   (z3 * FP32_0_899976223) +
                   (z4 * FP32_2_562915447);

            // Save results to the top left 4x4 subregion
            block[(ctr * 8) + 0] = MathF.Round(Numerics.Clamp(((tmp10 + tmp2) * 0.5F) + normalizationValue, 0, maxValue));
            block[(ctr * 8) + 3] = MathF.Round(Numerics.Clamp(((tmp10 - tmp2) * 0.5F) + normalizationValue, 0, maxValue));
            block[(ctr * 8) + 1] = MathF.Round(Numerics.Clamp(((tmp12 + tmp0) * 0.5F) + normalizationValue, 0, maxValue));
            block[(ctr * 8) + 2] = MathF.Round(Numerics.Clamp(((tmp12 - tmp0) * 0.5F) + normalizationValue, 0, maxValue));
        }
    }

    /// <summary>
    /// Apply 2D floating point 'donwscaling' IDCT inplace producing
    /// 8x8 -> 2x2 result.
    /// </summary>
    /// <remarks>
    /// Resulting matrix is stored in the top left 2x2 part of the
    /// <paramref name="block"/>.
    /// </remarks>
    /// <param name="block">Input block.</param>
    /// <param name="dequantTable">Dequantization table adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="normalizationValue">Output range normalization value, 1/2 of the <paramref name="maxValue"/>.</param>
    /// <param name="maxValue">Maximum value of the output range.</param>
    public static void TransformIDCT_2x2(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
    {
        for (int ctr = 0; ctr < 8; ctr++)
        {
            // Don't process rows 2/4/6, second pass doesn't use it
            if (ctr == 2 || ctr == 4 || ctr == 6)
            {
                continue;
            }

            // Even part
            float tmp0;
            float z1 = block[(ctr * 8) + 0] * dequantTable[(ctr * 8) + 0];
            float tmp10 = z1 * 4;

            // Odd part
            z1 = block[(ctr * 8) + 7] * dequantTable[(ctr * 8) + 7];
            tmp0 = z1 * -FP32_0_720959822;
            z1 = block[(ctr * 8) + 5] * dequantTable[(ctr * 8) + 5];
            tmp0 += z1 * FP32_0_850430095;
            z1 = block[(ctr * 8) + 3] * dequantTable[(ctr * 8) + 3];
            tmp0 += z1 * -FP32_1_272758580;
            z1 = block[(ctr * 8) + 1] * dequantTable[(ctr * 8) + 1];
            tmp0 += z1 * FP32_3_624509785;

            // temporal result is saved to +2 shifted indices
            // because result is saved into the top left 2x2 region of the
            // input block
            block[(ctr * 8) + 2] = (tmp10 + tmp0) * 0.25F;
            block[(ctr * 8) + 3] = (tmp10 - tmp0) * 0.25F;
        }

        for (int ctr = 0; ctr < 2; ctr++)
        {
            // Even part
            float tmp10 = block[ctr + (8 * 0) + 2] * 4;

            // Odd part
            float tmp0 = (block[ctr + (8 * 7) + 2] * -FP32_0_720959822) +
                   (block[ctr + (8 * 5) + 2] * FP32_0_850430095) +
                   (block[ctr + (8 * 3) + 2] * -FP32_1_272758580) +
                   (block[ctr + (8 * 1) + 2] * FP32_3_624509785);

            // Save results to the top left 2x2 subregion
            block[(ctr * 8) + 0] = MathF.Round(Numerics.Clamp(((tmp10 + tmp0) * 0.25F) + normalizationValue, 0, maxValue));
            block[(ctr * 8) + 1] = MathF.Round(Numerics.Clamp(((tmp10 - tmp0) * 0.25F) + normalizationValue, 0, maxValue));
        }
    }

    /// <summary>
    /// Apply 2D floating point 'donwscaling' IDCT inplace producing
    /// 8x8 -> 1x1 result.
    /// </summary>
    /// <param name="dc">Direct current term value from input block.</param>
    /// <param name="dequantizer">Dequantization value.</param>
    /// <param name="normalizationValue">Output range normalization value, 1/2 of the <paramref name="maxValue"/>.</param>
    /// <param name="maxValue">Maximum value of the output range.</param>
    public static float TransformIDCT_1x1(float dc, float dequantizer, float normalizationValue, float maxValue)
        => MathF.Round(Numerics.Clamp((dc * dequantizer) + normalizationValue, 0, maxValue));
}
#pragma warning restore IDE0078
