// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Adst8Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 8, nameof(input));
        Guard.MustBeSizedAtLeast(output, 8, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_iadst8_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

        int stage = 0;
        Span<int> stepSpan = stackalloc int[8];
        ref int step = ref stepSpan[0];
        Span<int> bufferSpan = stackalloc int[8];
        ref int buffer = ref bufferSpan[0];

        // stage 0;

        // stage 1;
        stage++;
        buffer = Unsafe.Add(ref input, 7);
        Unsafe.Add(ref buffer, 1) = input;
        Unsafe.Add(ref buffer, 2) = Unsafe.Add(ref input, 5);
        Unsafe.Add(ref buffer, 3) = Unsafe.Add(ref input, 2);
        Unsafe.Add(ref buffer, 4) = Unsafe.Add(ref input, 3);
        Unsafe.Add(ref buffer, 5) = Unsafe.Add(ref input, 4);
        Unsafe.Add(ref buffer, 6) = Unsafe.Add(ref input, 1);
        Unsafe.Add(ref buffer, 7) = Unsafe.Add(ref input, 6);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        step = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[4], buffer, cospi[60], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 1) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[60], buffer, -cospi[4], Unsafe.Add(ref buffer, 1), cosBit);
        Unsafe.Add(ref step, 2) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[20], Unsafe.Add(ref buffer, 2), cospi[44], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 3) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[44], Unsafe.Add(ref buffer, 2), -cospi[20], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 4) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[36], Unsafe.Add(ref buffer, 4), cospi[28], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 5) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[28], Unsafe.Add(ref buffer, 4), -cospi[36], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[52], Unsafe.Add(ref buffer, 6), cospi[12], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[12], Unsafe.Add(ref buffer, 6), -cospi[52], Unsafe.Add(ref buffer, 7), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        byte range = stageRange[stage];
        buffer = Av1Dct4Inverse1dTransformer.ClampValue(step + Unsafe.Add(ref step, 4), range);
        Unsafe.Add(ref buffer, 1) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) + Unsafe.Add(ref step, 5), range);
        Unsafe.Add(ref buffer, 2) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) + Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 3) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) + Unsafe.Add(ref step, 7), range);
        Unsafe.Add(ref buffer, 4) = Av1Dct4Inverse1dTransformer.ClampValue(step - Unsafe.Add(ref step, 4), range);
        Unsafe.Add(ref buffer, 5) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 1) - Unsafe.Add(ref step, 5), range);
        Unsafe.Add(ref buffer, 6) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 2) - Unsafe.Add(ref step, 6), range);
        Unsafe.Add(ref buffer, 7) = Av1Dct4Inverse1dTransformer.ClampValue(Unsafe.Add(ref step, 3) - Unsafe.Add(ref step, 7), range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        step = buffer;
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Unsafe.Add(ref buffer, 2);
        Unsafe.Add(ref step, 3) = Unsafe.Add(ref buffer, 3);
        Unsafe.Add(ref step, 4) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 4), cospi[48], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 5) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], Unsafe.Add(ref buffer, 4), -cospi[16], Unsafe.Add(ref buffer, 5), cosBit);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], Unsafe.Add(ref buffer, 6), cospi[16], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], Unsafe.Add(ref buffer, 6), cospi[48], Unsafe.Add(ref buffer, 7), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
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

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        step = buffer;
        Unsafe.Add(ref step, 1) = Unsafe.Add(ref buffer, 1);
        Unsafe.Add(ref step, 2) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 2), cospi[32], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 3) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 2), -cospi[32], Unsafe.Add(ref buffer, 3), cosBit);
        Unsafe.Add(ref step, 4) = Unsafe.Add(ref buffer, 4);
        Unsafe.Add(ref step, 5) = Unsafe.Add(ref buffer, 5);
        Unsafe.Add(ref step, 6) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 6), cospi[32], Unsafe.Add(ref buffer, 7), cosBit);
        Unsafe.Add(ref step, 7) = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], Unsafe.Add(ref buffer, 6), -cospi[32], Unsafe.Add(ref buffer, 7), cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        output = step;
        Unsafe.Add(ref output, 1) = -Unsafe.Add(ref step, 4);
        Unsafe.Add(ref output, 2) = Unsafe.Add(ref step, 6);
        Unsafe.Add(ref output, 3) = -Unsafe.Add(ref step, 2);
        Unsafe.Add(ref output, 4) = Unsafe.Add(ref step, 3);
        Unsafe.Add(ref output, 5) = -Unsafe.Add(ref step, 7);
        Unsafe.Add(ref output, 6) = Unsafe.Add(ref step, 5);
        Unsafe.Add(ref output, 7) = -Unsafe.Add(ref step, 1);
    }
}
