// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the one-dimensional transform operators.
/// </content>
internal static partial class FloatingPointDCT
{
    // The Arai, Agui and Nakajima constants. The quantization tables hold the remaining scale factors.
    // See AdjustToIDCT and AdjustToFDCT. The names give the value, as in the reference implementations.
#pragma warning disable SA1310 // Field names should not contain underscore
    private const float F_0_3826 = 0.382683433f;
    private const float F_0_5411 = 0.541196100f;
    private const float F_0_7071 = 0.707106781f;
    private const float F_1_3065 = 1.306562965f;
    private const float F_1_4142 = 1.414213562f;
    private const float F_1_8477 = 1.847759065f;
    private const float F_N1_0823 = -1.082392200f;
    private const float F_N2_6131 = -2.613125930f;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>
    /// Defines a one-dimensional transform of eight values for scalar values and for each SIMD width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each argument holds one row of the block. The operator transforms each lane independently:
    /// lane <c>j</c> of the eight arguments is the column <c>j</c>. Thus, one call transforms every column in the registers.
    /// </para>
    /// <para>
    /// All widths use the same operations in the same order. <c>MultiplyAddEstimate</c> uses a fused multiply-add
    /// when the hardware has one. Thus, the widths give the same result on the same machine.
    /// </para>
    /// <para>
    /// A scaled transform gives fewer outputs than inputs. It writes its <see cref="OutputSize"/> outputs to the
    /// first arguments. The values in the other arguments are not defined.
    /// </para>
    /// </remarks>
    internal interface ITransformOperator
    {
        /// <summary>
        /// Gets the number of outputs of one transform: 8 for the full transform, and 4, 2 or 1 for a scaled transform.
        /// </summary>
        static abstract int OutputSize { get; }

        /// <summary>
        /// Transforms one column of eight values.
        /// </summary>
        /// <param name="v0">Row 0.</param>
        /// <param name="v1">Row 1.</param>
        /// <param name="v2">Row 2.</param>
        /// <param name="v3">Row 3.</param>
        /// <param name="v4">Row 4.</param>
        /// <param name="v5">Row 5.</param>
        /// <param name="v6">Row 6.</param>
        /// <param name="v7">Row 7.</param>
        static abstract void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7);

        /// <summary>
        /// Transforms four columns of eight values.
        /// </summary>
        /// <param name="v0">Row 0.</param>
        /// <param name="v1">Row 1.</param>
        /// <param name="v2">Row 2.</param>
        /// <param name="v3">Row 3.</param>
        /// <param name="v4">Row 4.</param>
        /// <param name="v5">Row 5.</param>
        /// <param name="v6">Row 6.</param>
        /// <param name="v7">Row 7.</param>
        static abstract void Invoke(
            ref Vector128<float> v0,
            ref Vector128<float> v1,
            ref Vector128<float> v2,
            ref Vector128<float> v3,
            ref Vector128<float> v4,
            ref Vector128<float> v5,
            ref Vector128<float> v6,
            ref Vector128<float> v7);

        /// <summary>
        /// Transforms eight columns of eight values.
        /// </summary>
        /// <param name="v0">Row 0.</param>
        /// <param name="v1">Row 1.</param>
        /// <param name="v2">Row 2.</param>
        /// <param name="v3">Row 3.</param>
        /// <param name="v4">Row 4.</param>
        /// <param name="v5">Row 5.</param>
        /// <param name="v6">Row 6.</param>
        /// <param name="v7">Row 7.</param>
        static abstract void Invoke(
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7);

