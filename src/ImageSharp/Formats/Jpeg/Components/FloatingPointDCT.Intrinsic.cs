// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal static partial class FloatingPointDCT
{
    /// <summary>
    /// Apply floating point FDCT inplace using simd operations.
    /// </summary>
    /// <param name="block">Input block.</param>
    private static void FDCT8x8_Avx(ref Block8x8F block)
    {
        DebugGuard.IsTrue(Avx.IsSupported, "Avx support is required to execute this operation.");

        // First pass - process columns
        FDCT8x8_1D_Avx(ref block);

        // Second pass - process rows
        block.TransposeInplace();
        FDCT8x8_1D_Avx(ref block);

        // Applies 1D floating point FDCT inplace
        static void FDCT8x8_1D_Avx(ref Block8x8F block)
        {
            Vector256<float> tmp0 = Avx.Add(block.V0, block.V7);
            Vector256<float> tmp7 = Avx.Subtract(block.V0, block.V7);
            Vector256<float> tmp1 = Avx.Add(block.V1, block.V6);
            Vector256<float> tmp6 = Avx.Subtract(block.V1, block.V6);
            Vector256<float> tmp2 = Avx.Add(block.V2, block.V5);
            Vector256<float> tmp5 = Avx.Subtract(block.V2, block.V5);
            Vector256<float> tmp3 = Avx.Add(block.V3, block.V4);
            Vector256<float> tmp4 = Avx.Subtract(block.V3, block.V4);

            // Even part
            Vector256<float> tmp10 = Avx.Add(tmp0, tmp3);
            Vector256<float> tmp13 = Avx.Subtract(tmp0, tmp3);
            Vector256<float> tmp11 = Avx.Add(tmp1, tmp2);
            Vector256<float> tmp12 = Avx.Subtract(tmp1, tmp2);

            block.V0 = Avx.Add(tmp10, tmp11);
            block.V4 = Avx.Subtract(tmp10, tmp11);

            Vector256<float> mm256_F_0_7071 = Vector256.Create(0.707106781f);
            Vector256<float> z1 = Avx.Multiply(Avx.Add(tmp12, tmp13), mm256_F_0_7071);
            block.V2 = Avx.Add(tmp13, z1);
            block.V6 = Avx.Subtract(tmp13, z1);

            // Odd part
            tmp10 = Avx.Add(tmp4, tmp5);
            tmp11 = Avx.Add(tmp5, tmp6);
            tmp12 = Avx.Add(tmp6, tmp7);

            Vector256<float> z5 = Avx.Multiply(Avx.Subtract(tmp10, tmp12), Vector256.Create(0.382683433f));         // mm256_F_0_3826
            Vector256<float> z2 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, Vector256.Create(0.541196100f), tmp10);    // mm256_F_0_5411
            Vector256<float> z4 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, Vector256.Create(1.306562965f), tmp12);    // mm256_F_1_3065
            Vector256<float> z3 = Avx.Multiply(tmp11, mm256_F_0_7071);

            Vector256<float> z11 = Avx.Add(tmp7, z3);
            Vector256<float> z13 = Avx.Subtract(tmp7, z3);

            block.V5 = Avx.Add(z13, z2);
            block.V3 = Avx.Subtract(z13, z2);
            block.V1 = Avx.Add(z11, z4);
            block.V7 = Avx.Subtract(z11, z4);
        }
    }

    /// <summary>
    /// Apply floating point IDCT inplace using simd operations.
    /// </summary>
    /// <param name="transposedBlock">Transposed input block.</param>
    private static void IDCT8x8_Avx(ref Block8x8F transposedBlock)
    {
        DebugGuard.IsTrue(Avx.IsSupported, "Avx support is required to execute this operation.");

        // First pass - process columns
        IDCT8x8_1D_Avx(ref transposedBlock);

        // Second pass - process rows
        transposedBlock.TransposeInplace();
        IDCT8x8_1D_Avx(ref transposedBlock);

        // Applies 1D floating point FDCT inplace
        static void IDCT8x8_1D_Avx(ref Block8x8F block)
        {
            // Even part
            Vector256<float> tmp0 = block.V0;
            Vector256<float> tmp1 = block.V2;
            Vector256<float> tmp2 = block.V4;
            Vector256<float> tmp3 = block.V6;

            Vector256<float> z5 = tmp0;
            Vector256<float> tmp10 = Avx.Add(z5, tmp2);
            Vector256<float> tmp11 = Avx.Subtract(z5, tmp2);

            Vector256<float> mm256_F_1_4142 = Vector256.Create(1.414213562f);
            Vector256<float> tmp13 = Avx.Add(tmp1, tmp3);
            Vector256<float> tmp12 = SimdUtils.HwIntrinsics.MultiplySubtract(tmp13, Avx.Subtract(tmp1, tmp3), mm256_F_1_4142);

            tmp0 = Avx.Add(tmp10, tmp13);
            tmp3 = Avx.Subtract(tmp10, tmp13);
            tmp1 = Avx.Add(tmp11, tmp12);
            tmp2 = Avx.Subtract(tmp11, tmp12);

            // Odd part
            Vector256<float> tmp4 = block.V1;
            Vector256<float> tmp5 = block.V3;
            Vector256<float> tmp6 = block.V5;
            Vector256<float> tmp7 = block.V7;

            Vector256<float> z13 = Avx.Add(tmp6, tmp5);
            Vector256<float> z10 = Avx.Subtract(tmp6, tmp5);
            Vector256<float> z11 = Avx.Add(tmp4, tmp7);
            Vector256<float> z12 = Avx.Subtract(tmp4, tmp7);

            tmp7 = Avx.Add(z11, z13);
            tmp11 = Avx.Multiply(Avx.Subtract(z11, z13), mm256_F_1_4142);

            z5 = Avx.Multiply(Avx.Add(z10, z12), Vector256.Create(1.847759065f));                   // mm256_F_1_8477

            tmp10 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, z12, Vector256.Create(-1.082392200f));   // mm256_F_n1_0823
            tmp12 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, z10, Vector256.Create(-2.613125930f));   // mm256_F_n2_6131

            tmp6 = Avx.Subtract(tmp12, tmp7);
            tmp5 = Avx.Subtract(tmp11, tmp6);
            tmp4 = Avx.Subtract(tmp10, tmp5);

            block.V0 = Avx.Add(tmp0, tmp7);
            block.V7 = Avx.Subtract(tmp0, tmp7);
            block.V1 = Avx.Add(tmp1, tmp6);
            block.V6 = Avx.Subtract(tmp1, tmp6);
            block.V2 = Avx.Add(tmp2, tmp5);
            block.V5 = Avx.Subtract(tmp2, tmp5);
            block.V3 = Avx.Add(tmp3, tmp4);
            block.V4 = Avx.Subtract(tmp3, tmp4);
        }
    }
}
