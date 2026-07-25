// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Dct16Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 16, nameof(input));
        Guard.MustBeSizedAtLeast(output, 16, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_idct16_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;
        Span<int> temp0 = stackalloc int[16];
        Span<int> temp1 = stackalloc int[16];

        // stage 0;

        // stage 1;
        stage++;
        temp0[0] = input;
        temp0[1] = Unsafe.Add(ref input, 8);
        temp0[2] = Unsafe.Add(ref input, 4);
        temp0[3] = Unsafe.Add(ref input, 12);
        temp0[4] = Unsafe.Add(ref input, 2);
        temp0[5] = Unsafe.Add(ref input, 10);
        temp0[6] = Unsafe.Add(ref input, 6);
        temp0[7] = Unsafe.Add(ref input, 14);
        temp0[8] = Unsafe.Add(ref input, 1);
        temp0[9] = Unsafe.Add(ref input, 9);
        temp0[10] = Unsafe.Add(ref input, 5);
        temp0[11] = Unsafe.Add(ref input, 13);
        temp0[12] = Unsafe.Add(ref input, 3);
        temp0[13] = Unsafe.Add(ref input, 11);
        temp0[14] = Unsafe.Add(ref input, 7);
        temp0[15] = Unsafe.Add(ref input, 15);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
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

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        byte range = stageRange[stage];
        temp0[0] = temp1[0];
        temp0[1] = temp1[1];
        temp0[2] = temp1[2];
        temp0[3] = temp1[3];
        temp0[4] = HalfButterfly(cospi[56], temp1[4], -cospi[8], temp1[7], cosBit);
        temp0[5] = HalfButterfly(cospi[24], temp1[5], -cospi[40], temp1[6], cosBit);
        temp0[6] = HalfButterfly(cospi[40], temp1[5], cospi[24], temp1[6], cosBit);
        temp0[7] = HalfButterfly(cospi[8], temp1[4], cospi[56], temp1[7], cosBit);
        temp0[8] = ClampValue(temp1[8] + temp1[9], range);
        temp0[9] = ClampValue(temp1[8] - temp1[9], range);
        temp0[10] = ClampValue(temp1[11] - temp1[10], range);
        temp0[11] = ClampValue(temp1[10] + temp1[11], range);
        temp0[12] = ClampValue(temp1[12] + temp1[13], range);
        temp0[13] = ClampValue(temp1[12] - temp1[13], range);
        temp0[14] = ClampValue(temp1[15] - temp1[14], range);
        temp0[15] = ClampValue(temp1[14] + temp1[15], range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        range = stageRange[stage];
        temp1[0] = HalfButterfly(cospi[32], temp0[0], cospi[32], temp0[1], cosBit);
        temp1[1] = HalfButterfly(cospi[32], temp0[0], -cospi[32], temp0[1], cosBit);
        temp1[2] = HalfButterfly(cospi[48], temp0[2], -cospi[16], temp0[3], cosBit);
        temp1[3] = HalfButterfly(cospi[16], temp0[2], cospi[48], temp0[3], cosBit);
        temp1[4] = ClampValue(temp0[4] + temp0[5], range);
        temp1[5] = ClampValue(temp0[4] - temp0[5], range);
        temp1[6] = ClampValue(temp0[7] - temp0[6], range);
        temp1[7] = ClampValue(temp0[6] + temp0[7], range);
        temp1[8] = temp0[8];
        temp1[9] = HalfButterfly(-cospi[16], temp0[9], cospi[48], temp0[14], cosBit);
        temp1[10] = HalfButterfly(-cospi[48], temp0[10], -cospi[16], temp0[13], cosBit);
        temp1[11] = temp0[11];
        temp1[12] = temp0[12];
        temp1[13] = HalfButterfly(-cospi[16], temp0[10], cospi[48], temp0[13], cosBit);
        temp1[14] = HalfButterfly(cospi[48], temp0[9], cospi[16], temp0[14], cosBit);
        temp1[15] = temp0[15];

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
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
        temp0[12] = ClampValue(temp1[15] - temp1[12], range);
        temp0[13] = ClampValue(temp1[14] - temp1[13], range);
        temp0[14] = ClampValue(temp1[13] + temp1[14], range);
        temp0[15] = ClampValue(temp1[12] + temp1[15], range);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
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

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        range = stageRange[stage];
        Unsafe.Add(ref output, 0) = ClampValue(temp1[0] + temp1[15], range);
        Unsafe.Add(ref output, 1) = ClampValue(temp1[1] + temp1[14], range);
        Unsafe.Add(ref output, 2) = ClampValue(temp1[2] + temp1[13], range);
        Unsafe.Add(ref output, 3) = ClampValue(temp1[3] + temp1[12], range);
        Unsafe.Add(ref output, 4) = ClampValue(temp1[4] + temp1[11], range);
        Unsafe.Add(ref output, 5) = ClampValue(temp1[5] + temp1[10], range);
        Unsafe.Add(ref output, 6) = ClampValue(temp1[6] + temp1[9], range);
        Unsafe.Add(ref output, 7) = ClampValue(temp1[7] + temp1[8], range);
        Unsafe.Add(ref output, 8) = ClampValue(temp1[7] - temp1[8], range);
        Unsafe.Add(ref output, 9) = ClampValue(temp1[6] - temp1[9], range);
        Unsafe.Add(ref output, 10) = ClampValue(temp1[5] - temp1[10], range);
        Unsafe.Add(ref output, 11) = ClampValue(temp1[4] - temp1[11], range);
        Unsafe.Add(ref output, 12) = ClampValue(temp1[3] - temp1[12], range);
        Unsafe.Add(ref output, 13) = ClampValue(temp1[2] - temp1[13], range);
        Unsafe.Add(ref output, 14) = ClampValue(temp1[1] - temp1[14], range);
        Unsafe.Add(ref output, 15) = ClampValue(temp1[0] - temp1[15], range);
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
