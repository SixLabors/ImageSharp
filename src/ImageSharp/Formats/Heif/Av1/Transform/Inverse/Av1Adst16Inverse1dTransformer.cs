// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Adst16Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 16, nameof(input));
        Guard.MustBeSizedAtLeast(output, 16, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_iadst16_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

        int stage = 0;
        Span<int> stepSpan = stackalloc int[16];
        ref int step = ref stepSpan[0];
        Span<int> bufferSpan = stackalloc int[16];
        ref int buffer = ref bufferSpan[0];

        // stage 0;

        // stage 1;
        stage++;
        buffer = Unsafe.Add(ref input, 15);
        Unsafe.Add(ref buffer, 1) = input;
        Unsafe.Add(ref buffer, 2) = Unsafe.Add(ref input, 13);
        Unsafe.Add(ref buffer, 3) = Unsafe.Add(ref input, 2);
        Unsafe.Add(ref buffer, 4) = Unsafe.Add(ref input, 11);
        Unsafe.Add(ref buffer, 5) = Unsafe.Add(ref input, 4);
        Unsafe.Add(ref buffer, 6) = Unsafe.Add(ref input, 9);
        Unsafe.Add(ref buffer, 7) = Unsafe.Add(ref input, 6);
        Unsafe.Add(ref buffer, 8) = Unsafe.Add(ref input, 7);
        Unsafe.Add(ref buffer, 9) = Unsafe.Add(ref input, 8);
        Unsafe.Add(ref buffer, 10) = Unsafe.Add(ref input, 5);
        Unsafe.Add(ref buffer, 11) = Unsafe.Add(ref input, 10);
        Unsafe.Add(ref buffer, 12) = Unsafe.Add(ref input, 3);
        Unsafe.Add(ref buffer, 13) = Unsafe.Add(ref input, 12);
        Unsafe.Add(ref buffer, 14) = Unsafe.Add(ref input, 1);
        Unsafe.Add(ref buffer, 15) = Unsafe.Add(ref input, 14);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        step = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[2], buffer, cospi[62], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 1) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[62], buffer, -cospi[2], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 2) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[10], Unsafe.Add(ref buffer, 2), cospi[54], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 3) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[54], Unsafe.Add(ref buffer, 2), -cospi[10], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 4) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[18], Unsafe.Add(ref buffer, 4), cospi[46], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 5) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[46], Unsafe.Add(ref buffer, 4), -cospi[18], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[26], Unsafe.Add(ref buffer, 6), cospi[38], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[38], Unsafe.Add(ref buffer, 6), -cospi[26], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 8) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[34], Unsafe.Add(ref buffer, 8), cospi[30], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 9) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[30], Unsafe.Add(ref buffer, 8), -cospi[34], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 10) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[42], Unsafe.Add(ref buffer, 10), cospi[22], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 11) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[22], Unsafe.Add(ref buffer, 10), -cospi[42], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 12) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[50], Unsafe.Add(ref buffer, 12), cospi[14], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 13) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[14], Unsafe.Add(ref buffer, 12), -cospi[50], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 14) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[58], Unsafe.Add(ref buffer, 14), cospi[6], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 15) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[14], Unsafe.Add(ref buffer, 14), -cospi[58], Unsafe.Add(ref buffer, 15), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        byte range = stageRange[stage];
        buffer = Av1Dct4Inverse1dTransformer.ClampValue(step + Unsafe.Add(ref step, 8), range);
        Unsafe.Add(ref buffer, 1) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) + Unsafe.Add(ref step, 9), range);
        Unsafe.Add(ref buffer, 2) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) + Unsafe.Add(ref step, 10), range);
        Unsafe.Add(ref buffer, 3) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) + Unsafe.Add(ref step, 11), range);
        Unsafe.Add(ref buffer, 4) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 4) + Unsafe.Add(ref step, 12), range);
        Unsafe.Add(ref buffer, 5) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 5) + Unsafe.Add(ref step, 13), range);
        Unsafe.Add(ref buffer, 6) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 6) + Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 7) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 7) + Unsafe.Add(ref step, 15), range);
        Unsafe.Add(ref buffer, 8) = Av1Dct4Inverse1dTransformer.ClampValue(step - Unsafe.Add(ref step, 8), range);
        Unsafe.Add(ref buffer, 9) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) - Unsafe.Add(ref step, 9), range);
        Unsafe.Add(ref buffer, 10) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) - Unsafe.Add(ref step, 10), range);
        Unsafe.Add(ref buffer, 11) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) - Unsafe.Add(ref step, 11), range);
        Unsafe.Add(ref buffer, 12) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 4) - Unsafe.Add(ref step, 12), range);
        Unsafe.Add(ref buffer, 13) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 5) - Unsafe.Add(ref step, 13), range);
        Unsafe.Add(ref buffer, 14) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 6) - Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 15) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 7) - Unsafe.Add(ref step, 15), range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        step = buffer;
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Unsafe.Add(ref buffer, 2);
        Unsafe.Add(ref step, 3) = Unsafe.Add(ref buffer, 3);
        Unsafe.Add(ref step, 4) = Unsafe.Add(ref buffer, 4);
        Unsafe.Add(ref step, 5) = Unsafe.Add(ref buffer, 5);
        Unsafe.Add(ref step, 6) = Unsafe.Add(ref buffer, 6);
        Unsafe.Add(ref step, 7) = Unsafe.Add(ref buffer, 7);
        Unsafe.Add(ref step, 8) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], Unsafe.Add(ref buffer, 8), cospi[56], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 9) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[56], Unsafe.Add(ref buffer, 8), -cospi[8], Unsafe.Add(ref buffer, 9), cosBit);
        Unsafe.Add(ref step, 10) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], Unsafe.Add(ref buffer, 10), cospi[24], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 11) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[24], Unsafe.Add(ref buffer, 10), -cospi[40], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 12) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[56], Unsafe.Add(ref buffer, 12), cospi[8], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 13) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], Unsafe.Add(ref buffer, 12), cospi[56], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 14) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[24], Unsafe.Add(ref buffer, 14), cospi[40], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 15) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], Unsafe.Add(ref buffer, 14), cospi[24], Unsafe.Add(ref buffer, 15), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        range = stageRange[stage];
        buffer = Av1Dct4Inverse1dTransformer.ClampValue(step + Unsafe.Add(ref step, 4), range);
        Unsafe.Add(ref buffer, 1) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) + Unsafe.Add(ref step, 5), range);
        Unsafe.Add(ref buffer, 2) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) + Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 3) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) + Unsafe.Add(ref step, 7), range);
        Unsafe.Add(ref buffer, 4) = Av1Dct4Inverse1dTransformer.ClampValue(step - Unsafe.Add(ref step, 4), range);
        Unsafe.Add(ref buffer, 5) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) - Unsafe.Add(ref step, 5), range);
        Unsafe.Add(ref buffer, 6) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) - Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 7) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) - Unsafe.Add(ref step, 7), range);
        Unsafe.Add(ref buffer, 8) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 8) + Unsafe.Add(ref step, 12), range);
        Unsafe.Add(ref buffer, 9) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 9) + Unsafe.Add(ref step, 13), range);
        Unsafe.Add(ref buffer, 10) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 10) + Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 11) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 11) + Unsafe.Add(ref step, 15), range);
        Unsafe.Add(ref buffer, 12) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 8) - Unsafe.Add(ref step, 12), range);
        Unsafe.Add(ref buffer, 13) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 9) - Unsafe.Add(ref step, 13), range);
        Unsafe.Add(ref buffer, 14) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 10) - Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 15) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 11) - Unsafe.Add(ref step, 15), range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        step = buffer;
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Unsafe.Add(ref buffer, 2);
        Unsafe.Add(ref step, 3) = Unsafe.Add(ref buffer, 3);
        Unsafe.Add(ref step, 4) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 4), cospi[48], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 5) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], Unsafe.Add(ref buffer, 4), -cospi[16], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], Unsafe.Add(ref buffer, 6), cospi[16], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 6), cospi[48], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 8) = Unsafe.Add(ref buffer, 8);
        Unsafe.Add(ref step, 9) = Unsafe.Add(ref buffer, 9);
        Unsafe.Add(ref step, 10) = Unsafe.Add(ref buffer, 10);
        Unsafe.Add(ref step, 11) = Unsafe.Add(ref buffer, 11);
        Unsafe.Add(ref step, 12) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 12), cospi[48], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 13) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], Unsafe.Add(ref buffer, 12), -cospi[16], Unsafe.Add(ref buffer, 13), cosBit);
        Unsafe.Add(ref step, 14) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], Unsafe.Add(ref buffer, 14), cospi[16], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 15) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 14), cospi[48], Unsafe.Add(ref buffer, 15), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        range = stageRange[stage];
        buffer = Av1Dct4Inverse1dTransformer.ClampValue(step + Unsafe.Add(ref step, 2), range);
        Unsafe.Add(ref buffer, 1) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) + Unsafe.Add(ref step, 3), range);
        Unsafe.Add(ref buffer, 2) = Av1Dct4Inverse1dTransformer.ClampValue(step - Unsafe.Add(ref step, 2), range);
        Unsafe.Add(ref buffer, 3) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) - Unsafe.Add(ref step, 3), range);
        Unsafe.Add(ref buffer, 4) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 4) + Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 5) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 5) + Unsafe.Add(ref step, 7), range);
        Unsafe.Add(ref buffer, 6) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 4) - Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 7) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 5) - Unsafe.Add(ref step, 7), range);
        Unsafe.Add(ref buffer, 8) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 8) + Unsafe.Add(ref step, 10), range);
        Unsafe.Add(ref buffer, 9) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 9) + Unsafe.Add(ref step, 11), range);
        Unsafe.Add(ref buffer, 10) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 8) - Unsafe.Add(ref step, 10), range);
        Unsafe.Add(ref buffer, 11) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 9) - Unsafe.Add(ref step, 11), range);
        Unsafe.Add(ref buffer, 12) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 12) + Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 13) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 13) + Unsafe.Add(ref step, 15), range);
        Unsafe.Add(ref buffer, 14) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 12) - Unsafe.Add(ref step, 14), range);
        Unsafe.Add(ref buffer, 15) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 13) - Unsafe.Add(ref step, 15), range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 8
        step = buffer;
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 2), cospi[32], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 3) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 2), -cospi[32], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 4) = Unsafe.Add(ref buffer, 4);
        Unsafe.Add(ref step, 5) = Unsafe.Add(ref buffer, 5);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 6), cospi[32], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 6), -cospi[32], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 8) = Unsafe.Add(ref buffer, 8);
        Unsafe.Add(ref step, 9) = Unsafe.Add(ref buffer, 9);
        Unsafe.Add(ref step, 10) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 10), cospi[32], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 11) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 10), -cospi[32], Unsafe.Add(ref buffer, 11), cosBit);
        Unsafe.Add(ref step, 12) = Unsafe.Add(ref buffer, 12);
        Unsafe.Add(ref step, 13) = Unsafe.Add(ref buffer, 13);
        Unsafe.Add(ref step, 14) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 14), cospi[32], Unsafe.Add(ref buffer, 15), cosBit);
        Unsafe.Add(ref step, 15) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 14), -cospi[32], Unsafe.Add(ref buffer, 15), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 9
        output = step;
        Unsafe.Add(ref output, 1) = -Unsafe.Add(ref step, 8);
        Unsafe.Add(ref output, 2) = Unsafe.Add(ref step, 12);
        Unsafe.Add(ref output, 3) = -Unsafe.Add(ref step, 4);
        Unsafe.Add(ref output, 4) = Unsafe.Add(ref step, 6);
        Unsafe.Add(ref output, 5) = -Unsafe.Add(ref step, 14);
        Unsafe.Add(ref output, 6) = Unsafe.Add(ref step, 10);
        Unsafe.Add(ref output, 7) = -Unsafe.Add(ref step, 2);
        Unsafe.Add(ref output, 8) = Unsafe.Add(ref step, 3);
        Unsafe.Add(ref output, 9) = -Unsafe.Add(ref step, 11);
        Unsafe.Add(ref output, 10) = Unsafe.Add(ref step, 15);
        Unsafe.Add(ref output, 11) = -Unsafe.Add(ref step, 7);
        Unsafe.Add(ref output, 12) = Unsafe.Add(ref step, 5);
        Unsafe.Add(ref output, 13) = -Unsafe.Add(ref step, 13);
        Unsafe.Add(ref output, 14) = Unsafe.Add(ref step, 9);
        Unsafe.Add(ref output, 15) = -Unsafe.Add(ref step, 1);
    }
}
