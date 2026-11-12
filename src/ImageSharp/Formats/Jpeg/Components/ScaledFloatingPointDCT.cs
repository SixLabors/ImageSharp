// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

#pragma warning disable IDE0078
namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Contains floating point inverse DCT implementations with built-in scaling.
/// </summary>
/// <remarks>
/// Based on "Loeffler, Ligtenberg, and Moschytz" algorithm.
/// </remarks>
internal static class ScaledFloatingPointDCT
{
#pragma warning disable SA1310
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
    /// Dequantizes one block, applies the 8x8 to 4x4 inverse transform and writes the normalized samples.
    /// </summary>
    /// <param name="source">The quantized coefficients in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination. The method writes 4 rows of 4 samples.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    public static void TransformIDCT4x4(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride)
        => FloatingPointDCT.TransformIDCT<InverseTransform4Operator>(ref source, ref dequantizationTable, maximum, ref destination, destinationStride);

    /// <summary>
    /// Dequantizes a row of blocks, applies the 8x8 to 4x4 inverse transform and writes the normalized samples.
    /// </summary>
    /// <remarks>
    /// Block <c>i</c> writes columns <c>4 * H * i</c> to <c>4 * H * (i + 1) - 1</c> of <c>4 * V</c> destination rows,
    /// H and V are the scale factors. Each sample covers H x V destination samples.
    /// </remarks>
    /// <param name="source">The quantized coefficient blocks in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    /// <param name="horizontalScale">The number of destination columns for each sample.</param>
    /// <param name="verticalScale">The number of destination rows for each sample.</param>
    /// <param name="clearSource">
    /// <see langword="true"/> to clear each source block after the method reads it. <see langword="false"/> to keep the source blocks.
    /// </param>
    public static void TransformIDCT4x4(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride,
        int horizontalScale,
        int verticalScale,
        bool clearSource)
        => FloatingPointDCT.TransformIDCT<InverseTransform4Operator>(
            source,
            ref dequantizationTable,
            maximum,
            ref destination,
            destinationStride,
            horizontalScale,
            verticalScale,
            clearSource);

    /// <summary>
    /// Dequantizes one block, applies the 8x8 to 2x2 inverse transform and writes the normalized samples.
    /// </summary>
    /// <param name="source">The quantized coefficients in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination. The method writes 2 rows of 2 samples.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    public static void TransformIDCT2x2(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride)
        => FloatingPointDCT.TransformIDCT<InverseTransform2Operator>(ref source, ref dequantizationTable, maximum, ref destination, destinationStride);

    /// <summary>
    /// Dequantizes a row of blocks, applies the 8x8 to 2x2 inverse transform and writes the normalized samples.
    /// </summary>
    /// <remarks>
    /// Block <c>i</c> writes columns <c>2 * H * i</c> to <c>2 * H * (i + 1) - 1</c> of <c>2 * V</c> destination rows,
    /// H and V are the scale factors. Each sample covers H x V destination samples.
    /// </remarks>
    /// <param name="source">The quantized coefficient blocks in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    /// <param name="horizontalScale">The number of destination columns for each sample.</param>
    /// <param name="verticalScale">The number of destination rows for each sample.</param>
    /// <param name="clearSource">
    /// <see langword="true"/> to clear each source block after the method reads it. <see langword="false"/> to keep the source blocks.
    /// </param>
    public static void TransformIDCT2x2(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride,
        int horizontalScale,
        int verticalScale,
        bool clearSource)
        => FloatingPointDCT.TransformIDCT<InverseTransform2Operator>(
            source,
            ref dequantizationTable,
            maximum,
            ref destination,
            destinationStride,
            horizontalScale,
            verticalScale,
            clearSource);

    /// <summary>
    /// Dequantizes a row of blocks, applies the 8x8 to 1x1 inverse transform and writes the normalized samples.
    /// </summary>
    /// <remarks>
    /// The 8x8 to 1x1 transform uses only the DC coefficient. Block <c>i</c> writes columns <c>H * i</c> to
    /// <c>H * (i + 1) - 1</c> of <c>V</c> destination rows. H and V are the scale factors.
    /// </remarks>
    /// <param name="source">The quantized coefficient blocks.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    /// <param name="horizontalScale">The number of destination columns for each sample.</param>
    /// <param name="verticalScale">The number of destination rows for each sample.</param>
    /// <param name="clearSource">
    /// <see langword="true"/> to clear each source block after the method reads it. <see langword="false"/> to keep the source blocks.
    /// </param>
    public static void TransformIDCT1x1(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride,
        int horizontalScale,
        int verticalScale,
        bool clearSource)
        => FloatingPointDCT.TransformIDCT<InverseTransform1Operator>(
            source,
            ref dequantizationTable,
            maximum,
            ref destination,
            destinationStride,
            horizontalScale,
            verticalScale,
            clearSource);