        /// <summary>
        /// Transforms sixteen columns of eight values.
        /// </summary>
        /// <param name="v0">Row 0.</param>
        /// <param name="v1">Row 1.</param>
        /// <param name="v2">Row 2.</param>
        /// <param name="v3">Row 3.</param>
        /// <param name="v4">Row 4.</param>
        /// <param name="v5">Row 5.</param>
        /// <param name="v6">Row 6.</param>
        /// <param name="v7">Row 7.</param>
        static abstract void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7);
    }

    /// <summary>
    /// The one-dimensional inverse transform of the Arai, Agui and Nakajima algorithm.
    /// </summary>
    /// <remarks>
    /// The caller must dequantize the input with a table that <see cref="AdjustToIDCT"/> adjusts.
    /// </remarks>
    internal readonly struct InverseTransformOperator : ITransformOperator
    {
        /// <inheritdoc/>
        public static int OutputSize => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7)
        {
            // Even part
            float tmp10 = v0 + v4;
            float tmp11 = v0 - v4;
            float tmp13 = v2 + v6;
            float tmp12 = float.MultiplyAddEstimate(v2 - v6, F_1_4142, -tmp13);

            float tmp0 = tmp10 + tmp13;
            float tmp3 = tmp10 - tmp13;
            float tmp1 = tmp11 + tmp12;
            float tmp2 = tmp11 - tmp12;

            // Odd part
            float z13 = v5 + v3;
            float z10 = v5 - v3;
            float z11 = v1 + v7;
            float z12 = v1 - v7;

            float tmp7 = z11 + z13;
            tmp11 = (z11 - z13) * F_1_4142;

            float z5 = (z10 + z12) * F_1_8477;
            tmp10 = float.MultiplyAddEstimate(z12, F_N1_0823, z5);
            tmp12 = float.MultiplyAddEstimate(z10, F_N2_6131, z5);

            float tmp6 = tmp12 - tmp7;
            float tmp5 = tmp11 - tmp6;
            float tmp4 = tmp10 - tmp5;

            v0 = tmp0 + tmp7;
            v7 = tmp0 - tmp7;
            v1 = tmp1 + tmp6;
            v6 = tmp1 - tmp6;
            v2 = tmp2 + tmp5;
            v5 = tmp2 - tmp5;
            v3 = tmp3 + tmp4;
            v4 = tmp3 - tmp4;
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
            Vector128<float> tmp10 = v0 + v4;
            Vector128<float> tmp11 = v0 - v4;
            Vector128<float> tmp13 = v2 + v6;
            Vector128<float> tmp12 = Vector128.MultiplyAddEstimate(v2 - v6, Vector128.Create(F_1_4142), -tmp13);

            Vector128<float> tmp0 = tmp10 + tmp13;
            Vector128<float> tmp3 = tmp10 - tmp13;
            Vector128<float> tmp1 = tmp11 + tmp12;
            Vector128<float> tmp2 = tmp11 - tmp12;

            // Odd part
            Vector128<float> z13 = v5 + v3;
            Vector128<float> z10 = v5 - v3;
            Vector128<float> z11 = v1 + v7;
            Vector128<float> z12 = v1 - v7;

            Vector128<float> tmp7 = z11 + z13;
            tmp11 = (z11 - z13) * Vector128.Create(F_1_4142);

            Vector128<float> z5 = (z10 + z12) * Vector128.Create(F_1_8477);
            tmp10 = Vector128.MultiplyAddEstimate(z12, Vector128.Create(F_N1_0823), z5);
            tmp12 = Vector128.MultiplyAddEstimate(z10, Vector128.Create(F_N2_6131), z5);

            Vector128<float> tmp6 = tmp12 - tmp7;
            Vector128<float> tmp5 = tmp11 - tmp6;
            Vector128<float> tmp4 = tmp10 - tmp5;

            v0 = tmp0 + tmp7;
            v7 = tmp0 - tmp7;
            v1 = tmp1 + tmp6;
            v6 = tmp1 - tmp6;
            v2 = tmp2 + tmp5;
            v5 = tmp2 - tmp5;
            v3 = tmp3 + tmp4;
            v4 = tmp3 - tmp4;
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
            Vector256<float> tmp10 = v0 + v4;
            Vector256<float> tmp11 = v0 - v4;
            Vector256<float> tmp13 = v2 + v6;
            Vector256<float> tmp12 = Vector256.MultiplyAddEstimate(v2 - v6, Vector256.Create(F_1_4142), -tmp13);

            Vector256<float> tmp0 = tmp10 + tmp13;
            Vector256<float> tmp3 = tmp10 - tmp13;
            Vector256<float> tmp1 = tmp11 + tmp12;
            Vector256<float> tmp2 = tmp11 - tmp12;

            // Odd part
            Vector256<float> z13 = v5 + v3;
            Vector256<float> z10 = v5 - v3;
            Vector256<float> z11 = v1 + v7;
            Vector256<float> z12 = v1 - v7;

            Vector256<float> tmp7 = z11 + z13;
            tmp11 = (z11 - z13) * Vector256.Create(F_1_4142);

            Vector256<float> z5 = (z10 + z12) * Vector256.Create(F_1_8477);
            tmp10 = Vector256.MultiplyAddEstimate(z12, Vector256.Create(F_N1_0823), z5);
            tmp12 = Vector256.MultiplyAddEstimate(z10, Vector256.Create(F_N2_6131), z5);

            Vector256<float> tmp6 = tmp12 - tmp7;
            Vector256<float> tmp5 = tmp11 - tmp6;
            Vector256<float> tmp4 = tmp10 - tmp5;

            v0 = tmp0 + tmp7;
            v7 = tmp0 - tmp7;
            v1 = tmp1 + tmp6;
            v6 = tmp1 - tmp6;
            v2 = tmp2 + tmp5;
            v5 = tmp2 - tmp5;
            v3 = tmp3 + tmp4;
            v4 = tmp3 - tmp4;
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
            Vector512<float> tmp10 = v0 + v4;
            Vector512<float> tmp11 = v0 - v4;
            Vector512<float> tmp13 = v2 + v6;
            Vector512<float> tmp12 = Vector512.MultiplyAddEstimate(v2 - v6, Vector512.Create(F_1_4142), -tmp13);

            Vector512<float> tmp0 = tmp10 + tmp13;
            Vector512<float> tmp3 = tmp10 - tmp13;
            Vector512<float> tmp1 = tmp11 + tmp12;
            Vector512<float> tmp2 = tmp11 - tmp12;

            // Odd part
            Vector512<float> z13 = v5 + v3;
            Vector512<float> z10 = v5 - v3;
            Vector512<float> z11 = v1 + v7;
            Vector512<float> z12 = v1 - v7;

            Vector512<float> tmp7 = z11 + z13;
            tmp11 = (z11 - z13) * Vector512.Create(F_1_4142);

            Vector512<float> z5 = (z10 + z12) * Vector512.Create(F_1_8477);
            tmp10 = Vector512.MultiplyAddEstimate(z12, Vector512.Create(F_N1_0823), z5);
            tmp12 = Vector512.MultiplyAddEstimate(z10, Vector512.Create(F_N2_6131), z5);

            Vector512<float> tmp6 = tmp12 - tmp7;
            Vector512<float> tmp5 = tmp11 - tmp6;
            Vector512<float> tmp4 = tmp10 - tmp5;

            v0 = tmp0 + tmp7;
            v7 = tmp0 - tmp7;
            v1 = tmp1 + tmp6;
            v6 = tmp1 - tmp6;
            v2 = tmp2 + tmp5;
            v5 = tmp2 - tmp5;
            v3 = tmp3 + tmp4;
            v4 = tmp3 - tmp4;
        }
    }

    /// <summary>
    /// The one-dimensional forward transform of the Arai, Agui and Nakajima algorithm.
    /// </summary>
    /// <remarks>
    /// The caller must quantize the output with a table that <see cref="AdjustToFDCT"/> adjusts.
    /// </remarks>
    internal readonly struct ForwardTransformOperator : ITransformOperator
    {
        /// <inheritdoc/>
        public static int OutputSize => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref float v0, ref float v1, ref float v2, ref float v3, ref float v4, ref float v5, ref float v6, ref float v7)
        {
            float tmp0 = v0 + v7;
            float tmp7 = v0 - v7;
            float tmp1 = v1 + v6;
            float tmp6 = v1 - v6;
            float tmp2 = v2 + v5;
            float tmp5 = v2 - v5;
            float tmp3 = v3 + v4;
            float tmp4 = v3 - v4;

            // Even part
            float tmp10 = tmp0 + tmp3;
            float tmp13 = tmp0 - tmp3;
            float tmp11 = tmp1 + tmp2;
            float tmp12 = tmp1 - tmp2;

            v0 = tmp10 + tmp11;
            v4 = tmp10 - tmp11;

            float z1 = (tmp12 + tmp13) * F_0_7071;
            v2 = tmp13 + z1;
            v6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5;
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            float z5 = (tmp10 - tmp12) * F_0_3826;
            float z2 = float.MultiplyAddEstimate(F_0_5411, tmp10, z5);
            float z4 = float.MultiplyAddEstimate(F_1_3065, tmp12, z5);
            float z3 = tmp11 * F_0_7071;

            float z11 = tmp7 + z3;
            float z13 = tmp7 - z3;

            v5 = z13 + z2;
            v3 = z13 - z2;
            v1 = z11 + z4;
            v7 = z11 - z4;
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
            Vector128<float> tmp0 = v0 + v7;
            Vector128<float> tmp7 = v0 - v7;
            Vector128<float> tmp1 = v1 + v6;
            Vector128<float> tmp6 = v1 - v6;
            Vector128<float> tmp2 = v2 + v5;
            Vector128<float> tmp5 = v2 - v5;
            Vector128<float> tmp3 = v3 + v4;
            Vector128<float> tmp4 = v3 - v4;

            // Even part
            Vector128<float> tmp10 = tmp0 + tmp3;
            Vector128<float> tmp13 = tmp0 - tmp3;
            Vector128<float> tmp11 = tmp1 + tmp2;
            Vector128<float> tmp12 = tmp1 - tmp2;

            v0 = tmp10 + tmp11;
            v4 = tmp10 - tmp11;

            Vector128<float> z1 = (tmp12 + tmp13) * Vector128.Create(F_0_7071);
            v2 = tmp13 + z1;
            v6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5;
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            Vector128<float> z5 = (tmp10 - tmp12) * Vector128.Create(F_0_3826);
            Vector128<float> z2 = Vector128.MultiplyAddEstimate(Vector128.Create(F_0_5411), tmp10, z5);
            Vector128<float> z4 = Vector128.MultiplyAddEstimate(Vector128.Create(F_1_3065), tmp12, z5);
            Vector128<float> z3 = tmp11 * Vector128.Create(F_0_7071);

            Vector128<float> z11 = tmp7 + z3;
            Vector128<float> z13 = tmp7 - z3;

            v5 = z13 + z2;
            v3 = z13 - z2;
            v1 = z11 + z4;
            v7 = z11 - z4;
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
            Vector256<float> tmp0 = v0 + v7;
            Vector256<float> tmp7 = v0 - v7;
            Vector256<float> tmp1 = v1 + v6;
            Vector256<float> tmp6 = v1 - v6;
            Vector256<float> tmp2 = v2 + v5;
            Vector256<float> tmp5 = v2 - v5;
            Vector256<float> tmp3 = v3 + v4;
            Vector256<float> tmp4 = v3 - v4;

            // Even part
            Vector256<float> tmp10 = tmp0 + tmp3;
            Vector256<float> tmp13 = tmp0 - tmp3;
            Vector256<float> tmp11 = tmp1 + tmp2;
            Vector256<float> tmp12 = tmp1 - tmp2;

            v0 = tmp10 + tmp11;
            v4 = tmp10 - tmp11;

            Vector256<float> z1 = (tmp12 + tmp13) * Vector256.Create(F_0_7071);
            v2 = tmp13 + z1;
            v6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5;
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            Vector256<float> z5 = (tmp10 - tmp12) * Vector256.Create(F_0_3826);
            Vector256<float> z2 = Vector256.MultiplyAddEstimate(Vector256.Create(F_0_5411), tmp10, z5);
            Vector256<float> z4 = Vector256.MultiplyAddEstimate(Vector256.Create(F_1_3065), tmp12, z5);
            Vector256<float> z3 = tmp11 * Vector256.Create(F_0_7071);

            Vector256<float> z11 = tmp7 + z3;
            Vector256<float> z13 = tmp7 - z3;

            v5 = z13 + z2;
            v3 = z13 - z2;
            v1 = z11 + z4;
            v7 = z11 - z4;
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
            Vector512<float> tmp0 = v0 + v7;
            Vector512<float> tmp7 = v0 - v7;
            Vector512<float> tmp1 = v1 + v6;
            Vector512<float> tmp6 = v1 - v6;
            Vector512<float> tmp2 = v2 + v5;
            Vector512<float> tmp5 = v2 - v5;
            Vector512<float> tmp3 = v3 + v4;
            Vector512<float> tmp4 = v3 - v4;

            // Even part
            Vector512<float> tmp10 = tmp0 + tmp3;
            Vector512<float> tmp13 = tmp0 - tmp3;
            Vector512<float> tmp11 = tmp1 + tmp2;
            Vector512<float> tmp12 = tmp1 - tmp2;

            v0 = tmp10 + tmp11;
            v4 = tmp10 - tmp11;

            Vector512<float> z1 = (tmp12 + tmp13) * Vector512.Create(F_0_7071);
            v2 = tmp13 + z1;
            v6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5;
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            Vector512<float> z5 = (tmp10 - tmp12) * Vector512.Create(F_0_3826);
            Vector512<float> z2 = Vector512.MultiplyAddEstimate(Vector512.Create(F_0_5411), tmp10, z5);
            Vector512<float> z4 = Vector512.MultiplyAddEstimate(Vector512.Create(F_1_3065), tmp12, z5);
            Vector512<float> z3 = tmp11 * Vector512.Create(F_0_7071);

            Vector512<float> z11 = tmp7 + z3;
            Vector512<float> z13 = tmp7 - z3;

            v5 = z13 + z2;
            v3 = z13 - z2;
            v1 = z11 + z4;
            v7 = z11 - z4;
        }
    }
}
