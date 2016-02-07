namespace ImageProcessorCore.Formats.Jpg.New
{
    using System;

    /// <summary>
    /// <see href="https://svn.apache.org/repos/asf/commons/proper/imaging/trunk/src/main/java/org/apache/commons/imaging/formats/jpeg/decoder/Dct.java"/>
    /// </summary>
    public class Dct
    {
        private const int Fix0298631336 = 2446;
        private const int Fix0390180644 = 3196;
        private const int Fix0541196100 = 4433;
        private const int Fix0765366865 = 6270;
        private const int Fix0899976223 = 7373;
        private const int Fix1175875602 = 9633;
        private const int Fix1501321110 = 12299;
        private const int Fix1847759065 = 15137;
        private const int Fix1961570560 = 16069;
        private const int Fix2053119869 = 16819;
        private const int Fix2562915447 = 20995;
        private const int Fix3072711026 = 25172;
        private const int ConstBits = 13;

        private const int Pass1Bits = 2;

        private const int CenterJSample = 128;


        private static float A1 = (float)Math.Cos(2.0 * Math.PI / 8.0);
        private static float A2 = (float)(Math.Cos(Math.PI / 8.0) - Math.Cos(3.0 * Math.PI / 8.0));
        private static float A3 = A1;
        private static float A4 = (float)(Math.Cos(Math.PI / 8.0) + Math.Cos(3.0 * Math.PI / 8.0));
        private static float A5 = (float)(Math.Cos(3.0 * Math.PI / 8.0));

        private static float C2 = (float)(2.0 * Math.Cos(Math.PI / 8));
        private static float C4 = (float)(2.0 * Math.Cos(2 * Math.PI / 8));
        private static float C6 = (float)(2.0 * Math.Cos(3 * Math.PI / 8));
        private static float Q = C2 - C6;
        private static float R = C2 + C6;


        public void Fdct(byte[] block)
        {
            // Pass 1: process rows.
            for (int y = 0; y < 8; y++)
            {
                var x0 = block[y * 8 + 0];
                var x1 = block[y * 8 + 1];
                var x2 = block[y * 8 + 2];
                var x3 = block[y * 8 + 3];
                var x4 = block[y * 8 + 4];
                var x5 = block[y * 8 + 5];
                var x6 = block[y * 8 + 6];
                var x7 = block[y * 8 + 7];

                var tmp0 = x0 + x7;
                var tmp1 = x1 + x6;
                var tmp2 = x2 + x5;
                var tmp3 = x3 + x4;

                var tmp10 = tmp0 + tmp3;
                var tmp12 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp13 = tmp1 - tmp2;

                tmp0 = x0 - x7;
                tmp1 = x1 - x6;
                tmp2 = x2 - x5;
                tmp3 = x3 - x4;

                block[y * 8 + 0] = (byte)((tmp10 + tmp11 - 8 * CenterJSample) << Pass1Bits);
                block[y * 8 + 4] = (byte)((tmp10 - tmp11) << Pass1Bits);

                var z1 = (tmp12 + tmp13) * Fix0541196100;
                z1 += 1 << (ConstBits - Pass1Bits - 1);

                block[y * 8 + 2] = (byte)((z1 + tmp12 * Fix0765366865) >> (ConstBits - Pass1Bits));
                block[y * 8 + 6] = (byte)((z1 - tmp13 * Fix1847759065) >> (ConstBits - Pass1Bits));

                tmp10 = tmp0 + tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp0 + tmp2;
                tmp13 = tmp1 + tmp3;

                z1 = (tmp12 + tmp13) * Fix1175875602;
                z1 += 1 << (ConstBits - Pass1Bits - 1);

                tmp0 = tmp0 * Fix1501321110;
                tmp1 = tmp1 * Fix3072711026;
                tmp2 = tmp2 * Fix2053119869;
                tmp3 = tmp3 * Fix0298631336;
                tmp10 = tmp10 * -Fix0899976223;
                tmp11 = tmp11 * -Fix2562915447;
                tmp12 = tmp12 * -Fix0390180644;
                tmp13 = tmp13 * -Fix1961570560;

                tmp12 += z1;
                tmp13 += z1;

                block[y * 8 + 1] = (byte)((tmp0 + tmp10 + tmp12) >> (ConstBits - Pass1Bits));
                block[y * 8 + 3] = (byte)((tmp1 + tmp11 + tmp13) >> (ConstBits - Pass1Bits));
                block[y * 8 + 5] = (byte)((tmp2 + tmp11 + tmp12) >> (ConstBits - Pass1Bits));
                block[y * 8 + 7] = (byte)((tmp3 + tmp10 + tmp13) >> (ConstBits - Pass1Bits));
            }

            // Pass 2: process columns.
            // We remove pass1Bits scaling, but leave results scaled up by an overall factor of 8.
            for (int x = 0; x < 8; x++)
            {
                var tmp0 = block[0 * 8 + x] + block[7 * 8 + x];
                var tmp1 = block[1 * 8 + x] + block[6 * 8 + x];
                var tmp2 = block[2 * 8 + x] + block[5 * 8 + x];
                var tmp3 = block[3 * 8 + x] + block[4 * 8 + x];
                var tmp10 = tmp0 + tmp3 + 1 << (Pass1Bits - 1);

                var tmp12 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp13 = tmp1 - tmp2;

                tmp0 = block[0 * 8 + x] - block[7 * 8 + x];
                tmp1 = block[1 * 8 + x] - block[6 * 8 + x];
                tmp2 = block[2 * 8 + x] - block[5 * 8 + x];
                tmp3 = block[3 * 8 + x] - block[4 * 8 + x];

                block[0 * 8 + x] = (byte)((tmp10 + tmp11) >> Pass1Bits);
                block[4 * 8 + x] = (byte)((tmp10 - tmp11) >> Pass1Bits);

                var z1 = (tmp12 + tmp13) * Fix0541196100;

                z1 += 1 << (ConstBits + Pass1Bits - 1);

                block[2 * 8 + x] = (byte)((z1 + tmp12 * Fix0765366865) >> (ConstBits + Pass1Bits));
                block[6 * 8 + x] = (byte)((z1 - tmp13 * Fix1847759065) >> (ConstBits + Pass1Bits));

                tmp10 = tmp0 + tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp0 + tmp2;
                tmp13 = tmp1 + tmp3;
                z1 = (tmp12 + tmp13) * Fix1175875602;
                z1 += 1 << (ConstBits + Pass1Bits - 1);

                tmp0 = tmp0 * Fix1501321110;
                tmp1 = tmp1 * Fix3072711026;
                tmp2 = tmp2 * Fix2053119869;
                tmp3 = tmp3 * Fix0298631336;
                tmp10 = tmp10 * -Fix0899976223;
                tmp11 = tmp11 * -Fix2562915447;
                tmp12 = tmp12 * -Fix0390180644;
                tmp13 = tmp13 * -Fix1961570560;

                tmp12 += z1;
                tmp13 += z1;

                block[1 * 8 + x] = (byte)((tmp0 + tmp10 + tmp12) >> (ConstBits + Pass1Bits));
                block[3 * 8 + x] = (byte)((tmp1 + tmp11 + tmp13) >> (ConstBits + Pass1Bits));
                block[5 * 8 + x] = (byte)((tmp2 + tmp11 + tmp12) >> (ConstBits + Pass1Bits));
                block[7 * 8 + x] = (byte)((tmp3 + tmp10 + tmp13) >> (ConstBits + Pass1Bits));
            }
        }

        public static void forwardDCT8x8(float[] matrix)
        {
            float a00, a10, a20, a30, a40, a50, a60, a70;
            float a01, a11, a21, a31, negA41, a51, a61;
            float a22, a23, mul5, a43, a53, a63;
            float a54, a74;

            for (int i = 0; i < 8; i++)
            {
                a00 = matrix[8 * i] + matrix[8 * i + 7];
                a10 = matrix[8 * i + 1] + matrix[8 * i + 6];
                a20 = matrix[8 * i + 2] + matrix[8 * i + 5];
                a30 = matrix[8 * i + 3] + matrix[8 * i + 4];
                a40 = matrix[8 * i + 3] - matrix[8 * i + 4];
                a50 = matrix[8 * i + 2] - matrix[8 * i + 5];
                a60 = matrix[8 * i + 1] - matrix[8 * i + 6];
                a70 = matrix[8 * i] - matrix[8 * i + 7];
                a01 = a00 + a30;
                a11 = a10 + a20;
                a21 = a10 - a20;
                a31 = a00 - a30;
                negA41 = a40 + a50;
                a51 = a50 + a60;
                a61 = a60 + a70;
                a22 = a21 + a31;
                a23 = a22 * A1;
                mul5 = (a61 - negA41) * A5;
                a43 = negA41 * A2 - mul5;
                a53 = a51 * A3;
                a63 = a61 * A4 - mul5;
                a54 = a70 + a53;
                a74 = a70 - a53;
                matrix[8 * i] = a01 + a11;
                matrix[8 * i + 4] = a01 - a11;
                matrix[8 * i + 2] = a31 + a23;
                matrix[8 * i + 6] = a31 - a23;
                matrix[8 * i + 5] = a74 + a43;
                matrix[8 * i + 1] = a54 + a63;
                matrix[8 * i + 7] = a54 - a63;
                matrix[8 * i + 3] = a74 - a43;
            }

            for (int i = 0; i < 8; i++)
            {
                a00 = matrix[i] + matrix[56 + i];
                a10 = matrix[8 + i] + matrix[48 + i];
                a20 = matrix[16 + i] + matrix[40 + i];
                a30 = matrix[24 + i] + matrix[32 + i];
                a40 = matrix[24 + i] - matrix[32 + i];
                a50 = matrix[16 + i] - matrix[40 + i];
                a60 = matrix[8 + i] - matrix[48 + i];
                a70 = matrix[i] - matrix[56 + i];
                a01 = a00 + a30;
                a11 = a10 + a20;
                a21 = a10 - a20;
                a31 = a00 - a30;
                negA41 = a40 + a50;
                a51 = a50 + a60;
                a61 = a60 + a70;
                a22 = a21 + a31;
                a23 = a22 * A1;
                mul5 = (a61 - negA41) * A5;
                a43 = negA41 * A2 - mul5;
                a53 = a51 * A3;
                a63 = a61 * A4 - mul5;
                a54 = a70 + a53;
                a74 = a70 - a53;
                matrix[i] = a01 + a11;
                matrix[32 + i] = a01 - a11;
                matrix[16 + i] = a31 + a23;
                matrix[48 + i] = a31 - a23;
                matrix[40 + i] = a74 + a43;
                matrix[8 + i] = a54 + a63;
                matrix[56 + i] = a54 - a63;
                matrix[24 + i] = a74 - a43;
            }
        }
    }
}