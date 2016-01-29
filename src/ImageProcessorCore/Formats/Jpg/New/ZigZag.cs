namespace ImageProcessorCore.Formats
{
    internal class ZigZag
    {
        internal static readonly int[] ZigZagMap =
        {
            0,   1,  8, 16,  9,  2,  3, 10,
            17, 24, 32, 25, 18, 11,  4,  5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13,  6,  7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

        public static void UnZigZag(float[] input, float[] output)
        {
            output[0] = input[0];
            output[1] = input[1];
            output[8] = input[2];
            output[16] = input[3];
            output[9] = input[4];
            output[2] = input[5];
            output[3] = input[6];
            output[10] = input[7];
            output[17] = input[8];
            output[24] = input[9];
            output[32] = input[10];
            output[25] = input[11];
            output[18] = input[12];
            output[11] = input[13];
            output[4] = input[14];
            output[5] = input[15];
            output[12] = input[16];
            output[19] = input[17];
            output[26] = input[18];
            output[33] = input[19];
            output[40] = input[20];
            output[48] = input[21];
            output[41] = input[22];
            output[34] = input[23];
            output[27] = input[24];
            output[20] = input[25];
            output[13] = input[26];
            output[6] = input[27];
            output[7] = input[28];
            output[14] = input[29];
            output[21] = input[30];
            output[28] = input[31];
            output[35] = input[32];
            output[42] = input[33];
            output[49] = input[34];
            output[56] = input[35];
            output[57] = input[36];
            output[50] = input[37];
            output[43] = input[38];
            output[36] = input[39];
            output[29] = input[40];
            output[22] = input[41];
            output[15] = input[42];
            output[23] = input[43];
            output[30] = input[44];
            output[37] = input[45];
            output[44] = input[46];
            output[51] = input[47];
            output[58] = input[48];
            output[59] = input[49];
            output[52] = input[50];
            output[45] = input[51];
            output[38] = input[52];
            output[31] = input[53];
            output[39] = input[54];
            output[46] = input[55];
            output[53] = input[56];
            output[60] = input[57];
            output[61] = input[58];
            output[54] = input[59];
            output[47] = input[60];
            output[55] = input[61];
            output[62] = input[62];
            output[63] = input[63];
        }
    }
}
