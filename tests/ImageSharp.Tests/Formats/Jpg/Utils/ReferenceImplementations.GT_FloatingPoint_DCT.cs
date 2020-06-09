// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static partial class ReferenceImplementations
    {
        /// <summary>
        /// Non-optimized method ported from:
        /// https://github.com/norishigefukushima/dct_simd/blob/master/dct/dct8x8_simd.cpp#L446
        ///
        /// *** Paper ***
        /// Plonka, Gerlind, and Manfred Tasche. "Fast and numerically stable algorithms for discrete cosine transforms." Linear algebra and its applications 394 (2005) : 309 - 345.
        /// </summary>
        internal static class GT_FloatingPoint_DCT
        {
            public static void Idct81d_GT(Span<float> src, Span<float> dst)
            {
                for (int i = 0; i < 8; i++)
                {
                    float mx00 = 1.4142135623731f * src[0];
                    float mx01 = (1.38703984532215f * src[1]) + (0.275899379282943f * src[7]);
                    float mx02 = (1.30656296487638f * src[2]) + (0.541196100146197f * src[6]);
                    float mx03 = (1.17587560241936f * src[3]) + (0.785694958387102f * src[5]);
                    float mx04 = 1.4142135623731f * src[4];
                    float mx05 = (-0.785694958387102f * src[3]) + (1.17587560241936f * src[5]);
                    float mx06 = (0.541196100146197f * src[2]) - (1.30656296487638f * src[6]);
                    float mx07 = (-0.275899379282943f * src[1]) + (1.38703984532215f * src[7]);
                    float mx09 = mx00 + mx04;
                    float mx0a = mx01 + mx03;
                    float mx0b = 1.4142135623731f * mx02;
                    float mx0c = mx00 - mx04;
                    float mx0d = mx01 - mx03;
                    float mx0e = 0.353553390593274f * (mx09 - mx0b);
                    float mx0f = 0.353553390593274f * (mx0c + mx0d);
                    float mx10 = 0.353553390593274f * (mx0c - mx0d);
                    float mx11 = 1.4142135623731f * mx06;
                    float mx12 = mx05 + mx07;
                    float mx13 = mx05 - mx07;
                    float mx14 = 0.353553390593274f * (mx11 + mx12);
                    float mx15 = 0.353553390593274f * (mx11 - mx12);
                    float mx16 = 0.5f * mx13;
                    dst[0] = (0.25f * (mx09 + mx0b)) + (0.353553390593274f * mx0a);
                    dst[1] = 0.707106781186547f * (mx0f + mx15);
                    dst[2] = 0.707106781186547f * (mx0f - mx15);
                    dst[3] = 0.707106781186547f * (mx0e + mx16);
                    dst[4] = 0.707106781186547f * (mx0e - mx16);
                    dst[5] = 0.707106781186547f * (mx10 - mx14);
                    dst[6] = 0.707106781186547f * (mx10 + mx14);
                    dst[7] = (0.25f * (mx09 + mx0b)) - (0.353553390593274f * mx0a);
                    dst = dst.Slice(8);
                    src = src.Slice(8);
                }
            }

            public static void IDCT8x8GT(Span<float> s, Span<float> d)
            {
                Idct81d_GT(s, d);

                Transpose8x8(d);

                Idct81d_GT(d, d);

                Transpose8x8(d);
            }
        }
    }
}
