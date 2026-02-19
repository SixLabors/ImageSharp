// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct4Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit);
    }

    private static void TransformScalar(ref int input, ref int output, int cosBit)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        ref int bf0 = ref output;
        ref int bf1 = ref output;
        Span<int> stepSpan = new int[4];
        ref int step0 = ref stepSpan[0];
        ref int step1 = ref Unsafe.Add(ref step0, 1);
        ref int step2 = ref Unsafe.Add(ref step0, 2);
        ref int step3 = ref Unsafe.Add(ref step0, 3);
        ref int output1 = ref Unsafe.Add(ref output, 1);
        ref int output2 = ref Unsafe.Add(ref output, 2);
        ref int output3 = ref Unsafe.Add(ref output, 3);

        // stage 0;

        // stage 1;
        output = input + Unsafe.Add(ref input, 3);
        output1 = Unsafe.Add(ref input, 1) + Unsafe.Add(ref input, 2);
        output2 = -Unsafe.Add(ref input, 2) + Unsafe.Add(ref input, 1);
        output3 = -Unsafe.Add(ref input, 3) + Unsafe.Add(ref input, 0);

        // stage 2
        step0 = HalfButterfly(cospi[32], output, cospi[32], output1, cosBit);
        step1 = HalfButterfly(-cospi[32], output1, cospi[32], output, cosBit);
        step2 = HalfButterfly(cospi[48], output2, cospi[16], output3, cosBit);
        step3 = HalfButterfly(cospi[48], output3, -cospi[16], output2, cosBit);

        // stage 3
        output = step0;
        output1 = step2;
        output2 = step1;
        output3 = step3;
    }

    internal static int HalfButterfly(int w0, int in0, int w1, int in1, int bit)
    {
        long result64 = (long)(w0 * in0) + (w1 * in1);
        long intermediate = result64 + (1L << (bit - 1));

        // NOTE(david.barker): The value 'result_64' may not necessarily fit
        // into 32 bits. However, the result of this function is nominally
        // ROUND_POWER_OF_TWO_64(result_64, bit)
        // and that is required to fit into stage_range[stage] many bits
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
