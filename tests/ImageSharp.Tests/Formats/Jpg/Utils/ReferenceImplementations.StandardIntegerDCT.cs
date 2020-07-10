// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static partial class ReferenceImplementations
    {
        /// <summary>
        /// TODO: produces really bad results for bigger values!
        ///
        /// Contains the "original" golang based DCT/IDCT implementations as reference implementations.
        /// 1. ===== Forward DCT =====
        /// **** The original golang source claims:
        /// It is based on the code in jfdctint.c from the Independent JPEG Group,
        /// found at http://www.ijg.org/files/jpegsrc.v8c.tar.gz.
        ///
        /// **** Could be found here as well:
        /// https://github.com/mozilla/mozjpeg/blob/master/jfdctint.c
        ///
        /// 2. ===== Inverse DCT =====
        ///
        /// The golang source claims:
        /// http://standards.iso.org/ittf/PubliclyAvailableStandards/ISO_IEC_13818-4_2004_Conformance_Testing/Video/verifier/mpeg2decode_960109.tar.gz
        /// The referenced MPEG2 code claims:
        /// /**********************************************************/
        /// /* inverse two dimensional DCT, Chen-Wang algorithm       */
        /// /* (cf. IEEE ASSP-32, pp. 803-816, Aug. 1984)             */
        /// /* 32-bit integer arithmetic (8 bit coefficients)         */
        /// /* 11 mults, 29 adds per DCT                              */
        /// /*                                      sE, 18.8.91       */
        /// /**********************************************************/
        /// /* coefficients extended to 12 bit for IEEE1180-1990      */
        /// /* compliance                           sE,  2.1.94       */
        /// /**********************************************************/
        ///
        /// **** The code looks pretty similar to the standard libjpeg IDCT, but without quantization:
        /// https://github.com/mozilla/mozjpeg/blob/master/jidctint.c
        /// </summary>
        public static class StandardIntegerDCT
        {
            private const int Fix_0_298631336 = 2446;
            private const int Fix_0_390180644 = 3196;
            private const int Fix_0_541196100 = 4433;
            private const int Fix_0_765366865 = 6270;
            private const int Fix_0_899976223 = 7373;
            private const int Fix_1_175875602 = 9633;
            private const int Fix_1_501321110 = 12299;
            private const int Fix_1_847759065 = 15137;
            private const int Fix_1_961570560 = 16069;
            private const int Fix_2_053119869 = 16819;
            private const int Fix_2_562915447 = 20995;
            private const int Fix_3_072711026 = 25172;

            /// <summary>
            /// The number of bits
            /// </summary>
            private const int Bits = 13;

            /// <summary>
            /// The number of bits to shift by on the first pass.
            /// </summary>
            private const int Pass1Bits = 2;

            /// <summary>
            /// The value to shift by
            /// </summary>
            private const int CenterJSample = 128;

            public static Block8x8 Subtract128_TransformFDCT_Upscale8(ref Block8x8 block)
            {
                var temp = new int[Block8x8.Size];
                block.CopyTo(temp);
                Subtract128_TransformFDCT_Upscale8_Inplace(temp);
                var result = default(Block8x8);
                result.LoadFrom(temp);
                return result;
            }

            // [Obsolete("Looks like this method produces really bad results for bigger values!")]
            public static Block8x8 TransformIDCT(ref Block8x8 block)
            {
                var temp = new int[Block8x8.Size];
                block.CopyTo(temp);
                TransformIDCTInplace(temp);
                var result = default(Block8x8);
                result.LoadFrom(temp);
                return result;
            }

            /// <summary>
            /// Performs a forward DCT on an 8x8 block of coefficients, including a level shift.
            /// Leave results scaled up by an overall factor of 8.
            /// </summary>
            /// <param name="block">The block of coefficients.</param>
            public static void Subtract128_TransformFDCT_Upscale8_Inplace(Span<int> block)
            {
                // Pass 1: process rows.
                for (int y = 0; y < 8; y++)
                {
                    int y8 = y * 8;

                    int x0 = block[y8];
                    int x1 = block[y8 + 1];
                    int x2 = block[y8 + 2];
                    int x3 = block[y8 + 3];
                    int x4 = block[y8 + 4];
                    int x5 = block[y8 + 5];
                    int x6 = block[y8 + 6];
                    int x7 = block[y8 + 7];

                    int tmp0 = x0 + x7;
                    int tmp1 = x1 + x6;
                    int tmp2 = x2 + x5;
                    int tmp3 = x3 + x4;

                    int tmp10 = tmp0 + tmp3;
                    int tmp12 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp13 = tmp1 - tmp2;

                    tmp0 = x0 - x7;
                    tmp1 = x1 - x6;
                    tmp2 = x2 - x5;
                    tmp3 = x3 - x4;

                    block[y8] = (tmp10 + tmp11 - (8 * CenterJSample)) << Pass1Bits;
                    block[y8 + 4] = (tmp10 - tmp11) << Pass1Bits;
                    int z1 = (tmp12 + tmp13) * Fix_0_541196100;
                    z1 += 1 << (Bits - Pass1Bits - 1);
                    block[y8 + 2] = (z1 + (tmp12 * Fix_0_765366865)) >> (Bits - Pass1Bits);
                    block[y8 + 6] = (z1 - (tmp13 * Fix_1_847759065)) >> (Bits - Pass1Bits);

                    tmp10 = tmp0 + tmp3;
                    tmp11 = tmp1 + tmp2;
                    tmp12 = tmp0 + tmp2;
                    tmp13 = tmp1 + tmp3;
                    z1 = (tmp12 + tmp13) * Fix_1_175875602;
                    z1 += 1 << (Bits - Pass1Bits - 1);
                    tmp0 = tmp0 * Fix_1_501321110;
                    tmp1 = tmp1 * Fix_3_072711026;
                    tmp2 = tmp2 * Fix_2_053119869;
                    tmp3 = tmp3 * Fix_0_298631336;
                    tmp10 = tmp10 * -Fix_0_899976223;
                    tmp11 = tmp11 * -Fix_2_562915447;
                    tmp12 = tmp12 * -Fix_0_390180644;
                    tmp13 = tmp13 * -Fix_1_961570560;

                    tmp12 += z1;
                    tmp13 += z1;
                    block[y8 + 1] = (tmp0 + tmp10 + tmp12) >> (Bits - Pass1Bits);
                    block[y8 + 3] = (tmp1 + tmp11 + tmp13) >> (Bits - Pass1Bits);
                    block[y8 + 5] = (tmp2 + tmp11 + tmp12) >> (Bits - Pass1Bits);
                    block[y8 + 7] = (tmp3 + tmp10 + tmp13) >> (Bits - Pass1Bits);
                }

                // Pass 2: process columns.
                // We remove pass1Bits scaling, but leave results scaled up by an overall factor of 8.
                for (int x = 0; x < 8; x++)
                {
                    int tmp0 = block[x] + block[56 + x];
                    int tmp1 = block[8 + x] + block[48 + x];
                    int tmp2 = block[16 + x] + block[40 + x];
                    int tmp3 = block[24 + x] + block[32 + x];

                    int tmp10 = tmp0 + tmp3 + (1 << (Pass1Bits - 1));
                    int tmp12 = tmp0 - tmp3;
                    int tmp11 = tmp1 + tmp2;
                    int tmp13 = tmp1 - tmp2;

                    tmp0 = block[x] - block[56 + x];
                    tmp1 = block[8 + x] - block[48 + x];
                    tmp2 = block[16 + x] - block[40 + x];
                    tmp3 = block[24 + x] - block[32 + x];

                    block[x] = (tmp10 + tmp11) >> Pass1Bits;
                    block[32 + x] = (tmp10 - tmp11) >> Pass1Bits;

                    int z1 = (tmp12 + tmp13) * Fix_0_541196100;
                    z1 += 1 << (Bits + Pass1Bits - 1);
                    block[16 + x] = (z1 + (tmp12 * Fix_0_765366865)) >> (Bits + Pass1Bits);
                    block[48 + x] = (z1 - (tmp13 * Fix_1_847759065)) >> (Bits + Pass1Bits);

                    tmp10 = tmp0 + tmp3;
                    tmp11 = tmp1 + tmp2;
                    tmp12 = tmp0 + tmp2;
                    tmp13 = tmp1 + tmp3;
                    z1 = (tmp12 + tmp13) * Fix_1_175875602;
                    z1 += 1 << (Bits + Pass1Bits - 1);
                    tmp0 = tmp0 * Fix_1_501321110;
                    tmp1 = tmp1 * Fix_3_072711026;
                    tmp2 = tmp2 * Fix_2_053119869;
                    tmp3 = tmp3 * Fix_0_298631336;
                    tmp10 = tmp10 * -Fix_0_899976223;
                    tmp11 = tmp11 * -Fix_2_562915447;
                    tmp12 = tmp12 * -Fix_0_390180644;
                    tmp13 = tmp13 * -Fix_1_961570560;

                    tmp12 += z1;
                    tmp13 += z1;
                    block[8 + x] = (tmp0 + tmp10 + tmp12) >> (Bits + Pass1Bits);
                    block[24 + x] = (tmp1 + tmp11 + tmp13) >> (Bits + Pass1Bits);
                    block[40 + x] = (tmp2 + tmp11 + tmp12) >> (Bits + Pass1Bits);
                    block[56 + x] = (tmp3 + tmp10 + tmp13) >> (Bits + Pass1Bits);
                }
            }

            private const int W1 = 2841; // 2048*sqrt(2)*cos(1*pi/16)
            private const int W2 = 2676; // 2048*sqrt(2)*cos(2*pi/16)
            private const int W3 = 2408; // 2048*sqrt(2)*cos(3*pi/16)
            private const int W5 = 1609; // 2048*sqrt(2)*cos(5*pi/16)
            private const int W6 = 1108; // 2048*sqrt(2)*cos(6*pi/16)
            private const int W7 = 565;  // 2048*sqrt(2)*cos(7*pi/16)

            private const int W1pw7 = W1 + W7;
            private const int W1mw7 = W1 - W7;
            private const int W2pw6 = W2 + W6;
            private const int W2mw6 = W2 - W6;
            private const int W3pw5 = W3 + W5;
            private const int W3mw5 = W3 - W5;

            private const int R2 = 181; // 256/sqrt(2)

            /// <summary>
            /// Performs a 2-D Inverse Discrete Cosine Transformation.
            /// <para>
            /// The input coefficients should already have been multiplied by the
            /// appropriate quantization table. We use fixed-point computation, with the
            /// number of bits for the fractional component varying over the intermediate
            /// stages.
            /// </para>
            /// For more on the actual algorithm, see Z. Wang, "Fast algorithms for the
            /// discrete W transform and for the discrete Fourier transform", IEEE Trans. on
            /// ASSP, Vol. ASSP- 32, pp. 803-816, Aug. 1984.
            /// </summary>
            /// <param name="src">The source block of coefficients</param>
            public static void TransformIDCTInplace(Span<int> src)
            {
                // Horizontal 1-D IDCT.
                for (int y = 0; y < 8; y++)
                {
                    int y8 = y * 8;

                    // If all the AC components are zero, then the IDCT is trivial.
                    if (src[y8 + 1] == 0 && src[y8 + 2] == 0 && src[y8 + 3] == 0 &&
                        src[y8 + 4] == 0 && src[y8 + 5] == 0 && src[y8 + 6] == 0 && src[y8 + 7] == 0)
                    {
                        int dc = src[y8 + 0] << 3;
                        src[y8 + 0] = dc;
                        src[y8 + 1] = dc;
                        src[y8 + 2] = dc;
                        src[y8 + 3] = dc;
                        src[y8 + 4] = dc;
                        src[y8 + 5] = dc;
                        src[y8 + 6] = dc;
                        src[y8 + 7] = dc;
                        continue;
                    }

                    // Prescale.
                    int x0 = (src[y8 + 0] << 11) + 128;
                    int x1 = src[y8 + 4] << 11;
                    int x2 = src[y8 + 6];
                    int x3 = src[y8 + 2];
                    int x4 = src[y8 + 1];
                    int x5 = src[y8 + 7];
                    int x6 = src[y8 + 5];
                    int x7 = src[y8 + 3];

                    // Stage 1.
                    int x8 = W7 * (x4 + x5);
                    x4 = x8 + (W1mw7 * x4);
                    x5 = x8 - (W1pw7 * x5);
                    x8 = W3 * (x6 + x7);
                    x6 = x8 - (W3mw5 * x6);
                    x7 = x8 - (W3pw5 * x7);

                    // Stage 2.
                    x8 = x0 + x1;
                    x0 -= x1;
                    x1 = W6 * (x3 + x2);
                    x2 = x1 - (W2pw6 * x2);
                    x3 = x1 + (W2mw6 * x3);
                    x1 = x4 + x6;
                    x4 -= x6;
                    x6 = x5 + x7;
                    x5 -= x7;

                    // Stage 3.
                    x7 = x8 + x3;
                    x8 -= x3;
                    x3 = x0 + x2;
                    x0 -= x2;
                    x2 = ((R2 * (x4 + x5)) + 128) >> 8;
                    x4 = ((R2 * (x4 - x5)) + 128) >> 8;

                    // Stage 4.
                    src[y8 + 0] = (x7 + x1) >> 8;
                    src[y8 + 1] = (x3 + x2) >> 8;
                    src[y8 + 2] = (x0 + x4) >> 8;
                    src[y8 + 3] = (x8 + x6) >> 8;
                    src[y8 + 4] = (x8 - x6) >> 8;
                    src[y8 + 5] = (x0 - x4) >> 8;
                    src[y8 + 6] = (x3 - x2) >> 8;
                    src[y8 + 7] = (x7 - x1) >> 8;
                }

                // Vertical 1-D IDCT.
                for (int x = 0; x < 8; x++)
                {
                    // Similar to the horizontal 1-D IDCT case, if all the AC components are zero, then the IDCT is trivial.
                    // However, after performing the horizontal 1-D IDCT, there are typically non-zero AC components, so
                    // we do not bother to check for the all-zero case.

                    // Prescale.
                    int y0 = (src[x] << 8) + 8192;
                    int y1 = src[32 + x] << 8;
                    int y2 = src[48 + x];
                    int y3 = src[16 + x];
                    int y4 = src[8 + x];
                    int y5 = src[56 + x];
                    int y6 = src[40 + x];
                    int y7 = src[24 + x];

                    // Stage 1.
                    int y8 = (W7 * (y4 + y5)) + 4;
                    y4 = (y8 + (W1mw7 * y4)) >> 3;
                    y5 = (y8 - (W1pw7 * y5)) >> 3;
                    y8 = (W3 * (y6 + y7)) + 4;
                    y6 = (y8 - (W3mw5 * y6)) >> 3;
                    y7 = (y8 - (W3pw5 * y7)) >> 3;

                    // Stage 2.
                    y8 = y0 + y1;
                    y0 -= y1;
                    y1 = (W6 * (y3 + y2)) + 4;
                    y2 = (y1 - (W2pw6 * y2)) >> 3;
                    y3 = (y1 + (W2mw6 * y3)) >> 3;
                    y1 = y4 + y6;
                    y4 -= y6;
                    y6 = y5 + y7;
                    y5 -= y7;

                    // Stage 3.
                    y7 = y8 + y3;
                    y8 -= y3;
                    y3 = y0 + y2;
                    y0 -= y2;
                    y2 = ((R2 * (y4 + y5)) + 128) >> 8;
                    y4 = ((R2 * (y4 - y5)) + 128) >> 8;

                    // Stage 4.
                    src[x] = (y7 + y1) >> 14;
                    src[8 + x] = (y3 + y2) >> 14;
                    src[16 + x] = (y0 + y4) >> 14;
                    src[24 + x] = (y8 + y6) >> 14;
                    src[32 + x] = (y8 - y6) >> 14;
                    src[40 + x] = (y0 - y4) >> 14;
                    src[48 + x] = (y3 - y2) >> 14;
                    src[56 + x] = (y7 - y1) >> 14;
                }
            }
        }
    }
}
