// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal static partial class FastFloatingPointDCT
    {
        /// <summary>
        /// Gets reciprocal coefficients for jpeg quantization tables calculation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Current FDCT implementation expects its results to be multiplied by
        /// a reciprocal quantization table. Values in this table must be divided
        /// by quantization table values scaled with quality settings.
        /// </para>
        /// <para>
        /// These values were calculates with this formula:
        /// <code>
        /// value[row * 8 + col] = scalefactor[row] * scalefactor[col] * 8;
        /// </code>
        /// Where:
        /// <code>
        /// scalefactor[0] = 1
        /// </code>
        /// <code>
        /// scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
        /// </code>
        /// Values are also scaled by 8 so DCT code won't do unnecessary division.
        /// </para>
        /// </remarks>
        public static ReadOnlySpan<float> DctReciprocalAdjustmentCoefficients => new float[]
        {
            0.125f, 0.09011998f, 0.09567086f, 0.10630376f, 0.125f, 0.15909483f, 0.23096988f, 0.45306373f,
            0.09011998f, 0.064972885f, 0.068974845f, 0.07664074f, 0.09011998f, 0.11470097f, 0.16652f, 0.32664075f,
            0.09567086f, 0.068974845f, 0.07322331f, 0.081361376f, 0.09567086f, 0.121765904f, 0.17677669f, 0.34675997f,
            0.10630376f, 0.07664074f, 0.081361376f, 0.09040392f, 0.10630376f, 0.13529903f, 0.19642374f, 0.38529903f,
            0.125f, 0.09011998f, 0.09567086f, 0.10630376f, 0.125f, 0.15909483f, 0.23096988f, 0.45306373f,
            0.15909483f, 0.11470097f, 0.121765904f, 0.13529903f, 0.15909483f, 0.2024893f, 0.2939689f, 0.5766407f,
            0.23096988f, 0.16652f, 0.17677669f, 0.19642374f, 0.23096988f, 0.2939689f, 0.4267767f, 0.8371526f,
            0.45306373f, 0.32664075f, 0.34675997f, 0.38529903f, 0.45306373f, 0.5766407f, 0.8371526f, 1.642134f,
        };

#pragma warning disable SA1310, SA1311, IDE1006 // naming rules violation warnings
        private static readonly Vector256<float> mm256_F_0_7071 = Vector256.Create(0.707106781f);
        private static readonly Vector256<float> mm256_F_0_3826 = Vector256.Create(0.382683433f);
        private static readonly Vector256<float> mm256_F_0_5411 = Vector256.Create(0.541196100f);
        private static readonly Vector256<float> mm256_F_1_3065 = Vector256.Create(1.306562965f);

        private static readonly Vector128<float> mm128_F_0_7071 = Vector128.Create(0.707106781f);
        private static readonly Vector128<float> mm128_F_0_3826 = Vector128.Create(0.382683433f);
        private static readonly Vector128<float> mm128_F_0_5411 = Vector128.Create(0.541196100f);
        private static readonly Vector128<float> mm128_F_1_3065 = Vector128.Create(1.306562965f);
#pragma warning restore SA1310, SA1311, IDE1006

        /// <summary>
        /// Apply floating point FDCT inplace using simd operations.
        /// </summary>
        /// <param name="block">Input matrix.</param>
        private static void ForwardTransformSimd(ref Block8x8F block)
        {
            DebugGuard.IsTrue(Avx.IsSupported || Sse.IsSupported, "Avx or at least Sse support is required to execute this operation.");

            // First pass - process rows
            block.Transpose();
            if (Avx.IsSupported)
            {
                FDCT8x8_avx(ref block);
            }
            else if (Sse.IsSupported)
            {
                // Left part
                FDCT8x4_sse(ref Unsafe.As<Vector4, Vector128<float>>(ref block.V0L));

                // Right part
                FDCT8x4_sse(ref Unsafe.As<Vector4, Vector128<float>>(ref block.V0R));
            }

            // Second pass - process columns
            block.Transpose();
            if (Avx.IsSupported)
            {
                FDCT8x8_avx(ref block);
            }
            else if (Sse.IsSupported)
            {
                // Left part
                FDCT8x4_sse(ref Unsafe.As<Vector4, Vector128<float>>(ref block.V0L));

                // Right part
                FDCT8x4_sse(ref Unsafe.As<Vector4, Vector128<float>>(ref block.V0R));
            }
        }

        /// <summary>
        /// Apply 1D floating point FDCT inplace using SSE operations on 8x4 part of 8x8 matrix.
        /// </summary>
        /// <remarks>
        /// Requires Sse support.
        /// Must be called on both 8x4 matrix parts for the full FDCT transform.
        /// </remarks>
        /// <param name="blockRef">Input reference to the first </param>
        public static void FDCT8x4_sse(ref Vector128<float> blockRef)
        {
            DebugGuard.IsTrue(Sse.IsSupported, "Sse support is required to execute this operation.");

            Vector128<float> tmp0 = Sse.Add(Unsafe.Add(ref blockRef, 0), Unsafe.Add(ref blockRef, 14));
            Vector128<float> tmp7 = Sse.Subtract(Unsafe.Add(ref blockRef, 0), Unsafe.Add(ref blockRef, 14));
            Vector128<float> tmp1 = Sse.Add(Unsafe.Add(ref blockRef, 2), Unsafe.Add(ref blockRef, 12));
            Vector128<float> tmp6 = Sse.Subtract(Unsafe.Add(ref blockRef, 2), Unsafe.Add(ref blockRef, 12));
            Vector128<float> tmp2 = Sse.Add(Unsafe.Add(ref blockRef, 4), Unsafe.Add(ref blockRef, 10));
            Vector128<float> tmp5 = Sse.Subtract(Unsafe.Add(ref blockRef, 4), Unsafe.Add(ref blockRef, 10));
            Vector128<float> tmp3 = Sse.Add(Unsafe.Add(ref blockRef, 6), Unsafe.Add(ref blockRef, 8));
            Vector128<float> tmp4 = Sse.Subtract(Unsafe.Add(ref blockRef, 6), Unsafe.Add(ref blockRef, 8));

            // Even part
            Vector128<float> tmp10 = Sse.Add(tmp0, tmp3);
            Vector128<float> tmp13 = Sse.Subtract(tmp0, tmp3);
            Vector128<float> tmp11 = Sse.Add(tmp1, tmp2);
            Vector128<float> tmp12 = Sse.Subtract(tmp1, tmp2);

            Unsafe.Add(ref blockRef, 0) = Sse.Add(tmp10, tmp11);
            Unsafe.Add(ref blockRef, 8) = Sse.Subtract(tmp10, tmp11);

            Vector128<float> z1 = Sse.Multiply(Sse.Add(tmp12, tmp13), mm128_F_0_7071);
            Unsafe.Add(ref blockRef, 4) = Sse.Add(tmp13, z1);
            Unsafe.Add(ref blockRef, 12) = Sse.Subtract(tmp13, z1);

            // Odd part
            tmp10 = Sse.Add(tmp4, tmp5);
            tmp11 = Sse.Add(tmp5, tmp6);
            tmp12 = Sse.Add(tmp6, tmp7);

            Vector128<float> z5 = Sse.Multiply(Sse.Subtract(tmp10, tmp12), mm128_F_0_3826);
            Vector128<float> z2 = Sse.Add(Sse.Multiply(mm128_F_0_5411, tmp10), z5);
            Vector128<float> z4 = Sse.Add(Sse.Multiply(mm128_F_1_3065, tmp12), z5);
            Vector128<float> z3 = Sse.Multiply(tmp11, mm128_F_0_7071);

            Vector128<float> z11 = Sse.Add(tmp7, z3);
            Vector128<float> z13 = Sse.Subtract(tmp7, z3);

            Unsafe.Add(ref blockRef, 10) = Sse.Add(z13, z2);
            Unsafe.Add(ref blockRef, 6) = Sse.Subtract(z13, z2);
            Unsafe.Add(ref blockRef, 2) = Sse.Add(z11, z4);
            Unsafe.Add(ref blockRef, 14) = Sse.Subtract(z11, z4);
        }

        /// <summary>
        /// Apply 1D floating point FDCT inplace using AVX operations on 8x8 matrix.
        /// </summary>
        /// <remarks>
        /// Requires Avx support.
        /// </remarks>
        /// <param name="block">Input matrix.</param>
        public static void FDCT8x8_avx(ref Block8x8F block)
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
            Vector256<float> z2 = Avx.Add(Avx.Multiply(mm256_F_0_5411, tmp10), z5);
            Vector256<float> z4 = Avx.Add(Avx.Multiply(mm256_F_1_3065, tmp12), z5);
            Vector256<float> z3 = Avx.Multiply(tmp11, mm256_F_0_7071);

            Vector256<float> z11 = Avx.Add(tmp7, z3);
            Vector256<float> z13 = Avx.Subtract(tmp7, z3);

            block.V5 = Avx.Add(z13, z2);
            block.V3 = Avx.Subtract(z13, z2);
            block.V1 = Avx.Add(z11, z4);
            block.V7 = Avx.Subtract(z11, z4);
        }
    }
}
#endif
