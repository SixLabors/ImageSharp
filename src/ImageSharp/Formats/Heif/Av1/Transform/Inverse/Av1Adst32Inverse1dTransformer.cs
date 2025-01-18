// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing;
using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Adst32Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 32, nameof(input));
        Guard.MustBeSizedAtLeast(output, 32, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_iadst32_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

        int stage = 0;
        Span<int> bf0 = stackalloc int[32];
        ref int step = ref bf0[0];
        Span<int> bf1 = stackalloc int[32];
        ref int buffer = ref bf1[0];

        // stage 0;
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref input, stageRange[stage]);

        // stage 1;
        stage++;
        bf1[0] = Unsafe.Add(ref input, 0);
        bf1[1] = -Unsafe.Add(ref input, 31);
        bf1[2] = -Unsafe.Add(ref input, 15);
        bf1[3] = Unsafe.Add(ref input, 16);
        bf1[4] = -Unsafe.Add(ref input, 7);
        bf1[5] = Unsafe.Add(ref input, 24);
        bf1[6] = Unsafe.Add(ref input, 8);
        bf1[7] = -Unsafe.Add(ref input, 23);
        bf1[8] = -Unsafe.Add(ref input, 3);
        bf1[9] = Unsafe.Add(ref input, 28);
        bf1[10] = Unsafe.Add(ref input, 12);
        bf1[11] = -Unsafe.Add(ref input, 19);
        bf1[12] = Unsafe.Add(ref input, 4);
        bf1[13] = -Unsafe.Add(ref input, 27);
        bf1[14] = -Unsafe.Add(ref input, 11);
        bf1[15] = Unsafe.Add(ref input, 20);
        bf1[16] = -Unsafe.Add(ref input, 1);
        bf1[17] = Unsafe.Add(ref input, 30);
        bf1[18] = Unsafe.Add(ref input, 14);
        bf1[19] = -Unsafe.Add(ref input, 17);
        bf1[20] = Unsafe.Add(ref input, 6);
        bf1[21] = -Unsafe.Add(ref input, 25);
        bf1[22] = -Unsafe.Add(ref input, 9);
        bf1[23] = Unsafe.Add(ref input, 22);
        bf1[24] = Unsafe.Add(ref input, 2);
        bf1[25] = -Unsafe.Add(ref input, 29);
        bf1[26] = -Unsafe.Add(ref input, 13);
        bf1[27] = Unsafe.Add(ref input, 18);
        bf1[28] = -Unsafe.Add(ref input, 5);
        bf1[29] = Unsafe.Add(ref input, 26);
        bf1[30] = Unsafe.Add(ref input, 10);
        bf1[31] = -Unsafe.Add(ref input, 21);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref buffer, stageRange[stage]);

        // stage 2
        stage++;
        bf0[0] = bf1[0];
        bf0[1] = bf1[1];
        bf0[2] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[2], cospi[32], bf1[3], cosBit);
        bf0[3] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[2], -cospi[32], bf1[3], cosBit);
        bf0[4] = bf1[4];
        bf0[5] = bf1[5];
        bf0[6] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[6], cospi[32], bf1[7], cosBit);
        bf0[7] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[6], -cospi[32], bf1[7], cosBit);
        bf0[8] = bf1[8];
        bf0[9] = bf1[9];
        bf0[10] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[10], cospi[32], bf1[11], cosBit);
        bf0[11] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[10], -cospi[32], bf1[11], cosBit);
        bf0[12] = bf1[12];
        bf0[13] = bf1[13];
        bf0[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[14], cospi[32], bf1[15], cosBit);
        bf0[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[14], -cospi[32], bf1[15], cosBit);
        bf0[16] = bf1[16];
        bf0[17] = bf1[17];
        bf0[18] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[18], cospi[32], bf1[19], cosBit);
        bf0[19] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[18], -cospi[32], bf1[19], cosBit);
        bf0[20] = bf1[20];
        bf0[21] = bf1[21];
        bf0[22] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[22], cospi[32], bf1[23], cosBit);
        bf0[23] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[22], -cospi[32], bf1[23], cosBit);
        bf0[24] = bf1[24];
        bf0[25] = bf1[25];
        bf0[26] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[26], cospi[32], bf1[27], cosBit);
        bf0[27] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[26], -cospi[32], bf1[27], cosBit);
        bf0[28] = bf1[28];
        bf0[29] = bf1[29];
        bf0[30] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[30], cospi[32], bf1[31], cosBit);
        bf0[31] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf1[30], -cospi[32], bf1[31], cosBit);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref step, stageRange[stage]);

        // stage 3
        stage++;
        bf1[0] = bf0[0] + bf0[2];
        bf1[1] = bf0[1] + bf0[3];
        bf1[2] = bf0[0] - bf0[2];
        bf1[3] = bf0[1] - bf0[3];
        bf1[4] = bf0[4] + bf0[6];
        bf1[5] = bf0[5] + bf0[7];
        bf1[6] = bf0[4] - bf0[6];
        bf1[7] = bf0[5] - bf0[7];
        bf1[8] = bf0[8] + bf0[10];
        bf1[9] = bf0[9] + bf0[11];
        bf1[10] = bf0[8] - bf0[10];
        bf1[11] = bf0[9] - bf0[11];
        bf1[12] = bf0[12] + bf0[14];
        bf1[13] = bf0[13] + bf0[15];
        bf1[14] = bf0[12] - bf0[14];
        bf1[15] = bf0[13] - bf0[15];
        bf1[16] = bf0[16] + bf0[18];
        bf1[17] = bf0[17] + bf0[19];
        bf1[18] = bf0[16] - bf0[18];
        bf1[19] = bf0[17] - bf0[19];
        bf1[20] = bf0[20] + bf0[22];
        bf1[21] = bf0[21] + bf0[23];
        bf1[22] = bf0[20] - bf0[22];
        bf1[23] = bf0[21] - bf0[23];
        bf1[24] = bf0[24] + bf0[26];
        bf1[25] = bf0[25] + bf0[27];
        bf1[26] = bf0[24] - bf0[26];
        bf1[27] = bf0[25] - bf0[27];
        bf1[28] = bf0[28] + bf0[30];
        bf1[29] = bf0[29] + bf0[31];
        bf1[30] = bf0[28] - bf0[30];
        bf1[31] = bf0[29] - bf0[31];
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref buffer, stageRange[stage]);

        // stage 4
        stage++;
        bf0[0] = bf1[0];
        bf0[1] = bf1[1];
        bf0[2] = bf1[2];
        bf0[3] = bf1[3];
        bf0[4] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[4], cospi[48], bf1[5], cosBit);
        bf0[5] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf1[4], -cospi[16], bf1[5], cosBit);
        bf0[6] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf1[6], cospi[16], bf1[7], cosBit);
        bf0[7] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[6], cospi[48], bf1[7], cosBit);
        bf0[8] = bf1[8];
        bf0[9] = bf1[9];
        bf0[10] = bf1[10];
        bf0[11] = bf1[11];
        bf0[12] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[12], cospi[48], bf1[13], cosBit);
        bf0[13] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf1[12], -cospi[16], bf1[13], cosBit);
        bf0[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf1[14], cospi[16], bf1[15], cosBit);
        bf0[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[14], cospi[48], bf1[15], cosBit);
        bf0[16] = bf1[16];
        bf0[17] = bf1[17];
        bf0[18] = bf1[18];
        bf0[19] = bf1[19];
        bf0[20] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[20], cospi[48], bf1[21], cosBit);
        bf0[21] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf1[20], -cospi[16], bf1[21], cosBit);
        bf0[22] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf1[22], cospi[16], bf1[23], cosBit);
        bf0[23] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[22], cospi[48], bf1[23], cosBit);
        bf0[24] = bf1[24];
        bf0[25] = bf1[25];
        bf0[26] = bf1[26];
        bf0[27] = bf1[27];
        bf0[28] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[28], cospi[48], bf1[29], cosBit);
        bf0[29] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf1[28], -cospi[16], bf1[29], cosBit);
        bf0[30] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf1[30], cospi[16], bf1[31], cosBit);
        bf0[31] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf1[30], cospi[48], bf1[31], cosBit);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref step, stageRange[stage]);

        // stage 5
        stage++;
        bf1[0] = bf0[0] + bf0[4];
        bf1[1] = bf0[1] + bf0[5];
        bf1[2] = bf0[2] + bf0[6];
        bf1[3] = bf0[3] + bf0[7];
        bf1[4] = bf0[0] - bf0[4];
        bf1[5] = bf0[1] - bf0[5];
        bf1[6] = bf0[2] - bf0[6];
        bf1[7] = bf0[3] - bf0[7];
        bf1[8] = bf0[8] + bf0[12];
        bf1[9] = bf0[9] + bf0[13];
        bf1[10] = bf0[10] + bf0[14];
        bf1[11] = bf0[11] + bf0[15];
        bf1[12] = bf0[8] - bf0[12];
        bf1[13] = bf0[9] - bf0[13];
        bf1[14] = bf0[10] - bf0[14];
        bf1[15] = bf0[11] - bf0[15];
        bf1[16] = bf0[16] + bf0[20];
        bf1[17] = bf0[17] + bf0[21];
        bf1[18] = bf0[18] + bf0[22];
        bf1[19] = bf0[19] + bf0[23];
        bf1[20] = bf0[16] - bf0[20];
        bf1[21] = bf0[17] - bf0[21];
        bf1[22] = bf0[18] - bf0[22];
        bf1[23] = bf0[19] - bf0[23];
        bf1[24] = bf0[24] + bf0[28];
        bf1[25] = bf0[25] + bf0[29];
        bf1[26] = bf0[26] + bf0[30];
        bf1[27] = bf0[27] + bf0[31];
        bf1[28] = bf0[24] - bf0[28];
        bf1[29] = bf0[25] - bf0[29];
        bf1[30] = bf0[26] - bf0[30];
        bf1[31] = bf0[27] - bf0[31];
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref buffer, stageRange[stage]);

        // stage 6
        stage++;
        bf0[0] = bf1[0];
        bf0[1] = bf1[1];
        bf0[2] = bf1[2];
        bf0[3] = bf1[3];
        bf0[4] = bf1[4];
        bf0[5] = bf1[5];
        bf0[6] = bf1[6];
        bf0[7] = bf1[7];
        bf0[8] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf1[8], cospi[56], bf1[9], cosBit);
        bf0[9] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[56], bf1[8], -cospi[8], bf1[9], cosBit);
        bf0[10] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf1[10], cospi[24], bf1[11], cosBit);
        bf0[11] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[24], bf1[10], -cospi[40], bf1[11], cosBit);
        bf0[12] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[56], bf1[12], cospi[8], bf1[13], cosBit);
        bf0[13] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf1[12], cospi[56], bf1[13], cosBit);
        bf0[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[24], bf1[14], cospi[40], bf1[15], cosBit);
        bf0[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf1[14], cospi[24], bf1[15], cosBit);
        bf0[16] = bf1[16];
        bf0[17] = bf1[17];
        bf0[18] = bf1[18];
        bf0[19] = bf1[19];
        bf0[20] = bf1[20];
        bf0[21] = bf1[21];
        bf0[22] = bf1[22];
        bf0[23] = bf1[23];
        bf0[24] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf1[24], cospi[56], bf1[25], cosBit);
        bf0[25] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[56], bf1[24], -cospi[8], bf1[25], cosBit);
        bf0[26] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf1[26], cospi[24], bf1[27], cosBit);
        bf0[27] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[24], bf1[26], -cospi[40], bf1[27], cosBit);
        bf0[28] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[56], bf1[28], cospi[8], bf1[29], cosBit);
        bf0[29] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf1[28], cospi[56], bf1[29], cosBit);
        bf0[30] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[24], bf1[30], cospi[40], bf1[31], cosBit);
        bf0[31] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf1[30], cospi[24], bf1[31], cosBit);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref step, stageRange[stage]);

        // stage 7
        stage++;
        bf1[0] = bf0[0] + bf0[8];
        bf1[1] = bf0[1] + bf0[9];
        bf1[2] = bf0[2] + bf0[10];
        bf1[3] = bf0[3] + bf0[11];
        bf1[4] = bf0[4] + bf0[12];
        bf1[5] = bf0[5] + bf0[13];
        bf1[6] = bf0[6] + bf0[14];
        bf1[7] = bf0[7] + bf0[15];
        bf1[8] = bf0[0] - bf0[8];
        bf1[9] = bf0[1] - bf0[9];
        bf1[10] = bf0[2] - bf0[10];
        bf1[11] = bf0[3] - bf0[11];
        bf1[12] = bf0[4] - bf0[12];
        bf1[13] = bf0[5] - bf0[13];
        bf1[14] = bf0[6] - bf0[14];
        bf1[15] = bf0[7] - bf0[15];
        bf1[16] = bf0[16] + bf0[24];
        bf1[17] = bf0[17] + bf0[25];
        bf1[18] = bf0[18] + bf0[26];
        bf1[19] = bf0[19] + bf0[27];
        bf1[20] = bf0[20] + bf0[28];
        bf1[21] = bf0[21] + bf0[29];
        bf1[22] = bf0[22] + bf0[30];
        bf1[23] = bf0[23] + bf0[31];
        bf1[24] = bf0[16] - bf0[24];
        bf1[25] = bf0[17] - bf0[25];
        bf1[26] = bf0[18] - bf0[26];
        bf1[27] = bf0[19] - bf0[27];
        bf1[28] = bf0[20] - bf0[28];
        bf1[29] = bf0[21] - bf0[29];
        bf1[30] = bf0[22] - bf0[30];
        bf1[31] = bf0[23] - bf0[31];
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref buffer, stageRange[stage]);

        // stage 8
        stage++;
        Unsafe.Add(ref step, 0) = Unsafe.Add(ref buffer, 0);
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Unsafe.Add(ref buffer, 2);
        Unsafe.Add(ref step, 3) = Unsafe.Add(ref buffer, 3);
        Unsafe.Add(ref step, 4) = Unsafe.Add(ref buffer, 4);
        Unsafe.Add(ref step, 5) = Unsafe.Add(ref buffer, 5);
        Unsafe.Add(ref step, 6) = Unsafe.Add(ref buffer, 6);
        Unsafe.Add(ref step, 7) = Unsafe.Add(ref buffer, 7);
        Unsafe.Add(ref step, 8) = Unsafe.Add(ref buffer, 8);
        Unsafe.Add(ref step, 9) = Unsafe.Add(ref buffer, 9);
        Unsafe.Add(ref step, 10) = Unsafe.Add(ref buffer, 10);
        Unsafe.Add(ref step, 11) = Unsafe.Add(ref buffer, 11);
        Unsafe.Add(ref step, 12) = Unsafe.Add(ref buffer, 12);
        Unsafe.Add(ref step, 13) = Unsafe.Add(ref buffer, 13);
        Unsafe.Add(ref step, 14) = Unsafe.Add(ref buffer, 14);
        Unsafe.Add(ref step, 15) = Unsafe.Add(ref buffer, 15);
        Unsafe.Add(ref step, 16) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[4], Unsafe.Add(ref buffer, 16), cospi[60], Unsafe.Add(ref buffer, 17), cosBit);
        Unsafe.Add(ref step, 17) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[60], Unsafe.Add(ref buffer, 16), -cospi[4], Unsafe.Add(ref buffer, 17), cosBit);
        Unsafe.Add(ref step, 18) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[20], Unsafe.Add(ref buffer, 18), cospi[44], Unsafe.Add(ref buffer, 19), cosBit);
        Unsafe.Add(ref step, 19) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[44], Unsafe.Add(ref buffer, 18), -cospi[20], Unsafe.Add(ref buffer, 19), cosBit);
        Unsafe.Add(ref step, 20) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[36], Unsafe.Add(ref buffer, 20), cospi[28], Unsafe.Add(ref buffer, 21), cosBit);
        Unsafe.Add(ref step, 21) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[28], Unsafe.Add(ref buffer, 20), -cospi[36], Unsafe.Add(ref buffer, 21), cosBit);
        Unsafe.Add(ref step, 22) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[52], Unsafe.Add(ref buffer, 22), cospi[12], Unsafe.Add(ref buffer, 23), cosBit);
        Unsafe.Add(ref step, 23) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[12], Unsafe.Add(ref buffer, 22), -cospi[52], Unsafe.Add(ref buffer, 23), cosBit);
        Unsafe.Add(ref step, 24) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[60], Unsafe.Add(ref buffer, 24), cospi[4], Unsafe.Add(ref buffer, 25), cosBit);
        Unsafe.Add(ref step, 25) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[4], Unsafe.Add(ref buffer, 24), cospi[60], Unsafe.Add(ref buffer, 25), cosBit);
        Unsafe.Add(ref step, 26) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[44], Unsafe.Add(ref buffer, 26), cospi[20], Unsafe.Add(ref buffer, 27), cosBit);
        Unsafe.Add(ref step, 27) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[20], Unsafe.Add(ref buffer, 26), cospi[44], Unsafe.Add(ref buffer, 27), cosBit);
        Unsafe.Add(ref step, 28) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[28], Unsafe.Add(ref buffer, 28), cospi[36], Unsafe.Add(ref buffer, 29), cosBit);
        Unsafe.Add(ref step, 29) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[36], Unsafe.Add(ref buffer, 28), cospi[28], Unsafe.Add(ref buffer, 29), cosBit);
        Unsafe.Add(ref step, 30) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[12], Unsafe.Add(ref buffer, 30), cospi[52], Unsafe.Add(ref buffer, 31), cosBit);
        Unsafe.Add(ref step, 31) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[52], Unsafe.Add(ref buffer, 30), cospi[12], Unsafe.Add(ref buffer, 31), cosBit);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref step, stageRange[stage]);

        // stage 9
        stage++;
        Unsafe.Add(ref buffer, 0) = Unsafe.Add(ref step, 0) + Unsafe.Add(ref step, 16);
        Unsafe.Add(ref buffer, 1) = Unsafe.Add(ref step, 1) + Unsafe.Add(ref step, 17);
        Unsafe.Add(ref buffer, 2) = Unsafe.Add(ref step, 2) + Unsafe.Add(ref step, 18);
        Unsafe.Add(ref buffer, 3) = Unsafe.Add(ref step, 3) + Unsafe.Add(ref step, 19);
        Unsafe.Add(ref buffer, 4) = Unsafe.Add(ref step, 4) + Unsafe.Add(ref step, 20);
        Unsafe.Add(ref buffer, 5) = Unsafe.Add(ref step, 5) + Unsafe.Add(ref step, 21);
        Unsafe.Add(ref buffer, 6) = Unsafe.Add(ref step, 6) + Unsafe.Add(ref step, 22);
        Unsafe.Add(ref buffer, 7) = Unsafe.Add(ref step, 7) + Unsafe.Add(ref step, 23);
        Unsafe.Add(ref buffer, 8) = Unsafe.Add(ref step, 8) + Unsafe.Add(ref step, 24);
        Unsafe.Add(ref buffer, 9) = Unsafe.Add(ref step, 9) + Unsafe.Add(ref step, 25);
        Unsafe.Add(ref buffer, 10) = Unsafe.Add(ref step, 10) + Unsafe.Add(ref step, 26);
        Unsafe.Add(ref buffer, 11) = Unsafe.Add(ref step, 11) + Unsafe.Add(ref step, 27);
        Unsafe.Add(ref buffer, 12) = Unsafe.Add(ref step, 12) + Unsafe.Add(ref step, 28);
        Unsafe.Add(ref buffer, 13) = Unsafe.Add(ref step, 13) + Unsafe.Add(ref step, 29);
        Unsafe.Add(ref buffer, 14) = Unsafe.Add(ref step, 14) + Unsafe.Add(ref step, 30);
        Unsafe.Add(ref buffer, 15) = Unsafe.Add(ref step, 15) + Unsafe.Add(ref step, 31);
        Unsafe.Add(ref buffer, 16) = Unsafe.Add(ref step, 0) - Unsafe.Add(ref step, 16);
        Unsafe.Add(ref buffer, 17) = Unsafe.Add(ref step, 1) - Unsafe.Add(ref step, 17);
        Unsafe.Add(ref buffer, 18) = Unsafe.Add(ref step, 2) - Unsafe.Add(ref step, 18);
        Unsafe.Add(ref buffer, 19) = Unsafe.Add(ref step, 3) - Unsafe.Add(ref step, 19);
        Unsafe.Add(ref buffer, 20) = Unsafe.Add(ref step, 4) - Unsafe.Add(ref step, 20);
        Unsafe.Add(ref buffer, 21) = Unsafe.Add(ref step, 5) - Unsafe.Add(ref step, 21);
        Unsafe.Add(ref buffer, 22) = Unsafe.Add(ref step, 6) - Unsafe.Add(ref step, 22);
        Unsafe.Add(ref buffer, 23) = Unsafe.Add(ref step, 7) - Unsafe.Add(ref step, 23);
        Unsafe.Add(ref buffer, 24) = Unsafe.Add(ref step, 8) - Unsafe.Add(ref step, 24);
        Unsafe.Add(ref buffer, 25) = Unsafe.Add(ref step, 9) - Unsafe.Add(ref step, 25);
        Unsafe.Add(ref buffer, 26) = Unsafe.Add(ref step, 10) - Unsafe.Add(ref step, 26);
        Unsafe.Add(ref buffer, 27) = Unsafe.Add(ref step, 11) - Unsafe.Add(ref step, 27);
        Unsafe.Add(ref buffer, 28) = Unsafe.Add(ref step, 12) - Unsafe.Add(ref step, 28);
        Unsafe.Add(ref buffer, 29) = Unsafe.Add(ref step, 13) - Unsafe.Add(ref step, 29);
        Unsafe.Add(ref buffer, 30) = Unsafe.Add(ref step, 14) - Unsafe.Add(ref step, 30);
        Unsafe.Add(ref buffer, 31) = Unsafe.Add(ref step, 15) - Unsafe.Add(ref step, 31);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref buffer, stageRange[stage]);

        // stage 10
        stage++;
        Unsafe.Add(ref step, 0) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[1], Unsafe.Add(ref buffer, 0), cospi[63], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 1) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[63], Unsafe.Add(ref buffer, 0), -cospi[1], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 2) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[5], Unsafe.Add(ref buffer, 2), cospi[59], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 3) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[59], Unsafe.Add(ref buffer, 2), -cospi[5], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 4) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[9], Unsafe.Add(ref buffer, 4), cospi[55], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 5) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[55], Unsafe.Add(ref buffer, 4), -cospi[9], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[13], Unsafe.Add(ref buffer, 6), cospi[51], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[51], Unsafe.Add(ref buffer, 6), -cospi[13], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 8) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[17], Unsafe.Add(ref buffer, 8), cospi[47], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 9) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[47], Unsafe.Add(ref buffer, 8), -cospi[17], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 10) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[21], Unsafe.Add(ref buffer, 10), cospi[43], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 11) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[43], Unsafe.Add(ref buffer, 10), -cospi[21], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 12) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[25], Unsafe.Add(ref buffer, 12), cospi[39], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 13) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[39], Unsafe.Add(ref buffer, 12), -cospi[25], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 14) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[29], Unsafe.Add(ref buffer, 14), cospi[35], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 15) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[35], Unsafe.Add(ref buffer, 14), -cospi[29], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 16) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[33], Unsafe.Add(ref buffer, 16), cospi[31], Unsafe.Add(ref buffer, 17), cosBit);
        Unsafe.Add(ref step, 17) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[31], Unsafe.Add(ref buffer, 16), -cospi[33], Unsafe.Add(ref buffer, 17), cosBit);
        Unsafe.Add(ref step, 18) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[37], Unsafe.Add(ref buffer, 18), cospi[27], Unsafe.Add(ref buffer, 19), cosBit);
        Unsafe.Add(ref step, 19) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[27], Unsafe.Add(ref buffer, 18), -cospi[37], Unsafe.Add(ref buffer, 19), cosBit);
        Unsafe.Add(ref step, 20) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[41], Unsafe.Add(ref buffer, 20), cospi[23], Unsafe.Add(ref buffer, 21), cosBit);
        Unsafe.Add(ref step, 21) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[23], Unsafe.Add(ref buffer, 20), -cospi[41], Unsafe.Add(ref buffer, 21), cosBit);
        Unsafe.Add(ref step, 22) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[45], Unsafe.Add(ref buffer, 22), cospi[19], Unsafe.Add(ref buffer, 23), cosBit);
        Unsafe.Add(ref step, 23) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[19], Unsafe.Add(ref buffer, 22), -cospi[45], Unsafe.Add(ref buffer, 23), cosBit);
        Unsafe.Add(ref step, 24) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[49], Unsafe.Add(ref buffer, 24), cospi[15], Unsafe.Add(ref buffer, 25), cosBit);
        Unsafe.Add(ref step, 25) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[15], Unsafe.Add(ref buffer, 24), -cospi[49], Unsafe.Add(ref buffer, 25), cosBit);
        Unsafe.Add(ref step, 26) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[53], Unsafe.Add(ref buffer, 26), cospi[11], Unsafe.Add(ref buffer, 27), cosBit);
        Unsafe.Add(ref step, 27) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[11], Unsafe.Add(ref buffer, 26), -cospi[53], Unsafe.Add(ref buffer, 27), cosBit);
        Unsafe.Add(ref step, 28) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[57], Unsafe.Add(ref buffer, 28), cospi[7], Unsafe.Add(ref buffer, 29), cosBit);
        Unsafe.Add(ref step, 29) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[7], Unsafe.Add(ref buffer, 28), -cospi[57], Unsafe.Add(ref buffer, 29), cosBit);
        Unsafe.Add(ref step, 30) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[61], Unsafe.Add(ref buffer, 30), cospi[3], Unsafe.Add(ref buffer, 31), cosBit);
        Unsafe.Add(ref step, 31) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[3], Unsafe.Add(ref buffer, 30), -cospi[61], Unsafe.Add(ref buffer, 31), cosBit);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref step, stageRange[stage]);

        // stage 11
        stage++;
        Unsafe.Add(ref output, 0) = Unsafe.Add(ref step, 1);
        Unsafe.Add(ref output, 1) = Unsafe.Add(ref step, 30);
        Unsafe.Add(ref output, 2) = Unsafe.Add(ref step, 3);
        Unsafe.Add(ref output, 3) = Unsafe.Add(ref step, 28);
        Unsafe.Add(ref output, 4) = Unsafe.Add(ref step, 5);
        Unsafe.Add(ref output, 5) = Unsafe.Add(ref step, 26);
        Unsafe.Add(ref output, 6) = Unsafe.Add(ref step, 7);
        Unsafe.Add(ref output, 7) = Unsafe.Add(ref step, 24);
        Unsafe.Add(ref output, 8) = Unsafe.Add(ref step, 9);
        Unsafe.Add(ref output, 9) = Unsafe.Add(ref step, 22);
        Unsafe.Add(ref output, 10) = Unsafe.Add(ref step, 11);
        Unsafe.Add(ref output, 11) = Unsafe.Add(ref step, 20);
        Unsafe.Add(ref output, 12) = Unsafe.Add(ref step, 13);
        Unsafe.Add(ref output, 13) = Unsafe.Add(ref step, 18);
        Unsafe.Add(ref output, 14) = Unsafe.Add(ref step, 15);
        Unsafe.Add(ref output, 15) = Unsafe.Add(ref step, 16);
        Unsafe.Add(ref output, 16) = Unsafe.Add(ref step, 17);
        Unsafe.Add(ref output, 17) = Unsafe.Add(ref step, 14);
        Unsafe.Add(ref output, 18) = Unsafe.Add(ref step, 19);
        Unsafe.Add(ref output, 19) = Unsafe.Add(ref step, 12);
        Unsafe.Add(ref output, 20) = Unsafe.Add(ref step, 21);
        Unsafe.Add(ref output, 21) = Unsafe.Add(ref step, 10);
        Unsafe.Add(ref output, 22) = Unsafe.Add(ref step, 23);
        Unsafe.Add(ref output, 23) = Unsafe.Add(ref step, 8);
        Unsafe.Add(ref output, 24) = Unsafe.Add(ref step, 25);
        Unsafe.Add(ref output, 25) = Unsafe.Add(ref step, 6);
        Unsafe.Add(ref output, 26) = Unsafe.Add(ref step, 27);
        Unsafe.Add(ref output, 27) = Unsafe.Add(ref step, 4);
        Unsafe.Add(ref output, 28) = Unsafe.Add(ref step, 29);
        Unsafe.Add(ref output, 29) = Unsafe.Add(ref step, 2);
        Unsafe.Add(ref output, 30) = Unsafe.Add(ref step, 31);
        Unsafe.Add(ref output, 31) = Unsafe.Add(ref step, 0);
        Av1Dct4Inverse1dTransformer.ClampBuffer32(ref output, stageRange[stage]);
    }
}