    /// <summary>
    /// The one-dimensional inverse transform from eight coefficients to one sample.
    /// </summary>
    /// <remarks>
    /// The output is input 0: the table adjusted by <see cref="AdjustToIDCT(ref Block8x8F)"/> holds the scale of
    /// the transform. Thus, the transform changes no argument. The decoder stage writes the dequantized DC directly.
    /// </remarks>
    internal readonly struct InverseTransform1Operator : FloatingPointDCT.ITransformOperator
    {
        /// <inheritdoc/>
        public static int OutputSize => 1;

        /// <inheritdoc/>
        public static void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7)
        {
        }

        /// <inheritdoc/>
        public static void Invoke(
            ref Vector128<float> v0,
            ref Vector128<float> v1,
            ref Vector128<float> v2,
            ref Vector128<float> v3,
            ref Vector128<float> v4,
            ref Vector128<float> v5,
            ref Vector128<float> v6,
            ref Vector128<float> v7)
        {
        }

        /// <inheritdoc/>
        public static void Invoke(
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7)
        {
        }

        /// <inheritdoc/>
        public static void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7)
        {
        }
    }

    /// <summary>
    /// The one-dimensional inverse transform from eight coefficients to four samples.
    /// </summary>
    /// <remarks>
    /// The transform does not use input 4. It writes the four outputs to the first four arguments.
    /// All widths use separate multiplications and additions in the same order.
    /// </remarks>
    internal readonly struct InverseTransform4Operator : FloatingPointDCT.ITransformOperator
    {
        /// <inheritdoc/>
        public static int OutputSize => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7)
        {
            // Even part
            float tmp0 = v0 * 2;
            float tmp2 = (v2 * FP32_1_847759065) + (v6 * -FP32_0_765366865);

            float tmp10 = tmp0 + tmp2;
            float tmp12 = tmp0 - tmp2;

            // Odd part
            tmp0 = (v7 * -FP32_0_211164243) + (v5 * FP32_1_451774981) + (v3 * -FP32_2_172734803) + (v1 * FP32_1_061594337);
            tmp2 = (v7 * -FP32_0_509795579) + (v5 * -FP32_0_601344887) + (v3 * FP32_0_899976223) + (v1 * FP32_2_562915447);

            v0 = (tmp10 + tmp2) * 0.5F;
            v3 = (tmp10 - tmp2) * 0.5F;
            v1 = (tmp12 + tmp0) * 0.5F;
            v2 = (tmp12 - tmp0) * 0.5F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector128<float> v0,
            ref Vector128<float> v1,
            ref Vector128<float> v2,
            ref Vector128<float> v3,
            ref Vector128<float> v4,
            ref Vector128<float> v5,
            ref Vector128<float> v6,
            ref Vector128<float> v7)
        {
            // Lane j of each register is column j of 4 columns. Each step is the scalar step for every lane.
            // Even part
            Vector128<float> tmp0 = v0 * 2;
            Vector128<float> tmp2 = (v2 * FP32_1_847759065) + (v6 * -FP32_0_765366865);

            Vector128<float> tmp10 = tmp0 + tmp2;
            Vector128<float> tmp12 = tmp0 - tmp2;

            // Odd part
            tmp0 = (v7 * -FP32_0_211164243) + (v5 * FP32_1_451774981) + (v3 * -FP32_2_172734803) + (v1 * FP32_1_061594337);
            tmp2 = (v7 * -FP32_0_509795579) + (v5 * -FP32_0_601344887) + (v3 * FP32_0_899976223) + (v1 * FP32_2_562915447);

            v0 = (tmp10 + tmp2) * 0.5F;
            v3 = (tmp10 - tmp2) * 0.5F;
            v1 = (tmp12 + tmp0) * 0.5F;
            v2 = (tmp12 - tmp0) * 0.5F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7)
        {
            // Lane j of each register is column j of 8 columns. Each step is the scalar step for every lane.
            // Even part
            Vector256<float> tmp0 = v0 * 2;
            Vector256<float> tmp2 = (v2 * FP32_1_847759065) + (v6 * -FP32_0_765366865);

            Vector256<float> tmp10 = tmp0 + tmp2;
            Vector256<float> tmp12 = tmp0 - tmp2;

            // Odd part
            tmp0 = (v7 * -FP32_0_211164243) + (v5 * FP32_1_451774981) + (v3 * -FP32_2_172734803) + (v1 * FP32_1_061594337);
            tmp2 = (v7 * -FP32_0_509795579) + (v5 * -FP32_0_601344887) + (v3 * FP32_0_899976223) + (v1 * FP32_2_562915447);

            v0 = (tmp10 + tmp2) * 0.5F;
            v3 = (tmp10 - tmp2) * 0.5F;
            v1 = (tmp12 + tmp0) * 0.5F;
            v2 = (tmp12 - tmp0) * 0.5F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7)
        {
            // Lanes 0 to 7 are the columns of the first block. Lanes 8 to 15 are the columns of the second block.
            // Each step is the scalar step for every lane.
            // Even part
            Vector512<float> tmp0 = v0 * 2;
            Vector512<float> tmp2 = (v2 * FP32_1_847759065) + (v6 * -FP32_0_765366865);

            Vector512<float> tmp10 = tmp0 + tmp2;
            Vector512<float> tmp12 = tmp0 - tmp2;

            // Odd part
            tmp0 = (v7 * -FP32_0_211164243) + (v5 * FP32_1_451774981) + (v3 * -FP32_2_172734803) + (v1 * FP32_1_061594337);
            tmp2 = (v7 * -FP32_0_509795579) + (v5 * -FP32_0_601344887) + (v3 * FP32_0_899976223) + (v1 * FP32_2_562915447);

            v0 = (tmp10 + tmp2) * 0.5F;
            v3 = (tmp10 - tmp2) * 0.5F;
            v1 = (tmp12 + tmp0) * 0.5F;
            v2 = (tmp12 - tmp0) * 0.5F;
        }
    }

    /// <summary>
    /// The one-dimensional inverse transform from eight coefficients to two samples.
    /// </summary>
    /// <remarks>
    /// The transform does not use inputs 2, 4 and 6. It writes the two outputs to the first two arguments.
    /// All widths use separate multiplications and additions in the same order.
    /// </remarks>
    internal readonly struct InverseTransform2Operator : FloatingPointDCT.ITransformOperator
    {
        /// <inheritdoc/>
        public static int OutputSize => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7)
        {
            float tmp10 = v0 * 4;
            float tmp0 = (v7 * -FP32_0_720959822) + (v5 * FP32_0_850430095) + (v3 * -FP32_1_272758580) + (v1 * FP32_3_624509785);

            v0 = (tmp10 + tmp0) * 0.25F;
            v1 = (tmp10 - tmp0) * 0.25F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector128<float> v0,
            ref Vector128<float> v1,
            ref Vector128<float> v2,
            ref Vector128<float> v3,
            ref Vector128<float> v4,
            ref Vector128<float> v5,
            ref Vector128<float> v6,
            ref Vector128<float> v7)
        {
            // Lane j of each register is column j of 4 columns. Each step is the scalar step for every lane.
            Vector128<float> tmp10 = v0 * 4;
            Vector128<float> tmp0 = (v7 * -FP32_0_720959822) + (v5 * FP32_0_850430095) + (v3 * -FP32_1_272758580) + (v1 * FP32_3_624509785);

            v0 = (tmp10 + tmp0) * 0.25F;
            v1 = (tmp10 - tmp0) * 0.25F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7)
        {
            // Lane j of each register is column j of 8 columns. Each step is the scalar step for every lane.
            Vector256<float> tmp10 = v0 * 4;
            Vector256<float> tmp0 = (v7 * -FP32_0_720959822) + (v5 * FP32_0_850430095) + (v3 * -FP32_1_272758580) + (v1 * FP32_3_624509785);

            v0 = (tmp10 + tmp0) * 0.25F;
            v1 = (tmp10 - tmp0) * 0.25F;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7)
        {
            // Lanes 0 to 7 are the columns of the first block. Lanes 8 to 15 are the columns of the second block.
            // Each step is the scalar step for every lane.
            Vector512<float> tmp10 = v0 * 4;
            Vector512<float> tmp0 = (v7 * -FP32_0_720959822) + (v5 * FP32_0_850430095) + (v3 * -FP32_1_272758580) + (v1 * FP32_3_624509785);

            v0 = (tmp10 + tmp0) * 0.25F;
            v1 = (tmp10 - tmp0) * 0.25F;
        }
    }
}
#pragma warning restore IDE0078
