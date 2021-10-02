// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal static partial class FastFloatingPointDCT
    {
#pragma warning disable SA1310, SA1311, IDE1006 // naming rules violation warnings
        private static readonly Vector256<float> mm256_F_0_7071 = Vector256.Create(0.707106781f);
        private static readonly Vector256<float> mm256_F_0_3826 = Vector256.Create(0.382683433f);
        private static readonly Vector256<float> mm256_F_0_5411 = Vector256.Create(0.541196100f);
        private static readonly Vector256<float> mm256_F_1_3065 = Vector256.Create(1.306562965f);

        private static readonly Vector256<float> mm256_F_1_1758 = Vector256.Create(1.175876f);
        private static readonly Vector256<float> mm256_F_n1_9615 = Vector256.Create(-1.961570560f);
        private static readonly Vector256<float> mm256_F_n0_3901 = Vector256.Create(-0.390180644f);
        private static readonly Vector256<float> mm256_F_n0_8999 = Vector256.Create(-0.899976223f);
        private static readonly Vector256<float> mm256_F_n2_5629 = Vector256.Create(-2.562915447f);
        private static readonly Vector256<float> mm256_F_0_2986 = Vector256.Create(0.298631336f);
        private static readonly Vector256<float> mm256_F_2_0531 = Vector256.Create(2.053119869f);
        private static readonly Vector256<float> mm256_F_3_0727 = Vector256.Create(3.072711026f);
        private static readonly Vector256<float> mm256_F_1_5013 = Vector256.Create(1.501321110f);
        private static readonly Vector256<float> mm256_F_n1_8477 = Vector256.Create(-1.847759065f);
        private static readonly Vector256<float> mm256_F_0_7653 = Vector256.Create(0.765366865f);
#pragma warning restore SA1310, SA1311, IDE1006

        /// <summary>
        /// Apply floating point FDCT inplace using simd operations.
        /// </summary>
        /// <param name="block">Input matrix.</param>
        private static void ForwardTransform_Avx(ref Block8x8F block)
        {
            DebugGuard.IsTrue(Avx.IsSupported, "Avx support is required to execute this operation.");

            // First pass - process rows
            block.TransposeInplace();
            FDCT8x8_Avx(ref block);

            // Second pass - process columns
            block.TransposeInplace();
            FDCT8x8_Avx(ref block);
        }

        /// <summary>
        /// Apply 1D floating point FDCT inplace using AVX operations on 8x8 matrix.
        /// </summary>
        /// <remarks>
        /// Requires Avx support.
        /// </remarks>
        /// <param name="block">Input matrix.</param>
        public static void FDCT8x8_Avx(ref Block8x8F block)
        {
            DebugGuard.IsTrue(Avx.IsSupported, "Avx support is required to execute this operation.");

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

            Vector256<float> z1 = Avx.Multiply(Avx.Add(tmp12, tmp13), mm256_F_0_7071);
            block.V2 = Avx.Add(tmp13, z1);
            block.V6 = Avx.Subtract(tmp13, z1);

            // Odd part
            tmp10 = Avx.Add(tmp4, tmp5);
            tmp11 = Avx.Add(tmp5, tmp6);
            tmp12 = Avx.Add(tmp6, tmp7);

            Vector256<float> z5 = Avx.Multiply(Avx.Subtract(tmp10, tmp12), mm256_F_0_3826);
            Vector256<float> z2 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, mm256_F_0_5411, tmp10);
            Vector256<float> z4 = SimdUtils.HwIntrinsics.MultiplyAdd(z5, mm256_F_1_3065, tmp12);
            Vector256<float> z3 = Avx.Multiply(tmp11, mm256_F_0_7071);

            Vector256<float> z11 = Avx.Add(tmp7, z3);
            Vector256<float> z13 = Avx.Subtract(tmp7, z3);

            block.V5 = Avx.Add(z13, z2);
            block.V3 = Avx.Subtract(z13, z2);
            block.V1 = Avx.Add(z11, z4);
            block.V7 = Avx.Subtract(z11, z4);
        }

        /// <summary>
        /// Combined operation of <see cref="IDCT8x4_LeftPart(ref Block8x8F, ref Block8x8F)"/> and <see cref="IDCT8x4_RightPart(ref Block8x8F, ref Block8x8F)"/>
        /// using AVX commands.
        /// </summary>
        /// <param name="s">Source</param>
        /// <param name="d">Destination</param>
        public static void IDCT8x8_Avx(ref Block8x8F s, ref Block8x8F d)
        {
            Debug.Assert(Avx.IsSupported, "AVX is required to execute this method");

            Vector256<float> my1 = s.V1;
            Vector256<float> my7 = s.V7;
            Vector256<float> mz0 = Avx.Add(my1, my7);

            Vector256<float> my3 = s.V3;
            Vector256<float> mz2 = Avx.Add(my3, my7);
            Vector256<float> my5 = s.V5;
            Vector256<float> mz1 = Avx.Add(my3, my5);
            Vector256<float> mz3 = Avx.Add(my1, my5);

            Vector256<float> mz4 = Avx.Multiply(Avx.Add(mz0, mz1), mm256_F_1_1758);

            mz2 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, mz2, mm256_F_n1_9615);
            mz3 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, mz3, mm256_F_n0_3901);
            mz0 = Avx.Multiply(mz0, mm256_F_n0_8999);
            mz1 = Avx.Multiply(mz1, mm256_F_n2_5629);

            Vector256<float> mb3 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz0, my7, mm256_F_0_2986), mz2);
            Vector256<float> mb2 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz1, my5, mm256_F_2_0531), mz3);
            Vector256<float> mb1 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz1, my3, mm256_F_3_0727), mz2);
            Vector256<float> mb0 = Avx.Add(SimdUtils.HwIntrinsics.MultiplyAdd(mz0, my1, mm256_F_1_5013), mz3);

            Vector256<float> my2 = s.V2;
            Vector256<float> my6 = s.V6;
            mz4 = Avx.Multiply(Avx.Add(my2, my6), mm256_F_0_5411);
            Vector256<float> my0 = s.V0;
            Vector256<float> my4 = s.V4;
            mz0 = Avx.Add(my0, my4);
            mz1 = Avx.Subtract(my0, my4);
            mz2 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, my6, mm256_F_n1_8477);
            mz3 = SimdUtils.HwIntrinsics.MultiplyAdd(mz4, my2, mm256_F_0_7653);

            my0 = Avx.Add(mz0, mz3);
            my3 = Avx.Subtract(mz0, mz3);
            my1 = Avx.Add(mz1, mz2);
            my2 = Avx.Subtract(mz1, mz2);

            d.V0 = Avx.Add(my0, mb0);
            d.V7 = Avx.Subtract(my0, mb0);
            d.V1 = Avx.Add(my1, mb1);
            d.V6 = Avx.Subtract(my1, mb1);
            d.V2 = Avx.Add(my2, mb2);
            d.V5 = Avx.Subtract(my2, mb2);
            d.V3 = Avx.Add(my3, mb3);
            d.V4 = Avx.Subtract(my3, mb3);
        }
    }
}
#endif
