// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Adst16Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 16, nameof(input));
        Guard.MustBeSizedAtLeast(output, 16, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit);
    }

    private static void TransformScalar(ref int input, ref int output, int cosBit)
    {
        Span<int> temp0 = stackalloc int[16];
        Span<int> temp1 = stackalloc int[16];

        // stage 0;

        // stage 1;
        Guard.IsFalse(output == input, nameof(output), "Cannot operate on same buffer for input and output.");
        temp1[0] = input;
        temp1[1] = -Unsafe.Add(ref input, 15);
        temp1[2] = -Unsafe.Add(ref input, 7);
        temp1[3] = Unsafe.Add(ref input, 8);
        temp1[4] = -Unsafe.Add(ref input, 3);
        temp1[5] = Unsafe.Add(ref input, 12);
        temp1[6] = Unsafe.Add(ref input, 4);
        temp1[7] = -Unsafe.Add(ref input, 11);
        temp1[8] = -Unsafe.Add(ref input, 1);
        temp1[9] = Unsafe.Add(ref input, 14);
        temp1[10] = Unsafe.Add(ref input, 6);
        temp1[11] = -Unsafe.Add(ref input, 9);
        temp1[12] = Unsafe.Add(ref input, 2);
        temp1[13] = -Unsafe.Add(ref input, 13);
        temp1[14] = -Unsafe.Add(ref input, 5);
        temp1[15] = Unsafe.Add(ref input, 10);

        // stage 2
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[2], cospi[32], temp1[3], cosBit);
        temp0[3] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[2], -cospi[32], temp1[3], cosBit);
        temp0[4] = temp1[4];
        temp0[5] = temp1[5];
        temp0[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[6], cospi[32], temp1[7], cosBit);
        temp0[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[6], -cospi[32], temp1[7], cosBit);
        temp0[8] = temp1[8];
        temp0[9] = temp1[9];
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[10], cospi[32], temp1[11], cosBit);
        temp0[11] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[10], -cospi[32], temp1[11], cosBit);
        temp0[12] = temp1[12];
        temp0[13] = temp1[13];
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[14], cospi[32], temp1[15], cosBit);
        temp0[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[14], -cospi[32], temp1[15], cosBit);

        // stage 3
        temp1[0] = temp0[0] + temp0[2];
        temp1[1] = temp0[1] + temp0[3];
        temp1[2] = temp0[0] - temp0[2];
        temp1[3] = temp0[1] - temp0[3];
        temp1[4] = temp0[4] + temp0[6];
        temp1[5] = temp0[5] + temp0[7];
        temp1[6] = temp0[4] - temp0[6];
        temp1[7] = temp0[5] - temp0[7];
        temp1[8] = temp0[8] + temp0[10];
        temp1[9] = temp0[9] + temp0[11];
        temp1[10] = temp0[8] - temp0[10];
        temp1[11] = temp0[9] - temp0[11];
        temp1[12] = temp0[12] + temp0[14];
        temp1[13] = temp0[13] + temp0[15];
        temp1[14] = temp0[12] - temp0[14];
        temp1[15] = temp0[13] - temp0[15];

        // stage 4
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp1[4], cospi[48], temp1[5], cosBit);
        temp0[5] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[4], -cospi[16], temp1[5], cosBit);
        temp0[6] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp1[6], cospi[16], temp1[7], cosBit);
        temp0[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp1[6], cospi[48], temp1[7], cosBit);
        temp0[8] = temp1[8];
        temp0[9] = temp1[9];
        temp0[10] = temp1[10];
        temp0[11] = temp1[11];
        temp0[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp1[12], cospi[48], temp1[13], cosBit);
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[12], -cospi[16], temp1[13], cosBit);
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp1[14], cospi[16], temp1[15], cosBit);
        temp0[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp1[14], cospi[48], temp1[15], cosBit);

        // stage 5
        temp1[0] = temp0[0] + temp0[4];
        temp1[1] = temp0[1] + temp0[5];
        temp1[2] = temp0[2] + temp0[6];
        temp1[3] = temp0[3] + temp0[7];
        temp1[4] = temp0[0] - temp0[4];
        temp1[5] = temp0[1] - temp0[5];
        temp1[6] = temp0[2] - temp0[6];
        temp1[7] = temp0[3] - temp0[7];
        temp1[8] = temp0[8] + temp0[12];
        temp1[9] = temp0[9] + temp0[13];
        temp1[10] = temp0[10] + temp0[14];
        temp1[11] = temp0[11] + temp0[15];
        temp1[12] = temp0[8] - temp0[12];
        temp1[13] = temp0[9] - temp0[13];
        temp1[14] = temp0[10] - temp0[14];
        temp1[15] = temp0[11] - temp0[15];

        // stage 6
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = temp1[4];
        temp0[5] = temp1[5];
        temp0[6] = temp1[6];
        temp0[7] = temp1[7];
        temp0[8] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[8], temp1[8], cospi[56], temp1[9], cosBit);
        temp0[9] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp1[8], -cospi[8], temp1[9], cosBit);
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[40], temp1[10], cospi[24], temp1[11], cosBit);
        temp0[11] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp1[10], -cospi[40], temp1[11], cosBit);
        temp0[12] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[56], temp1[12], cospi[8], temp1[13], cosBit);
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[8], temp1[12], cospi[56], temp1[13], cosBit);
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[24], temp1[14], cospi[40], temp1[15], cosBit);
        temp0[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[40], temp1[14], cospi[24], temp1[15], cosBit);

        // stage 7
        temp1[0] = temp0[0] + temp0[8];
        temp1[1] = temp0[1] + temp0[9];
        temp1[2] = temp0[2] + temp0[10];
        temp1[3] = temp0[3] + temp0[11];
        temp1[4] = temp0[4] + temp0[12];
        temp1[5] = temp0[5] + temp0[13];
        temp1[6] = temp0[6] + temp0[14];
        temp1[7] = temp0[7] + temp0[15];
        temp1[8] = temp0[0] - temp0[8];
        temp1[9] = temp0[1] - temp0[9];
        temp1[10] = temp0[2] - temp0[10];
        temp1[11] = temp0[3] - temp0[11];
        temp1[12] = temp0[4] - temp0[12];
        temp1[13] = temp0[5] - temp0[13];
        temp1[14] = temp0[6] - temp0[14];
        temp1[15] = temp0[7] - temp0[15];

        // stage 8
        temp0[0] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[2], temp1[0], cospi[62], temp1[1], cosBit);
        temp0[1] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[62], temp1[0], -cospi[2], temp1[1], cosBit);
        temp0[2] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[10], temp1[2], cospi[54], temp1[3], cosBit);
        temp0[3] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[54], temp1[2], -cospi[10], temp1[3], cosBit);
        temp0[4] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[18], temp1[4], cospi[46], temp1[5], cosBit);
        temp0[5] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[46], temp1[4], -cospi[18], temp1[5], cosBit);
        temp0[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[26], temp1[6], cospi[38], temp1[7], cosBit);
        temp0[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[38], temp1[6], -cospi[26], temp1[7], cosBit);
        temp0[8] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[34], temp1[8], cospi[30], temp1[9], cosBit);
        temp0[9] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[30], temp1[8], -cospi[34], temp1[9], cosBit);
        temp0[10] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[42], temp1[10], cospi[22], temp1[11], cosBit);
        temp0[11] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[22], temp1[10], -cospi[42], temp1[11], cosBit);
        temp0[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[50], temp1[12], cospi[14], temp1[13], cosBit);
        temp0[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[14], temp1[12], -cospi[50], temp1[13], cosBit);
        temp0[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[58], temp1[14], cospi[6], temp1[15], cosBit);
        temp0[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[6], temp1[14], -cospi[58], temp1[15], cosBit);

        // stage 9
        output = temp0[1];
        Unsafe.Add(ref output, 1) = temp0[14];
        Unsafe.Add(ref output, 2) = temp0[3];
        Unsafe.Add(ref output, 3) = temp0[12];
        Unsafe.Add(ref output, 4) = temp0[5];
        Unsafe.Add(ref output, 5) = temp0[10];
        Unsafe.Add(ref output, 6) = temp0[7];
        Unsafe.Add(ref output, 7) = temp0[8];
        Unsafe.Add(ref output, 8) = temp0[9];
        Unsafe.Add(ref output, 9) = temp0[6];
        Unsafe.Add(ref output, 10) = temp0[11];
        Unsafe.Add(ref output, 11) = temp0[4];
        Unsafe.Add(ref output, 12) = temp0[13];
        Unsafe.Add(ref output, 13) = temp0[2];
        Unsafe.Add(ref output, 14) = temp0[15];
        Unsafe.Add(ref output, 15) = temp0[0];
    }
}
