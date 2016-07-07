// <copyright file="FDCT.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    internal class FDCT
    {
        // Trigonometric constants in 13-bit fixed point format.
        private const int fix_0_298631336 = 2446;
        private const int fix_0_390180644 = 3196;
        private const int fix_0_541196100 = 4433;
        private const int fix_0_765366865 = 6270;
        private const int fix_0_899976223 = 7373;
        private const int fix_1_175875602 = 9633;
        private const int fix_1_501321110 = 12299;
        private const int fix_1_847759065 = 15137;
        private const int fix_1_961570560 = 16069;
        private const int fix_2_053119869 = 16819;
        private const int fix_2_562915447 = 20995;
        private const int fix_3_072711026 = 25172;
        private const int constBits = 13;
        private const int pass1Bits = 2;
        private const int centerJSample = 128;

        // fdct performs a forward DCT on an 8x8 block of coefficients, including a
        // level shift.
        public static void Transform(Block b)
        {
            // Pass 1: process rows.
            for (int y = 0; y < 8; y++)
            {
                int y8 = y * 8;

                int x0 = b[y8 + 0];
                int x1 = b[y8 + 1];
                int x2 = b[y8 + 2];
                int x3 = b[y8 + 3];
                int x4 = b[y8 + 4];
                int x5 = b[y8 + 5];
                int x6 = b[y8 + 6];
                int x7 = b[y8 + 7];

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

                b[y8] = (tmp10 + tmp11 - 8 * centerJSample) << pass1Bits;
                b[y8 + 4] = (tmp10 - tmp11) << pass1Bits;
                int z1 = (tmp12 + tmp13) * fix_0_541196100;
                z1 += 1 << (constBits - pass1Bits - 1);
                b[y8 + 2] = (z1 + tmp12 * fix_0_765366865) >> (constBits - pass1Bits);
                b[y8 + 6] = (z1 - tmp13 * fix_1_847759065) >> (constBits - pass1Bits);

                tmp10 = tmp0 + tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp0 + tmp2;
                tmp13 = tmp1 + tmp3;
                z1 = (tmp12 + tmp13) * fix_1_175875602;
                z1 += 1 << (constBits - pass1Bits - 1);
                tmp0 = tmp0 * fix_1_501321110;
                tmp1 = tmp1 * fix_3_072711026;
                tmp2 = tmp2 * fix_2_053119869;
                tmp3 = tmp3 * fix_0_298631336;
                tmp10 = tmp10 * -fix_0_899976223;
                tmp11 = tmp11 * -fix_2_562915447;
                tmp12 = tmp12 * -fix_0_390180644;
                tmp13 = tmp13 * -fix_1_961570560;

                tmp12 += z1;
                tmp13 += z1;
                b[y8 + 1] = (tmp0 + tmp10 + tmp12) >> (constBits - pass1Bits);
                b[y8 + 3] = (tmp1 + tmp11 + tmp13) >> (constBits - pass1Bits);
                b[y8 + 5] = (tmp2 + tmp11 + tmp12) >> (constBits - pass1Bits);
                b[y8 + 7] = (tmp3 + tmp10 + tmp13) >> (constBits - pass1Bits);
            }

            // Pass 2: process columns.
            // We remove pass1Bits scaling, but leave results scaled up by an overall factor of 8.
            for (int x = 0; x < 8; x++)
            {
                int tmp0 = b[x] + b[56 + x];
                int tmp1 = b[8 + x] + b[48 + x];
                int tmp2 = b[16 + x] + b[40 + x];
                int tmp3 = b[24 + x] + b[32 + x];

                int tmp10 = tmp0 + tmp3 + (1 << (pass1Bits - 1));
                int tmp12 = tmp0 - tmp3;
                int tmp11 = tmp1 + tmp2;
                int tmp13 = tmp1 - tmp2;

                tmp0 = b[x] - b[56 + x];
                tmp1 = b[8 + x] - b[48 + x];
                tmp2 = b[16 + x] - b[40 + x];
                tmp3 = b[24 + x] - b[32 + x];

                b[x] = (tmp10 + tmp11) >> pass1Bits;
                b[32 + x] = (tmp10 - tmp11) >> pass1Bits;

                int z1 = (tmp12 + tmp13) * fix_0_541196100;
                z1 += 1 << (constBits + pass1Bits - 1);
                b[16 + x] = (z1 + tmp12 * fix_0_765366865) >> (constBits + pass1Bits);
                b[48 + x] = (z1 - tmp13 * fix_1_847759065) >> (constBits + pass1Bits);

                tmp10 = tmp0 + tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp0 + tmp2;
                tmp13 = tmp1 + tmp3;
                z1 = (tmp12 + tmp13) * fix_1_175875602;
                z1 += 1 << (constBits + pass1Bits - 1);
                tmp0 = tmp0 * fix_1_501321110;
                tmp1 = tmp1 * fix_3_072711026;
                tmp2 = tmp2 * fix_2_053119869;
                tmp3 = tmp3 * fix_0_298631336;
                tmp10 = tmp10 * -fix_0_899976223;
                tmp11 = tmp11 * -fix_2_562915447;
                tmp12 = tmp12 * -fix_0_390180644;
                tmp13 = tmp13 * -fix_1_961570560;

                tmp12 += z1;
                tmp13 += z1;
                b[8 + x] = (tmp0 + tmp10 + tmp12) >> (constBits + pass1Bits);
                b[24 + x] = (tmp1 + tmp11 + tmp13) >> (constBits + pass1Bits);
                b[40 + x] = (tmp2 + tmp11 + tmp12) >> (constBits + pass1Bits);
                b[56 + x] = (tmp3 + tmp10 + tmp13) >> (constBits + pass1Bits);
            }
        }
    }
}
