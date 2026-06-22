// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Dct32Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 32, nameof(input));
        Guard.MustBeSizedAtLeast(output, 32, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_idct32_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;
        Span<int> temp0 = stackalloc int[32];
        Span<int> temp1 = stackalloc int[32];

        // stage 0;

        // stage 1;
        stage++;
        temp1[0] = Unsafe.Add(ref input, 0);
        temp1[1] = Unsafe.Add(ref input, 16);
        temp1[2] = Unsafe.Add(ref input, 8);
        temp1[3] = Unsafe.Add(ref input, 24);
        temp1[4] = Unsafe.Add(ref input, 4);
        temp1[5] = Unsafe.Add(ref input, 20);
        temp1[6] = Unsafe.Add(ref input, 12);
        temp1[7] = Unsafe.Add(ref input, 28);
        temp1[8] = Unsafe.Add(ref input, 2);
        temp1[9] = Unsafe.Add(ref input, 18);
        temp1[10] = Unsafe.Add(ref input, 10);
        temp1[11] = Unsafe.Add(ref input, 26);
        temp1[12] = Unsafe.Add(ref input, 6);
        temp1[13] = Unsafe.Add(ref input, 22);
        temp1[14] = Unsafe.Add(ref input, 14);
        temp1[15] = Unsafe.Add(ref input, 30);
        temp1[16] = Unsafe.Add(ref input, 1);
        temp1[17] = Unsafe.Add(ref input, 17);
        temp1[18] = Unsafe.Add(ref input, 9);
        temp1[19] = Unsafe.Add(ref input, 25);
        temp1[20] = Unsafe.Add(ref input, 5);
        temp1[21] = Unsafe.Add(ref input, 21);
        temp1[22] = Unsafe.Add(ref input, 13);
        temp1[23] = Unsafe.Add(ref input, 29);
        temp1[24] = Unsafe.Add(ref input, 3);
        temp1[25] = Unsafe.Add(ref input, 19);
        temp1[26] = Unsafe.Add(ref input, 11);
        temp1[27] = Unsafe.Add(ref input, 27);
        temp1[28] = Unsafe.Add(ref input, 7);
        temp1[29] = Unsafe.Add(ref input, 23);
        temp1[30] = Unsafe.Add(ref input, 15);
        temp1[31] = Unsafe.Add(ref input, 31);

        // range_check_buf(stage, input, bf1, size, range);

        // stage 2
        stage++;
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = temp1[4];
        temp0[5] = temp1[5];
        temp0[6] = temp1[6];
        temp0[7] = temp1[7];
        temp0[8] = temp1[8];
        temp0[9] = temp1[9];
        temp0[10] = temp1[10];
        temp0[11] = temp1[11];
        temp0[12] = temp1[12];
        temp0[13] = temp1[13];
        temp0[14] = temp1[14];
        temp0[15] = temp1[15];
        temp0[16] = HalfButterfly(cospi[62], temp1[16], -cospi[2], temp1[31], cosBit);
        temp0[17] = HalfButterfly(cospi[30], temp1[17], -cospi[34], temp1[30], cosBit);
        temp0[18] = HalfButterfly(cospi[46], temp1[18], -cospi[18], temp1[29], cosBit);
        temp0[19] = HalfButterfly(cospi[14], temp1[19], -cospi[50], temp1[28], cosBit);
        temp0[20] = HalfButterfly(cospi[54], temp1[20], -cospi[10], temp1[27], cosBit);
        temp0[21] = HalfButterfly(cospi[22], temp1[21], -cospi[42], temp1[26], cosBit);
        temp0[22] = HalfButterfly(cospi[38], temp1[22], -cospi[26], temp1[25], cosBit);
        temp0[23] = HalfButterfly(cospi[6], temp1[23], -cospi[58], temp1[24], cosBit);
        temp0[24] = HalfButterfly(cospi[58], temp1[23], cospi[6], temp1[24], cosBit);
        temp0[25] = HalfButterfly(cospi[26], temp1[22], cospi[38], temp1[25], cosBit);
        temp0[26] = HalfButterfly(cospi[42], temp1[21], cospi[22], temp1[26], cosBit);
        temp0[27] = HalfButterfly(cospi[10], temp1[20], cospi[54], temp1[27], cosBit);
        temp0[28] = HalfButterfly(cospi[50], temp1[19], cospi[14], temp1[28], cosBit);
        temp0[29] = HalfButterfly(cospi[18], temp1[18], cospi[46], temp1[29], cosBit);
        temp0[30] = HalfButterfly(cospi[34], temp1[17], cospi[30], temp1[30], cosBit);
        temp0[31] = HalfButterfly(cospi[2], temp1[16], cospi[62], temp1[31], cosBit);

        // range_check_buf(stage, input, bf1, size, range);

        // stage 3
        stage++;
        byte range = stageRange[stage];
        temp1[0] = temp0[0];
        temp1[1] = temp0[1];
        temp1[2] = temp0[2];
        temp1[3] = temp0[3];
        temp1[4] = temp0[4];
        temp1[5] = temp0[5];
        temp1[6] = temp0[6];
        temp1[7] = temp0[7];
        temp1[8] = HalfButterfly(cospi[60], temp0[8], -cospi[4], temp0[15], cosBit);
        temp1[9] = HalfButterfly(cospi[28], temp0[9], -cospi[36], temp0[14], cosBit);
        temp1[10] = HalfButterfly(cospi[44], temp0[10], -cospi[20], temp0[13], cosBit);
        temp1[11] = HalfButterfly(cospi[12], temp0[11], -cospi[52], temp0[12], cosBit);
        temp1[12] = HalfButterfly(cospi[52], temp0[11], cospi[12], temp0[12], cosBit);
        temp1[13] = HalfButterfly(cospi[20], temp0[10], cospi[44], temp0[13], cosBit);
        temp1[14] = HalfButterfly(cospi[36], temp0[9], cospi[28], temp0[14], cosBit);
        temp1[15] = HalfButterfly(cospi[4], temp0[8], cospi[60], temp0[15], cosBit);
        temp1[16] = ClampValue(temp0[16] + temp0[17], range);
        temp1[17] = ClampValue(temp0[16] - temp0[17], range);
        temp1[18] = ClampValue(-temp0[18] + temp0[19], range);
        temp1[19] = ClampValue(temp0[18] + temp0[19], range);
        temp1[20] = ClampValue(temp0[20] + temp0[21], range);
        temp1[21] = ClampValue(temp0[20] - temp0[21], range);
        temp1[22] = ClampValue(-temp0[22] + temp0[23], range);
        temp1[23] = ClampValue(temp0[22] + temp0[23], range);
        temp1[24] = ClampValue(temp0[24] + temp0[25], range);
        temp1[25] = ClampValue(temp0[24] - temp0[25], range);
        temp1[26] = ClampValue(-temp0[26] + temp0[27], range);
        temp1[27] = ClampValue(temp0[26] + temp0[27], range);
        temp1[28] = ClampValue(temp0[28] + temp0[29], range);
        temp1[29] = ClampValue(temp0[28] - temp0[29], range);
        temp1[30] = ClampValue(-temp0[30] + temp0[31], range);
        temp1[31] = ClampValue(temp0[30] + temp0[31], range);

        // range_check_buf(stage, input, bf1, size, range);

        // stage 4
        stage++;
        range = stageRange[stage];
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = HalfButterfly(cospi[56], temp1[4], -cospi[8], temp1[7], cosBit);
        temp0[5] = HalfButterfly(cospi[24], temp1[5], -cospi[40], temp1[6], cosBit);
        temp0[6] = HalfButterfly(cospi[40], temp1[5], cospi[24], temp1[6], cosBit);
        temp0[7] = HalfButterfly(cospi[8], temp1[4], cospi[56], temp0[7], cosBit);
        temp0[8] = ClampValue(temp1[8] + temp1[9], range);
        temp0[9] = ClampValue(temp1[8] - temp1[9], range);
        temp0[10] = ClampValue(-temp1[10] + temp1[11], range);
        temp0[11] = ClampValue(temp1[10] + temp1[11], range);
        temp0[12] = ClampValue(temp1[12] + temp1[13], range);
        temp0[13] = ClampValue(temp1[12] - temp1[13], range);
        temp0[14] = ClampValue(-temp1[14] + temp1[15], range);
        temp0[15] = ClampValue(temp1[14] + temp1[15], range);
        temp0[16] = temp1[16];
        temp0[17] = HalfButterfly(-cospi[8], temp1[17], cospi[56], temp1[30], cosBit);
        temp0[18] = HalfButterfly(-cospi[56], temp1[18], -cospi[8], temp1[29], cosBit);
        temp0[19] = temp1[19];
        temp0[20] = temp1[20];
        temp0[21] = HalfButterfly(-cospi[40], temp1[21], cospi[24], temp1[26], cosBit);
        temp0[22] = HalfButterfly(-cospi[24], temp1[22], -cospi[40], temp1[25], cosBit);
        temp0[23] = temp1[23];
        temp0[24] = temp1[24];
        temp0[25] = HalfButterfly(-cospi[40], temp1[22], cospi[24], temp1[25], cosBit);
        temp0[26] = HalfButterfly(cospi[24], temp1[21], cospi[40], temp1[26], cosBit);
        temp0[27] = temp1[27];
        temp0[28] = temp1[28];
        temp0[29] = HalfButterfly(-cospi[8], temp1[18], cospi[56], temp1[29], cosBit);
        temp0[30] = HalfButterfly(cospi[56], temp1[17], cospi[8], temp1[30], cosBit);
        temp0[31] = temp1[31];

        // range_check_buf(stage, input, bf1, size, range);

        // stage 5
        stage++;
        range = stageRange[stage];
        temp1[0] = HalfButterfly(cospi[32], temp0[0], cospi[32], temp0[1], cosBit);
        temp1[1] = HalfButterfly(cospi[32], temp0[0], -cospi[32], temp0[1], cosBit);
        temp1[2] = HalfButterfly(cospi[48], temp0[2], -cospi[16], temp0[3], cosBit);
        temp1[3] = HalfButterfly(cospi[16], temp0[2], cospi[48], temp0[3], cosBit);
        temp1[4] = ClampValue(temp0[4] + temp0[5], range);
        temp1[5] = ClampValue(temp0[4] - temp0[5], range);
        temp1[6] = ClampValue(-temp0[6] + temp0[7], range);
        temp1[7] = ClampValue(temp0[6] + temp0[7], range);
        temp1[8] = temp0[8];
        temp1[9] = HalfButterfly(-cospi[16], temp0[9], cospi[48], temp0[14], cosBit);
        temp1[10] = HalfButterfly(-cospi[48], temp0[10], -cospi[16], temp0[13], cosBit);
        temp1[11] = temp0[11];
        temp1[12] = temp0[12];
        temp1[13] = HalfButterfly(-cospi[16], temp0[10], cospi[48], temp0[13], cosBit);
        temp1[14] = HalfButterfly(cospi[48], temp0[9], cospi[16], temp0[14], cosBit);
        temp1[15] = temp0[15];
        temp1[16] = ClampValue(temp0[16] + temp0[19], range);
        temp1[17] = ClampValue(temp0[17] + temp0[18], range);
        temp1[18] = ClampValue(temp0[17] - temp0[18], range);
        temp1[19] = ClampValue(temp0[16] - temp0[19], range);
        temp1[20] = ClampValue(-temp0[20] + temp0[23], range);
        temp1[21] = ClampValue(-temp0[21] + temp0[22], range);
        temp1[22] = ClampValue(temp0[21] + temp0[22], range);
        temp1[23] = ClampValue(temp0[20] + temp0[23], range);
        temp1[24] = ClampValue(temp0[24] + temp0[27], range);
        temp1[25] = ClampValue(temp0[25] + temp0[26], range);
        temp1[26] = ClampValue(temp0[25] - temp0[26], range);
        temp1[27] = ClampValue(temp0[24] - temp0[27], range);
        temp1[28] = ClampValue(-temp0[28] + temp0[31], range);
        temp1[29] = ClampValue(-temp0[29] + temp0[30], range);
        temp1[30] = ClampValue(temp0[29] + temp0[30], range);
        temp1[31] = ClampValue(temp0[28] + temp0[31], range);

        // range_check_buf(stage, input, bf1, size, range);

        // stage 6
        stage++;
        range = stageRange[stage];
        temp0[0] = ClampValue(temp1[0] + temp1[3], range);
        temp0[1] = ClampValue(temp1[1] + temp1[2], range);
        temp0[2] = ClampValue(temp1[1] - temp1[2], range);
        temp0[3] = ClampValue(temp1[0] - temp1[3], range);
        temp0[4] = temp1[4];
        temp0[5] = HalfButterfly(-cospi[32], temp1[5], cospi[32], temp1[6], cosBit);
        temp0[6] = HalfButterfly(cospi[32], temp1[5], cospi[32], temp1[6], cosBit);
        temp0[7] = temp1[7];
        temp0[8] = ClampValue(temp1[8] + temp1[11], range);
        temp0[9] = ClampValue(temp1[9] + temp1[10], range);
        temp0[10] = ClampValue(temp1[9] - temp1[10], range);
        temp0[11] = ClampValue(temp1[8] - temp1[11], range);
        temp0[12] = ClampValue(-temp1[12] + temp1[15], range);
        temp0[13] = ClampValue(-temp1[13] + temp1[14], range);
        temp0[14] = ClampValue(temp1[13] + temp1[14], range);
        temp0[15] = ClampValue(temp1[12] + temp1[15], range);
        temp0[16] = temp1[16];
        temp0[17] = temp1[17];
        temp0[18] = HalfButterfly(-cospi[16], temp1[18], cospi[48], temp1[29], cosBit);
        temp0[19] = HalfButterfly(-cospi[16], temp1[19], cospi[48], temp1[28], cosBit);
        temp0[20] = HalfButterfly(-cospi[48], temp1[20], -cospi[16], temp1[27], cosBit);
        temp0[21] = HalfButterfly(-cospi[48], temp1[21], -cospi[16], temp1[26], cosBit);
        temp0[22] = temp1[22];
        temp0[23] = temp1[23];
        temp0[24] = temp1[24];
        temp0[25] = temp1[25];
        temp0[26] = HalfButterfly(-cospi[16], temp1[21], cospi[48], temp1[26], cosBit);
        temp0[27] = HalfButterfly(-cospi[16], temp1[20], cospi[48], temp1[27], cosBit);
        temp0[28] = HalfButterfly(cospi[48], temp1[19], cospi[16], temp1[28], cosBit);
        temp0[29] = HalfButterfly(cospi[48], temp1[18], cospi[16], temp1[29], cosBit);
        temp0[30] = temp1[30];
        temp0[31] = temp1[31];

        // range_check_buf(stage, input, bf1, size, range);

        // stage 7
        stage++;
        range = stageRange[stage];
        temp1[0] = ClampValue(temp0[0] + temp0[7], range);
        temp1[1] = ClampValue(temp0[1] + temp0[6], range);
        temp1[2] = ClampValue(temp0[2] + temp0[5], range);
        temp1[3] = ClampValue(temp0[3] + temp0[4], range);
        temp1[4] = ClampValue(temp0[3] - temp0[4], range);
        temp1[5] = ClampValue(temp0[2] - temp0[5], range);
        temp1[6] = ClampValue(temp0[1] - temp0[6], range);
        temp1[7] = ClampValue(temp0[0] - temp0[7], range);
        temp1[8] = temp0[8];
        temp1[9] = temp0[9];
        temp1[10] = HalfButterfly(-cospi[32], temp0[10], cospi[32], temp0[13], cosBit);
        temp1[11] = HalfButterfly(-cospi[32], temp0[11], cospi[32], temp0[12], cosBit);
        temp1[12] = HalfButterfly(cospi[32], temp0[11], cospi[32], temp0[12], cosBit);
        temp1[13] = HalfButterfly(cospi[32], temp0[10], cospi[32], temp0[13], cosBit);
        temp1[14] = temp0[14];
        temp1[15] = temp0[15];
        temp1[16] = ClampValue(temp0[16] + temp0[23], range);
        temp1[17] = ClampValue(temp0[17] + temp0[22], range);
        temp1[18] = ClampValue(temp0[18] + temp0[21], range);
        temp1[19] = ClampValue(temp0[19] + temp0[20], range);
        temp1[20] = ClampValue(temp0[19] - temp0[20], range);
        temp1[21] = ClampValue(temp0[18] - temp0[21], range);
        temp1[22] = ClampValue(temp0[17] - temp0[22], range);
        temp1[23] = ClampValue(temp0[16] - temp0[23], range);
        temp1[24] = ClampValue(-temp0[24] + temp0[31], range);
        temp1[25] = ClampValue(-temp0[25] + temp0[30], range);
        temp1[26] = ClampValue(-temp0[26] + temp0[29], range);
        temp1[27] = ClampValue(-temp0[27] + temp0[28], range);
        temp1[28] = ClampValue(temp0[27] + temp0[28], range);
        temp1[29] = ClampValue(temp0[26] + temp0[29], range);
        temp1[30] = ClampValue(temp0[25] + temp0[30], range);
        temp1[31] = ClampValue(temp0[24] + temp0[31], range);

        // range_check_buf(stage, input, bf1, size, range);

        // stage 8
        stage++;
        range = stageRange[stage];
        temp0[0] = ClampValue(temp1[0] + temp1[15], range);
        temp0[1] = ClampValue(temp1[1] + temp1[14], range);
        temp0[2] = ClampValue(temp1[2] + temp1[13], range);
        temp0[3] = ClampValue(temp1[3] + temp1[12], range);
        temp0[4] = ClampValue(temp1[4] + temp1[11], range);
        temp0[5] = ClampValue(temp1[5] + temp1[10], range);
        temp0[6] = ClampValue(temp1[6] + temp1[9], range);
        temp0[7] = ClampValue(temp1[7] + temp1[8], range);
        temp0[8] = ClampValue(temp1[7] - temp1[8], range);
        temp0[9] = ClampValue(temp1[6] - temp1[9], range);
        temp0[10] = ClampValue(temp1[5] - temp1[10], range);
        temp0[11] = ClampValue(temp1[4] - temp1[11], range);
        temp0[12] = ClampValue(temp1[3] - temp1[12], range);
        temp0[13] = ClampValue(temp1[2] - temp1[13], range);
        temp0[14] = ClampValue(temp1[1] - temp1[14], range);
        temp0[15] = ClampValue(temp1[0] - temp1[15], range);
        temp0[16] = temp1[16];
        temp0[17] = temp1[17];
        temp0[18] = temp1[18];
        temp0[19] = temp1[19];
        temp0[20] = HalfButterfly(-cospi[32], temp1[20], cospi[32], temp1[27], cosBit);
        temp0[21] = HalfButterfly(-cospi[32], temp1[21], cospi[32], temp1[26], cosBit);
        temp0[22] = HalfButterfly(-cospi[32], temp1[22], cospi[32], temp1[25], cosBit);
        temp0[23] = HalfButterfly(-cospi[32], temp1[23], cospi[32], temp1[24], cosBit);
        temp0[24] = HalfButterfly(cospi[32], temp1[23], cospi[32], temp1[24], cosBit);
        temp0[25] = HalfButterfly(cospi[32], temp1[22], cospi[32], temp1[25], cosBit);
        temp0[26] = HalfButterfly(cospi[32], temp1[21], cospi[32], temp1[26], cosBit);
        temp0[27] = HalfButterfly(cospi[32], temp1[20], cospi[32], temp1[27], cosBit);
        temp0[28] = temp1[28];
        temp0[29] = temp1[29];
        temp0[30] = temp1[30];
        temp0[31] = temp1[31];

        // range_check_buf(stage, input, bf1, size, range);

        // stage 9
        stage++;
        range = stageRange[stage];
        Unsafe.Add(ref output, 0) = ClampValue(temp0[0] + temp0[31], range);
        Unsafe.Add(ref output, 1) = ClampValue(temp0[1] + temp0[30], range);
        Unsafe.Add(ref output, 2) = ClampValue(temp0[2] + temp0[29], range);
        Unsafe.Add(ref output, 3) = ClampValue(temp0[3] + temp0[28], range);
        Unsafe.Add(ref output, 4) = ClampValue(temp0[4] + temp0[27], range);
        Unsafe.Add(ref output, 5) = ClampValue(temp0[5] + temp0[26], range);
        Unsafe.Add(ref output, 6) = ClampValue(temp0[6] + temp0[25], range);
        Unsafe.Add(ref output, 7) = ClampValue(temp0[7] + temp0[24], range);
        Unsafe.Add(ref output, 8) = ClampValue(temp0[8] + temp0[23], range);
        Unsafe.Add(ref output, 9) = ClampValue(temp0[9] + temp0[22], range);
        Unsafe.Add(ref output, 10) = ClampValue(temp0[10] + temp0[21], range);
        Unsafe.Add(ref output, 11) = ClampValue(temp0[11] + temp0[20], range);
        Unsafe.Add(ref output, 12) = ClampValue(temp0[12] + temp0[19], range);
        Unsafe.Add(ref output, 13) = ClampValue(temp0[13] + temp0[18], range);
        Unsafe.Add(ref output, 14) = ClampValue(temp0[14] + temp0[17], range);
        Unsafe.Add(ref output, 15) = ClampValue(temp0[15] + temp0[16], range);
        Unsafe.Add(ref output, 16) = ClampValue(temp0[15] - temp0[16], range);
        Unsafe.Add(ref output, 17) = ClampValue(temp0[14] - temp0[17], range);
        Unsafe.Add(ref output, 18) = ClampValue(temp0[13] - temp0[18], range);
        Unsafe.Add(ref output, 19) = ClampValue(temp0[12] - temp0[19], range);
        Unsafe.Add(ref output, 20) = ClampValue(temp0[11] - temp0[20], range);
        Unsafe.Add(ref output, 21) = ClampValue(temp0[10] - temp0[21], range);
        Unsafe.Add(ref output, 22) = ClampValue(temp0[9] - temp0[22], range);
        Unsafe.Add(ref output, 23) = ClampValue(temp0[8] - temp0[23], range);
        Unsafe.Add(ref output, 24) = ClampValue(temp0[7] - temp0[24], range);
        Unsafe.Add(ref output, 25) = ClampValue(temp0[6] - temp0[25], range);
        Unsafe.Add(ref output, 26) = ClampValue(temp0[5] - temp0[26], range);
        Unsafe.Add(ref output, 27) = ClampValue(temp0[4] - temp0[27], range);
        Unsafe.Add(ref output, 28) = ClampValue(temp0[3] - temp0[28], range);
        Unsafe.Add(ref output, 29) = ClampValue(temp0[2] - temp0[29], range);
        Unsafe.Add(ref output, 30) = ClampValue(temp0[1] - temp0[30], range);
        Unsafe.Add(ref output, 31) = ClampValue(temp0[0] - temp0[31], range);
    }

    internal static int ClampValue(int value, byte bit)
    {
        if (bit <= 0)
        {
            return value; // Do nothing for invalid clamp bit.
        }

        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));
        return (int)Av1Math.Clamp(value, min_value, max_value);
    }

    internal static int HalfButterfly(int w0, int in0, int w1, int in1, int bit)
    {
        long result64 = (long)(w0 * in0) + (w1 * in1);
        long intermediate = result64 + (1L << (bit - 1));

        // NOTE(david.barker): The value 'result_64' may not necessarily fit
        // into 32 bits. However, the result of this function is nominally
        // ROUND_POWER_OF_TWO_64(result_64, bit)
        // and that is required to fit into range many bits
        // (checked by range_check_buf()).
        //
        // Here we've unpacked that rounding operation, and it can be shown
        // that the value of 'intermediate' here *does* fit into 32 bits
        // for any conformant bitstream.
        // The upshot is that, if you do all this calculation using
        // wrapping 32-bit arithmetic instead of (non-wrapping) 64-bit arithmetic,
        // then you'll still get the correct result.
        return (int)(intermediate >> bit);
    }
}
