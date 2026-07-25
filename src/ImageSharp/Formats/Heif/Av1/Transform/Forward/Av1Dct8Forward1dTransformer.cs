// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct8Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 8, nameof(input));
        Guard.MustBeSizedAtLeast(output, 8, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit);
    }

    private static void TransformScalar(ref int input, ref int output, int cosBit)
    {
        Span<int> temp0 = stackalloc int[8];
        Span<int> temp1 = stackalloc int[8];

        // stage 0;

        // stage 1;
        temp0[0] = Unsafe.Add(ref input, 0) + Unsafe.Add(ref input, 7);
        temp0[1] = Unsafe.Add(ref input, 1) + Unsafe.Add(ref input, 6);
        temp0[2] = Unsafe.Add(ref input, 2) + Unsafe.Add(ref input, 5);
        temp0[3] = Unsafe.Add(ref input, 3) + Unsafe.Add(ref input, 4);
        temp0[4] = -Unsafe.Add(ref input, 4) + Unsafe.Add(ref input, 3);
        temp0[5] = -Unsafe.Add(ref input, 5) + Unsafe.Add(ref input, 2);
        temp0[6] = -Unsafe.Add(ref input, 6) + Unsafe.Add(ref input, 1);
        temp0[7] = -Unsafe.Add(ref input, 7) + Unsafe.Add(ref input, 0);

        // stage 2
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        temp1[0] = temp0[0] + temp0[3];
        temp1[1] = temp0[1] + temp0[2];
        temp1[2] = -temp0[2] + temp0[1];
        temp1[3] = -temp0[3] + temp0[0];
        temp1[4] = temp0[4];
        temp1[5] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp0[5], cospi[32], temp0[6], cosBit);
        temp1[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp0[6], cospi[32], temp0[5], cosBit);
        temp1[7] = temp0[7];

        // stage 3
        temp0[0] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[32], temp1[0], cospi[32], temp1[1], cosBit);
        temp0[1] = Av1Dct4Forward1dTransformer.HalfButterfly(-cospi[32], temp1[1], cospi[32], temp1[0], cosBit);
        temp0[2] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[2], cospi[16], temp1[3], cosBit);
        temp0[3] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[48], temp1[3], -cospi[16], temp1[2], cosBit);
        temp0[4] = temp1[4] + temp1[5];
        temp0[5] = -temp1[5] + temp1[4];
        temp0[6] = -temp1[6] + temp1[7];
        temp0[7] = temp1[7] + temp1[6];

        // stage 4
        temp1[0] = temp0[0];
        temp1[1] = temp0[1];
        temp1[2] = temp0[2];
        temp1[3] = temp0[3];
        temp1[4] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp0[4], cospi[8], temp0[7], cosBit);
        temp1[5] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp0[5], cospi[40], temp0[6], cosBit);
        temp1[6] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[24], temp0[6], -cospi[40], temp0[5], cosBit);
        temp1[7] = Av1Dct4Forward1dTransformer.HalfButterfly(cospi[56], temp0[7], -cospi[8], temp0[4], cosBit);

        // stage 5
        Unsafe.Add(ref output, 0) = temp1[0];
        Unsafe.Add(ref output, 1) = temp1[4];
        Unsafe.Add(ref output, 2) = temp1[2];
        Unsafe.Add(ref output, 3) = temp1[6];
        Unsafe.Add(ref output, 4) = temp1[1];
        Unsafe.Add(ref output, 5) = temp1[5];
        Unsafe.Add(ref output, 6) = temp1[3];
        Unsafe.Add(ref output, 7) = temp1[7];
    }
}
