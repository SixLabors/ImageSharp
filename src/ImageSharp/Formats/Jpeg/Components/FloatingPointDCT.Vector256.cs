// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal static partial class FloatingPointDCT
{
    /// <summary>
    /// Apply floating point FDCT in place using simd operations.
    /// </summary>
    /// <param name="block">Input block.</param>
    private static void FDCT8x8_Vector256(ref Block8x8F block)
    {
        DebugGuard.IsTrue(Vector256.IsHardwareAccelerated, "Vector256 support is required to execute this operation.");

        // First pass - process columns
        FDCT8x8_1D_Vector256(ref block);

        // Second pass - process rows
        block.TransposeInPlace();
        FDCT8x8_1D_Vector256(ref block);

        // Applies 1D floating point FDCT in place
        static void FDCT8x8_1D_Vector256(ref Block8x8F block)
        {
            Vector256<float> tmp0 = block.V256_0 + block.V256_7;
            Vector256<float> tmp7 = block.V256_0 - block.V256_7;
            Vector256<float> tmp1 = block.V256_1 + block.V256_6;
            Vector256<float> tmp6 = block.V256_1 - block.V256_6;
            Vector256<float> tmp2 = block.V256_2 + block.V256_5;
            Vector256<float> tmp5 = block.V256_2 - block.V256_5;
            Vector256<float> tmp3 = block.V256_3 + block.V256_4;
            Vector256<float> tmp4 = block.V256_3 - block.V256_4;

            // Even part
            Vector256<float> tmp10 = tmp0 + tmp3;
            Vector256<float> tmp13 = tmp0 - tmp3;
            Vector256<float> tmp11 = tmp1 + tmp2;
            Vector256<float> tmp12 = tmp1 - tmp2;

            block.V256_0 = tmp10 + tmp11;
            block.V256_4 = tmp10 - tmp11;

            Vector256<float> mm256_F_0_7071 = Vector256.Create(0.707106781f);
            Vector256<float> z1 = (tmp12 + tmp13) * mm256_F_0_7071;
            block.V256_2 = tmp13 + z1;
            block.V256_6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5;
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            Vector256<float> z5 = (tmp10 - tmp12) * Vector256.Create(0.382683433f);    // mm256_F_0_3826
            Vector256<float> z2 = Vector256_.MultiplyAdd(z5, Vector256.Create(0.541196100f), tmp10);    // mm256_F_0_5411
            Vector256<float> z4 = Vector256_.MultiplyAdd(z5, Vector256.Create(1.306562965f), tmp12);    // mm256_F_1_3065
            Vector256<float> z3 = tmp11 * mm256_F_0_7071;

            Vector256<float> z11 = tmp7 + z3;
            Vector256<float> z13 = tmp7 - z3;

            block.V256_5 = z13 + z2;
            block.V256_3 = z13 - z2;
            block.V256_1 = z11 + z4;
            block.V256_7 = z11 - z4;
        }
    }

    /// <summary>
    /// Apply floating point IDCT in place using simd operations.
    /// </summary>
    /// <param name="transposedBlock">Transposed input block.</param>
    private static void IDCT8x8_Vector256(ref Block8x8F transposedBlock)
    {
        DebugGuard.IsTrue(Vector256.IsHardwareAccelerated, "Vector256 support is required to execute this operation.");

        // First pass - process columns
        IDCT8x8_1D_Vector256(ref transposedBlock);

        // Second pass - process rows
        transposedBlock.TransposeInPlace();
        IDCT8x8_1D_Vector256(ref transposedBlock);

        // Applies 1D floating point FDCT in place
        static void IDCT8x8_1D_Vector256(ref Block8x8F block)
        {
            // Even part
            Vector256<float> tmp0 = block.V256_0;
            Vector256<float> tmp1 = block.V256_2;
            Vector256<float> tmp2 = block.V256_4;
            Vector256<float> tmp3 = block.V256_6;

            Vector256<float> z5 = tmp0;
            Vector256<float> tmp10 = z5 + tmp2;
            Vector256<float> tmp11 = z5 - tmp2;

            Vector256<float> mm256_F_1_4142 = Vector256.Create(1.414213562f);
            Vector256<float> tmp13 = tmp1 + tmp3;
            Vector256<float> tmp12 = Vector256_.MultiplySubtract(tmp13, tmp1 - tmp3, mm256_F_1_4142);

            tmp0 = tmp10 + tmp13;
            tmp3 = tmp10 - tmp13;
            tmp1 = tmp11 + tmp12;
            tmp2 = tmp11 - tmp12;

            // Odd part
            Vector256<float> tmp4 = block.V256_1;
            Vector256<float> tmp5 = block.V256_3;
            Vector256<float> tmp6 = block.V256_5;
            Vector256<float> tmp7 = block.V256_7;

            Vector256<float> z13 = tmp6 + tmp5;
            Vector256<float> z10 = tmp6 - tmp5;
            Vector256<float> z11 = tmp4 + tmp7;
            Vector256<float> z12 = tmp4 - tmp7;

            tmp7 = z11 + z13;
            tmp11 = (z11 - z13) * mm256_F_1_4142;

            z5 = (z10 + z12) * Vector256.Create(1.847759065f);   // mm256_F_1_8477

            tmp10 = Vector256_.MultiplyAdd(z5, z12, Vector256.Create(-1.082392200f));   // mm256_F_n1_0823
            tmp12 = Vector256_.MultiplyAdd(z5, z10, Vector256.Create(-2.613125930f));   // mm256_F_n2_6131

            tmp6 = tmp12 - tmp7;
            tmp5 = tmp11 - tmp6;
            tmp4 = tmp10 - tmp5;

            block.V256_0 = tmp0 + tmp7;
            block.V256_7 = tmp0 - tmp7;
            block.V256_1 = tmp1 + tmp6;
            block.V256_6 = tmp1 - tmp6;
            block.V256_2 = tmp2 + tmp5;
            block.V256_5 = tmp2 - tmp5;
            block.V256_3 = tmp3 + tmp4;
            block.V256_4 = tmp3 - tmp4;
        }
    }
}
