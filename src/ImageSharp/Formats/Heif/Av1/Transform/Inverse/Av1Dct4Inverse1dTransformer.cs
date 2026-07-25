// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Dct4Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_idct4_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;
        Span<int> temp0 = stackalloc int[4];
        Span<int> temp1 = stackalloc int[4];

        // stage 0;

        // stage 1;
        stage++;
        temp0[0] = input;
        temp0[1] = Unsafe.Add(ref input, 2);
        temp0[2] = Unsafe.Add(ref input, 1);
        temp0[3] = Unsafe.Add(ref input, 3);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        temp1[0] = HalfButterfly(cospi[32], temp0[0], cospi[32], temp0[1], cosBit);
        temp1[1] = HalfButterfly(cospi[32], temp0[0], -cospi[32], temp0[1], cosBit);
        temp1[2] = HalfButterfly(cospi[48], temp0[2], -cospi[16], temp0[3], cosBit);
        temp1[3] = HalfButterfly(cospi[16], temp0[2], cospi[48], temp0[3], cosBit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        Unsafe.Add(ref output, 0) = temp1[0] + temp1[3];
        Unsafe.Add(ref output, 1) = temp1[1] + temp1[2];
        Unsafe.Add(ref output, 2) = temp1[1] - temp1[2];
        Unsafe.Add(ref output, 3) = temp1[0] - temp1[3];
        ClampBuffer4(ref output, stageRange[stage]);
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

    internal static void ClampBuffer4(ref int buffer, byte bit)
    {
        if (bit <= 0)
        {
            return; // Do nothing for invalid clamp bit.
        }

        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));

        Unsafe.Add(ref buffer, 0) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 0), min_value, max_value);
        Unsafe.Add(ref buffer, 1) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 1), min_value, max_value);
        Unsafe.Add(ref buffer, 2) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 2), min_value, max_value);
        Unsafe.Add(ref buffer, 3) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 3), min_value, max_value);
    }

    internal static void ClampBuffer8(ref int buffer, byte bit)
    {
        if (bit <= 0)
        {
            return; // Do nothing for invalid clamp bit.
        }

        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));

        Unsafe.Add(ref buffer, 0) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 0), min_value, max_value);
        Unsafe.Add(ref buffer, 1) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 1), min_value, max_value);
        Unsafe.Add(ref buffer, 2) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 2), min_value, max_value);
        Unsafe.Add(ref buffer, 3) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 3), min_value, max_value);
        Unsafe.Add(ref buffer, 4) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 4), min_value, max_value);
        Unsafe.Add(ref buffer, 5) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 5), min_value, max_value);
        Unsafe.Add(ref buffer, 6) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 6), min_value, max_value);
        Unsafe.Add(ref buffer, 7) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 7), min_value, max_value);
    }

    internal static void ClampBuffer16(ref int buffer, byte bit)
    {
        if (bit <= 0)
        {
            return; // Do nothing for invalid clamp bit.
        }

        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));

        Unsafe.Add(ref buffer, 0) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 0), min_value, max_value);
        Unsafe.Add(ref buffer, 1) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 1), min_value, max_value);
        Unsafe.Add(ref buffer, 2) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 2), min_value, max_value);
        Unsafe.Add(ref buffer, 3) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 3), min_value, max_value);
        Unsafe.Add(ref buffer, 4) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 4), min_value, max_value);
        Unsafe.Add(ref buffer, 5) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 5), min_value, max_value);
        Unsafe.Add(ref buffer, 6) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 6), min_value, max_value);
        Unsafe.Add(ref buffer, 7) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 7), min_value, max_value);
        Unsafe.Add(ref buffer, 8) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 8), min_value, max_value);
        Unsafe.Add(ref buffer, 9) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 9), min_value, max_value);
        Unsafe.Add(ref buffer, 10) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 10), min_value, max_value);
        Unsafe.Add(ref buffer, 11) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 11), min_value, max_value);
        Unsafe.Add(ref buffer, 12) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 12), min_value, max_value);
        Unsafe.Add(ref buffer, 13) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 13), min_value, max_value);
        Unsafe.Add(ref buffer, 14) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 14), min_value, max_value);
        Unsafe.Add(ref buffer, 15) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 15), min_value, max_value);
    }

    internal static void ClampBuffer32(ref int buffer, byte bit)
    {
        if (bit <= 0)
        {
            return; // Do nothing for invalid clamp bit.
        }

        long max_value = (1L << (bit - 1)) - 1;
        long min_value = -(1L << (bit - 1));

        Unsafe.Add(ref buffer, 0) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 0), min_value, max_value);
        Unsafe.Add(ref buffer, 1) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 1), min_value, max_value);
        Unsafe.Add(ref buffer, 2) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 2), min_value, max_value);
        Unsafe.Add(ref buffer, 3) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 3), min_value, max_value);
        Unsafe.Add(ref buffer, 4) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 4), min_value, max_value);
        Unsafe.Add(ref buffer, 5) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 5), min_value, max_value);
        Unsafe.Add(ref buffer, 6) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 6), min_value, max_value);
        Unsafe.Add(ref buffer, 7) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 7), min_value, max_value);
        Unsafe.Add(ref buffer, 8) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 8), min_value, max_value);
        Unsafe.Add(ref buffer, 9) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 9), min_value, max_value);
        Unsafe.Add(ref buffer, 10) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 10), min_value, max_value);
        Unsafe.Add(ref buffer, 11) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 11), min_value, max_value);
        Unsafe.Add(ref buffer, 12) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 12), min_value, max_value);
        Unsafe.Add(ref buffer, 13) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 13), min_value, max_value);
        Unsafe.Add(ref buffer, 14) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 14), min_value, max_value);
        Unsafe.Add(ref buffer, 15) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 15), min_value, max_value);
        Unsafe.Add(ref buffer, 16) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 16), min_value, max_value);
        Unsafe.Add(ref buffer, 17) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 17), min_value, max_value);
        Unsafe.Add(ref buffer, 18) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 18), min_value, max_value);
        Unsafe.Add(ref buffer, 19) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 19), min_value, max_value);
        Unsafe.Add(ref buffer, 20) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 20), min_value, max_value);
        Unsafe.Add(ref buffer, 21) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 21), min_value, max_value);
        Unsafe.Add(ref buffer, 22) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 22), min_value, max_value);
        Unsafe.Add(ref buffer, 23) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 23), min_value, max_value);
        Unsafe.Add(ref buffer, 24) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 24), min_value, max_value);
        Unsafe.Add(ref buffer, 25) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 25), min_value, max_value);
        Unsafe.Add(ref buffer, 26) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 26), min_value, max_value);
        Unsafe.Add(ref buffer, 27) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 27), min_value, max_value);
        Unsafe.Add(ref buffer, 28) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 28), min_value, max_value);
        Unsafe.Add(ref buffer, 29) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 29), min_value, max_value);
        Unsafe.Add(ref buffer, 30) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 30), min_value, max_value);
        Unsafe.Add(ref buffer, 31) = (int)Av1Math.Clamp(Unsafe.Add(ref buffer, 31), min_value, max_value);
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
