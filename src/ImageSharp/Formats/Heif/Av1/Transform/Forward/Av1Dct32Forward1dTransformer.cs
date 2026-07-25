// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct32Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 32, nameof(input));
        Guard.MustBeSizedAtLeast(output, 32, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit);
    }

    private static void TransformScalar(ref int input, ref int output, int cosBit)
    {
        Span<int> temp0 = stackalloc int[32];
        Span<int> temp1 = stackalloc int[32];

        // stage 0;

        // stage 1;
        temp0[0] = Unsafe.Add(ref input, 0) + Unsafe.Add(ref input, 31);
        temp0[1] = Unsafe.Add(ref input, 1) + Unsafe.Add(ref input, 30);
        temp0[2] = Unsafe.Add(ref input, 2) + Unsafe.Add(ref input, 29);
        temp0[3] = Unsafe.Add(ref input, 3) + Unsafe.Add(ref input, 28);
        temp0[4] = Unsafe.Add(ref input, 4) + Unsafe.Add(ref input, 27);
        temp0[5] = Unsafe.Add(ref input, 5) + Unsafe.Add(ref input, 26);
        temp0[6] = Unsafe.Add(ref input, 6) + Unsafe.Add(ref input, 25);
        temp0[7] = Unsafe.Add(ref input, 7) + Unsafe.Add(ref input, 24);
        temp0[8] = Unsafe.Add(ref input, 8) + Unsafe.Add(ref input, 23);
        temp0[9] = Unsafe.Add(ref input, 9) + Unsafe.Add(ref input, 22);
        temp0[10] = Unsafe.Add(ref input, 10) + Unsafe.Add(ref input, 21);
        temp0[11] = Unsafe.Add(ref input, 11) + Unsafe.Add(ref input, 20);
        temp0[12] = Unsafe.Add(ref input, 12) + Unsafe.Add(ref input, 19);
        temp0[13] = Unsafe.Add(ref input, 13) + Unsafe.Add(ref input, 18);
        temp0[14] = Unsafe.Add(ref input, 14) + Unsafe.Add(ref input, 17);
        temp0[15] = Unsafe.Add(ref input, 15) + Unsafe.Add(ref input, 16);
        temp0[16] = -Unsafe.Add(ref input, 16) + Unsafe.Add(ref input, 15);
        temp0[17] = -Unsafe.Add(ref input, 17) + Unsafe.Add(ref input, 14);
        temp0[18] = -Unsafe.Add(ref input, 18) + Unsafe.Add(ref input, 13);
        temp0[19] = -Unsafe.Add(ref input, 19) + Unsafe.Add(ref input, 12);
        temp0[20] = -Unsafe.Add(ref input, 20) + Unsafe.Add(ref input, 11);
        temp0[21] = -Unsafe.Add(ref input, 21) + Unsafe.Add(ref input, 10);
        temp0[22] = -Unsafe.Add(ref input, 22) + Unsafe.Add(ref input, 9);
        temp0[23] = -Unsafe.Add(ref input, 23) + Unsafe.Add(ref input, 8);
        temp0[24] = -Unsafe.Add(ref input, 24) + Unsafe.Add(ref input, 7);
        temp0[25] = -Unsafe.Add(ref input, 25) + Unsafe.Add(ref input, 6);
        temp0[26] = -Unsafe.Add(ref input, 26) + Unsafe.Add(ref input, 5);
        temp0[27] = -Unsafe.Add(ref input, 27) + Unsafe.Add(ref input, 4);
        temp0[28] = -Unsafe.Add(ref input, 28) + Unsafe.Add(ref input, 3);
        temp0[29] = -Unsafe.Add(ref input, 29) + Unsafe.Add(ref input, 2);
        temp0[30] = -Unsafe.Add(ref input, 30) + Unsafe.Add(ref input, 1);
        temp0[31] = -Unsafe.Add(ref input, 31) + Unsafe.Add(ref input, 0);

        // stage 2
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        temp1[0] = temp0[0] + temp0[15];
        temp1[1] = temp0[1] + temp0[14];
        temp1[2] = temp0[2] + temp0[13];
        temp1[3] = temp0[3] + temp0[12];
        temp1[4] = temp0[4] + temp0[11];
        temp1[5] = temp0[5] + temp0[10];
        temp1[6] = temp0[6] + temp0[9];
        temp1[7] = temp0[7] + temp0[8];
        temp1[8] = -temp0[8] + temp0[7];
        temp1[9] = -temp0[9] + temp0[6];
        temp1[10] = -temp0[10] + temp0[5];
        temp1[11] = -temp0[11] + temp0[4];
        temp1[12] = -temp0[12] + temp0[3];
        temp1[13] = -temp0[13] + temp0[2];
        temp1[14] = -temp0[14] + temp0[1];
        temp1[15] = -temp0[15] + temp0[0];
        temp1[16] = temp0[16];
        temp1[17] = temp0[17];
        temp1[18] = temp0[18];
        temp1[19] = temp0[19];
        temp1[20] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[20], cospi[32], temp0[27], cosBit);
        temp1[21] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[21], cospi[32], temp0[26], cosBit);
        temp1[22] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[22], cospi[32], temp0[25], cosBit);
        temp1[23] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[23], cospi[32], temp0[24], cosBit);
        temp1[24] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[24], cospi[32], temp0[23], cosBit);
        temp1[25] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[25], cospi[32], temp0[22], cosBit);
        temp1[26] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[26], cospi[32], temp0[21], cosBit);
        temp1[27] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[27], cospi[32], temp0[20], cosBit);
        temp1[28] = temp0[28];
        temp1[29] = temp0[29];
        temp1[30] = temp0[30];
        temp1[31] = temp0[31];

        // stage 3
        temp0[0] = temp1[0] + temp1[7];
        temp0[1] = temp1[1] + temp1[6];
        temp0[2] = temp1[2] + temp1[5];
        temp0[3] = temp1[3] + temp1[4];
        temp0[4] = -temp1[4] + temp1[3];
        temp0[5] = -temp1[5] + temp1[2];
        temp0[6] = -temp1[6] + temp1[1];
        temp0[7] = -temp1[7] + temp1[0];
        temp0[8] = temp1[8];
        temp0[9] = temp1[9];
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp1[10], cospi[32], temp1[13], cosBit);
        temp0[11] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp1[11], cospi[32], temp1[12], cosBit);
        temp0[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[12], cospi[32], temp1[11], cosBit);
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[13], cospi[32], temp1[10], cosBit);
        temp0[14] = temp1[14];
        temp0[15] = temp1[15];
        temp0[16] = temp1[16] + temp1[23];
        temp0[17] = temp1[17] + temp1[22];
        temp0[18] = temp1[18] + temp1[21];
        temp0[19] = temp1[19] + temp1[20];
        temp0[20] = -temp1[20] + temp1[19];
        temp0[21] = -temp1[21] + temp1[18];
        temp0[22] = -temp1[22] + temp1[17];
        temp0[23] = -temp1[23] + temp1[16];
        temp0[24] = -temp1[24] + temp1[31];
        temp0[25] = -temp1[25] + temp1[30];
        temp0[26] = -temp1[26] + temp1[29];
        temp0[27] = -temp1[27] + temp1[28];
        temp0[28] = temp1[28] + temp1[27];
        temp0[29] = temp1[29] + temp1[26];
        temp0[30] = temp1[30] + temp1[25];
        temp0[31] = temp1[31] + temp1[24];

        // stage 4
        temp1[0] = temp0[0] + temp0[3];
        temp1[1] = temp0[1] + temp0[2];
        temp1[2] = -temp0[2] + temp0[1];
        temp1[3] = -temp0[3] + temp0[0];
        temp1[4] = temp0[4];
        temp1[5] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[5], cospi[32], temp0[6], cosBit);
        temp1[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[6], cospi[32], temp0[5], cosBit);
        temp1[7] = temp0[7];
        temp1[8] = temp0[8] + temp0[11];
        temp1[9] = temp0[9] + temp0[10];
        temp1[10] = -temp0[10] + temp0[9];
        temp1[11] = -temp0[11] + temp0[8];
        temp1[12] = -temp0[12] + temp0[15];
        temp1[13] = -temp0[13] + temp0[14];
        temp1[14] = temp0[14] + temp0[13];
        temp1[15] = temp0[15] + temp0[12];
        temp1[16] = temp0[16];
        temp1[17] = temp0[17];
        temp1[18] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[16], temp0[18], cospi[48], temp0[29], cosBit);
        temp1[19] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[16], temp0[19], cospi[48], temp0[28], cosBit);
        temp1[20] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp0[20], -cospi[16], temp0[27], cosBit);
        temp1[21] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp0[21], -cospi[16], temp0[26], cosBit);
        temp1[22] = temp0[22];
        temp1[23] = temp0[23];
        temp1[24] = temp0[24];
        temp1[25] = temp0[25];
        temp1[26] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp0[26], -cospi[16], temp0[21], cosBit);
        temp1[27] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp0[27], -cospi[16], temp0[20], cosBit);
        temp1[28] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp0[28], cospi[48], temp0[19], cosBit);
        temp1[29] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp0[29], cospi[48], temp0[18], cosBit);
        temp1[30] = temp0[30];
        temp1[31] = temp0[31];

        // stage 5
        temp0[0] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[0], cospi[32], temp1[1], cosBit);
        temp0[1] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp1[1], cospi[32], temp1[0], cosBit);
        temp0[2] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[2], cospi[16], temp1[3], cosBit);
        temp0[3] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[3], -cospi[16], temp1[2], cosBit);
        temp0[4] = temp1[4] + temp1[5];
        temp0[5] = -temp1[5] + temp1[4];
        temp0[6] = -temp1[6] + temp1[7];
        temp0[7] = temp1[7] + temp1[6];
        temp0[8] = temp1[8];
        temp0[9] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[16], temp1[9], cospi[48], temp1[14], cosBit);
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp1[10], -cospi[16], temp1[13], cosBit);
        temp0[11] = temp1[11];
        temp0[12] = temp1[12];
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[13], -cospi[16], temp1[10], cosBit);
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp1[14], cospi[48], temp1[9], cosBit);
        temp0[15] = temp1[15];
        temp0[16] = temp1[16] + temp1[19];
        temp0[17] = temp1[17] + temp1[18];
        temp0[18] = -temp1[18] + temp1[17];
        temp0[19] = -temp1[19] + temp1[16];
        temp0[20] = -temp1[20] + temp1[23];
        temp0[21] = -temp1[21] + temp1[22];
        temp0[22] = temp1[22] + temp1[21];
        temp0[23] = temp1[23] + temp1[20];
        temp0[24] = temp1[24] + temp1[27];
        temp0[25] = temp1[25] + temp1[26];
        temp0[26] = -temp1[26] + temp1[25];
        temp0[27] = -temp1[27] + temp1[24];
        temp0[28] = -temp1[28] + temp1[31];
        temp0[29] = -temp1[29] + temp1[30];
        temp0[30] = temp1[30] + temp1[29];
        temp0[31] = temp1[31] + temp1[28];

        // stage 6
        temp1[0] = temp0[0];
        temp1[1] = temp0[1];
        temp1[2] = temp0[2];
        temp1[3] = temp0[3];
        temp1[4] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp0[4], cospi[8], temp0[7], cosBit);
        temp1[5] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp0[5], cospi[40], temp0[6], cosBit);
        temp1[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp0[6], -cospi[40], temp0[5], cosBit);
        temp1[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp0[7], -cospi[8], temp0[4], cosBit);
        temp1[8] = temp0[8] + temp0[9];
        temp1[9] = -temp0[9] + temp0[8];
        temp1[10] = -temp0[10] + temp0[11];
        temp1[11] = temp0[11] + temp0[10];
        temp1[12] = temp0[12] + temp0[13];
        temp1[13] = -temp0[13] + temp0[12];
        temp1[14] = -temp0[14] + temp0[15];
        temp1[15] = temp0[15] + temp0[14];
        temp1[16] = temp0[16];
        temp1[17] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[8], temp0[17], cospi[56], temp0[30], cosBit);
        temp1[18] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[56], temp0[18], -cospi[8], temp0[29], cosBit);
        temp1[19] = temp0[19];
        temp1[20] = temp0[20];
        temp1[21] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[40], temp0[21], cospi[24], temp0[26], cosBit);
        temp1[22] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[24], temp0[22], -cospi[40], temp0[25], cosBit);
        temp1[23] = temp0[23];
        temp1[24] = temp0[24];
        temp1[25] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp0[25], -cospi[40], temp0[22], cosBit);
        temp1[26] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[40], temp0[26], cospi[24], temp0[21], cosBit);
        temp1[27] = temp0[27];
        temp1[28] = temp0[28];
        temp1[29] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp0[29], -cospi[8], temp0[18], cosBit);
        temp1[30] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[8], temp0[30], cospi[56], temp0[17], cosBit);
        temp1[31] = temp0[31];

        // stage 7
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = temp1[4];
        temp0[5] = temp1[5];
        temp0[6] = temp1[6];
        temp0[7] = temp1[7];
        temp0[8] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[60], temp1[8], cospi[4], temp1[15], cosBit);
        temp0[9] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[28], temp1[9], cospi[36], temp1[14], cosBit);
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[44], temp1[10], cospi[20], temp1[13], cosBit);
        temp0[11] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[12], temp1[11], cospi[52], temp1[12], cosBit);
        temp0[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[12], temp1[12], -cospi[52], temp1[11], cosBit);
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[44], temp1[13], -cospi[20], temp1[10], cosBit);
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[28], temp1[14], -cospi[36], temp1[9], cosBit);
        temp0[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[60], temp1[15], -cospi[4], temp1[8], cosBit);
        temp0[16] = temp1[16] + temp1[17];
        temp0[17] = -temp1[17] + temp1[16];
        temp0[18] = -temp1[18] + temp1[19];
        temp0[19] = temp1[19] + temp1[18];
        temp0[20] = temp1[20] + temp1[21];
        temp0[21] = -temp1[21] + temp1[20];
        temp0[22] = -temp1[22] + temp1[23];
        temp0[23] = temp1[23] + temp1[22];
        temp0[24] = temp1[24] + temp1[25];
        temp0[25] = -temp1[25] + temp1[24];
        temp0[26] = -temp1[26] + temp1[27];
        temp0[27] = temp1[27] + temp1[26];
        temp0[28] = temp1[28] + temp1[29];
        temp0[29] = -temp1[29] + temp1[28];
        temp0[30] = -temp1[30] + temp1[31];
        temp0[31] = temp1[31] + temp1[30];

        // stage 8
        temp1[0] = temp0[0];
        temp1[1] = temp0[1];
        temp1[2] = temp0[2];
        temp1[3] = temp0[3];
        temp1[4] = temp0[4];
        temp1[5] = temp0[5];
        temp1[6] = temp0[6];
        temp1[7] = temp0[7];
        temp1[8] = temp0[8];
        temp1[9] = temp0[9];
        temp1[10] = temp0[10];
        temp1[11] = temp0[11];
        temp1[12] = temp0[12];
        temp1[13] = temp0[13];
        temp1[14] = temp0[14];
        temp1[15] = temp0[15];
        temp1[16] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[62], temp0[16], cospi[2], temp0[31], cosBit);
        temp1[17] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[30], temp0[17], cospi[34], temp0[30], cosBit);
        temp1[18] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[46], temp0[18], cospi[18], temp0[29], cosBit);
        temp1[19] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[14], temp0[19], cospi[50], temp0[28], cosBit);
        temp1[20] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[54], temp0[20], cospi[10], temp0[27], cosBit);
        temp1[21] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[22], temp0[21], cospi[42], temp0[26], cosBit);
        temp1[22] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[38], temp0[22], cospi[26], temp0[25], cosBit);
        temp1[23] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[6], temp0[23], cospi[58], temp0[24], cosBit);
        temp1[24] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[6], temp0[24], -cospi[58], temp0[23], cosBit);
        temp1[25] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[38], temp0[25], -cospi[26], temp0[22], cosBit);
        temp1[26] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[22], temp0[26], -cospi[42], temp0[21], cosBit);
        temp1[27] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[54], temp0[27], -cospi[10], temp0[20], cosBit);
        temp1[28] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[14], temp0[28], -cospi[50], temp0[19], cosBit);
        temp1[29] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[46], temp0[29], -cospi[18], temp0[18], cosBit);
        temp1[30] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[30], temp0[30], -cospi[34], temp0[17], cosBit);
        temp1[31] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[62], temp0[31], -cospi[2], temp0[16], cosBit);

        // stage 9
        Unsafe.Add(ref output, 0) = temp1[0];
        Unsafe.Add(ref output, 1) = temp1[16];
        Unsafe.Add(ref output, 2) = temp1[8];
        Unsafe.Add(ref output, 3) = temp1[24];
        Unsafe.Add(ref output, 4) = temp1[4];
        Unsafe.Add(ref output, 5) = temp1[20];
        Unsafe.Add(ref output, 6) = temp1[12];
        Unsafe.Add(ref output, 7) = temp1[28];
        Unsafe.Add(ref output, 8) = temp1[2];
        Unsafe.Add(ref output, 9) = temp1[18];
        Unsafe.Add(ref output, 10) = temp1[10];
        Unsafe.Add(ref output, 11) = temp1[26];
        Unsafe.Add(ref output, 12) = temp1[6];
        Unsafe.Add(ref output, 13) = temp1[22];
        Unsafe.Add(ref output, 14) = temp1[14];
        Unsafe.Add(ref output, 15) = temp1[30];
        Unsafe.Add(ref output, 16) = temp1[1];
        Unsafe.Add(ref output, 17) = temp1[17];
        Unsafe.Add(ref output, 18) = temp1[9];
        Unsafe.Add(ref output, 19) = temp1[25];
        Unsafe.Add(ref output, 20) = temp1[5];
        Unsafe.Add(ref output, 21) = temp1[21];
        Unsafe.Add(ref output, 22) = temp1[13];
        Unsafe.Add(ref output, 23) = temp1[29];
        Unsafe.Add(ref output, 24) = temp1[3];
        Unsafe.Add(ref output, 25) = temp1[19];
        Unsafe.Add(ref output, 26) = temp1[11];
        Unsafe.Add(ref output, 27) = temp1[27];
        Unsafe.Add(ref output, 28) = temp1[7];
        Unsafe.Add(ref output, 29) = temp1[23];
        Unsafe.Add(ref output, 30) = temp1[15];
        Unsafe.Add(ref output, 31) = temp1[31];
    }
}
