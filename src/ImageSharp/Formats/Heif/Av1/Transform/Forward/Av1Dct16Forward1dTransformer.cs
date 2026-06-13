// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct16Forward1dTransformer : IAv1Transformer1d
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
        temp0[0] = Unsafe.Add(ref input, 0) + Unsafe.Add(ref input, 15);
        temp0[1] = Unsafe.Add(ref input, 1) + Unsafe.Add(ref input, 14);
        temp0[2] = Unsafe.Add(ref input, 2) + Unsafe.Add(ref input, 13);
        temp0[3] = Unsafe.Add(ref input, 3) + Unsafe.Add(ref input, 12);
        temp0[4] = Unsafe.Add(ref input, 4) + Unsafe.Add(ref input, 11);
        temp0[5] = Unsafe.Add(ref input, 5) + Unsafe.Add(ref input, 10);
        temp0[6] = Unsafe.Add(ref input, 6) + Unsafe.Add(ref input, 9);
        temp0[7] = Unsafe.Add(ref input, 7) + Unsafe.Add(ref input, 8);
        temp0[8] = -Unsafe.Add(ref input, 8) + Unsafe.Add(ref input, 7);
        temp0[9] = -Unsafe.Add(ref input, 9) + Unsafe.Add(ref input, 6);
        temp0[10] = -Unsafe.Add(ref input, 10) + Unsafe.Add(ref input, 5);
        temp0[11] = -Unsafe.Add(ref input, 11) + Unsafe.Add(ref input, 4);
        temp0[12] = -Unsafe.Add(ref input, 12) + Unsafe.Add(ref input, 3);
        temp0[13] = -Unsafe.Add(ref input, 13) + Unsafe.Add(ref input, 2);
        temp0[14] = -Unsafe.Add(ref input, 14) + Unsafe.Add(ref input, 1);
        temp0[15] = -Unsafe.Add(ref input, 15) + Unsafe.Add(ref input, 0);

        // stage 2
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        temp1[0] = temp0[0] + temp0[7];
        temp1[1] = temp0[1] + temp0[6];
        temp1[2] = temp0[2] + temp0[5];
        temp1[3] = temp0[3] + temp0[4];
        temp1[4] = -temp0[4] + temp0[3];
        temp1[5] = -temp0[5] + temp0[2];
        temp1[6] = -temp0[6] + temp0[1];
        temp1[7] = -temp0[7] + temp0[0];
        temp1[8] = temp0[8];
        temp1[9] = temp0[9];
        temp1[10] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[10], cospi[32], temp0[13], cosBit);
        temp1[11] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[11], cospi[32], temp0[12], cosBit);
        temp1[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[12], cospi[32], temp0[11], cosBit);
        temp1[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[13], cospi[32], temp0[10], cosBit);
        temp1[14] = temp0[14];
        temp1[15] = temp0[15];

        // stage 3
        temp0[0] = temp1[0] + temp1[3];
        temp0[1] = temp1[1] + temp1[2];
        temp0[2] = -temp1[2] + temp1[1];
        temp0[3] = -temp1[3] + temp1[0];
        temp0[4] = temp1[4];
        temp0[5] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp1[5], cospi[32], temp1[6], cosBit);
        temp0[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[6], cospi[32], temp1[5], cosBit);
        temp0[7] = temp1[7];
        temp0[8] = temp1[8] + temp1[11];
        temp0[9] = temp1[9] + temp1[10];
        temp0[10] = -temp1[10] + temp1[9];
        temp0[11] = -temp1[11] + temp1[8];
        temp0[12] = -temp1[12] + temp1[15];
        temp0[13] = -temp1[13] + temp1[14];
        temp0[14] = temp1[14] + temp1[13];
        temp0[15] = temp1[15] + temp1[12];

        // stage 4
        temp1[0] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[0], cospi[32], temp0[1], cosBit);
        temp1[1] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[1], cospi[32], temp0[0], cosBit);
        temp1[2] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp0[2], cospi[16], temp0[3], cosBit);
        temp1[3] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp0[3], -cospi[16], temp0[2], cosBit);
        temp1[4] = temp0[4] + temp0[5];
        temp1[5] = -temp0[5] + temp0[4];
        temp1[6] = -temp0[6] + temp0[7];
        temp1[7] = temp0[7] + temp0[6];
        temp1[8] = temp0[8];
        temp1[9] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[16], temp0[9], cospi[48], temp0[14], cosBit);
        temp1[10] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[48], temp0[10], -cospi[16], temp0[13], cosBit);
        temp1[11] = temp0[11];
        temp1[12] = temp0[12];
        temp1[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp0[13], -cospi[16], temp0[10], cosBit);
        temp1[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[16], temp0[14], cospi[48], temp0[9], cosBit);
        temp1[15] = temp0[15];

        // stage 5
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp1[4], cospi[8], temp1[7], cosBit);
        temp0[5] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp1[5], cospi[40], temp1[6], cosBit);
        temp0[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp1[6], -cospi[40], temp1[5], cosBit);
        temp0[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp1[7], -cospi[8], temp1[4], cosBit);
        temp0[8] = temp1[8] + temp1[9];
        temp0[9] = -temp1[9] + temp1[8];
        temp0[10] = -temp1[10] + temp1[11];
        temp0[11] = temp1[11] + temp1[10];
        temp0[12] = temp1[12] + temp1[13];
        temp0[13] = -temp1[13] + temp1[12];
        temp0[14] = -temp1[14] + temp1[15];
        temp0[15] = temp1[15] + temp1[14];

        // stage 6
        temp1[0] = temp0[0];
        temp1[1] = temp0[1];
        temp1[2] = temp0[2];
        temp1[3] = temp0[3];
        temp1[4] = temp0[4];
        temp1[5] = temp0[5];
        temp1[6] = temp0[6];
        temp1[7] = temp0[7];
        temp1[8] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[60], temp0[8], cospi[4], temp0[15], cosBit);
        temp1[9] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[28], temp0[9], cospi[36], temp0[14], cosBit);
        temp1[10] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[44], temp0[10], cospi[20], temp0[13], cosBit);
        temp1[11] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[12], temp0[11], cospi[52], temp0[12], cosBit);
        temp1[12] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[12], temp0[12], -cospi[52], temp0[11], cosBit);
        temp1[13] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[44], temp0[13], -cospi[20], temp0[10], cosBit);
        temp1[14] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[28], temp0[14], -cospi[36], temp0[9], cosBit);
        temp1[15] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[60], temp0[15], -cospi[4], temp0[8], cosBit);

        // stage 7
        output = temp1[0];
        Unsafe.Add(ref output, 1) = temp1[8];
        Unsafe.Add(ref output, 2) = temp1[4];
        Unsafe.Add(ref output, 3) = temp1[12];
        Unsafe.Add(ref output, 4) = temp1[2];
        Unsafe.Add(ref output, 5) = temp1[10];
        Unsafe.Add(ref output, 6) = temp1[6];
        Unsafe.Add(ref output, 7) = temp1[14];
        Unsafe.Add(ref output, 8) = temp1[1];
        Unsafe.Add(ref output, 9) = temp1[9];
        Unsafe.Add(ref output, 10) = temp1[5];
        Unsafe.Add(ref output, 11) = temp1[13];
        Unsafe.Add(ref output, 12) = temp1[3];
        Unsafe.Add(ref output, 13) = temp1[11];
        Unsafe.Add(ref output, 14) = temp1[7];
        Unsafe.Add(ref output, 15) = temp1[15];
    }
}
