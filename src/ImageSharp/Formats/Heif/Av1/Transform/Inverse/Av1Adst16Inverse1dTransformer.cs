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
    private static void TransformScalar(ref int input, ref int output, int cos_bit, Span<byte> stage_range)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cos_bit);

        int stage = 0;
        Span<int> bf0 = stackalloc int[16];
        Span<int> bf1 = stackalloc int[16];

        // stage 0;

        // stage 1;
        stage++;
        bf0[0] = Unsafe.Add(ref input, 15);
        bf0[1] = Unsafe.Add(ref input, 0);
        bf0[2] = Unsafe.Add(ref input, 13);
        bf0[3] = Unsafe.Add(ref input, 2);
        bf0[4] = Unsafe.Add(ref input, 11);
        bf0[5] = Unsafe.Add(ref input, 4);
        bf0[6] = Unsafe.Add(ref input, 9);
        bf0[7] = Unsafe.Add(ref input, 6);
        bf0[8] = Unsafe.Add(ref input, 7);
        bf0[9] = Unsafe.Add(ref input, 8);
        bf0[10] = Unsafe.Add(ref input, 5);
        bf0[11] = Unsafe.Add(ref input, 10);
        bf0[12] = Unsafe.Add(ref input, 3);
        bf0[13] = Unsafe.Add(ref input, 12);
        bf0[14] = Unsafe.Add(ref input, 1);
        bf0[15] = Unsafe.Add(ref input, 14);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        bf1[0] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[2], bf0[0], cospi[62], bf0[1], cos_bit);
        bf1[1] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[62], bf0[0], -cospi[2], bf0[1], cos_bit);
        bf1[2] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[10], bf0[2], cospi[54], bf0[3], cos_bit);
        bf1[3] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[54], bf0[2], -cospi[10], bf0[3], cos_bit);
        bf1[4] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[18], bf0[4], cospi[46], bf0[5], cos_bit);
        bf1[5] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[46], bf0[4], -cospi[18], bf0[5], cos_bit);
        bf1[6] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[26], bf0[6], cospi[38], bf0[7], cos_bit);
        bf1[7] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[38], bf0[6], -cospi[26], bf0[7], cos_bit);
        bf1[8] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[34], bf0[8], cospi[30], bf0[9], cos_bit);
        bf1[9] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[30], bf0[8], -cospi[34], bf0[9], cos_bit);
        bf1[10] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[42], bf0[10], cospi[22], bf0[11], cos_bit);
        bf1[11] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[22], bf0[10], -cospi[42], bf0[11], cos_bit);
        bf1[12] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[50], bf0[12], cospi[14], bf0[13], cos_bit);
        bf1[13] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[14], bf0[12], -cospi[50], bf0[13], cos_bit);
        bf1[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[58], bf0[14], cospi[6], bf0[15], cos_bit);
        bf1[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[6], bf0[14], -cospi[58], bf0[15], cos_bit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        bf0[0] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] + bf1[8], stage_range[stage]);
        bf0[1] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] + bf1[9], stage_range[stage]);
        bf0[2] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[2] + bf1[10], stage_range[stage]);
        bf0[3] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[3] + bf1[11], stage_range[stage]);
        bf0[4] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[4] + bf1[12], stage_range[stage]);
        bf0[5] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[5] + bf1[13], stage_range[stage]);
        bf0[6] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[6] + bf1[14], stage_range[stage]);
        bf0[7] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[7] + bf1[15], stage_range[stage]);
        bf0[8] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] - bf1[8], stage_range[stage]);
        bf0[9] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] - bf1[9], stage_range[stage]);
        bf0[10] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[2] - bf1[10], stage_range[stage]);
        bf0[11] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[3] - bf1[11], stage_range[stage]);
        bf0[12] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[4] - bf1[12], stage_range[stage]);
        bf0[13] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[5] - bf1[13], stage_range[stage]);
        bf0[14] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[6] - bf1[14], stage_range[stage]);
        bf0[15] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[7] - bf1[15], stage_range[stage]);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = bf0[6];
        bf1[7] = bf0[7];
        bf1[8] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf0[8], cospi[56], bf0[9], cos_bit);
        bf1[9] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[56], bf0[8], -cospi[8], bf0[9], cos_bit);
        bf1[10] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf0[10], cospi[24], bf0[11], cos_bit);
        bf1[11] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[24], bf0[10], -cospi[40], bf0[11], cos_bit);
        bf1[12] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[56], bf0[12], cospi[8], bf0[13], cos_bit);
        bf1[13] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[8], bf0[12], cospi[56], bf0[13], cos_bit);
        bf1[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[24], bf0[14], cospi[40], bf0[15], cos_bit);
        bf1[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[40], bf0[14], cospi[24], bf0[15], cos_bit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        bf0[0] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] + bf1[4], stage_range[stage]);
        bf0[1] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] + bf1[5], stage_range[stage]);
        bf0[2] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[2] + bf1[6], stage_range[stage]);
        bf0[3] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[3] + bf1[7], stage_range[stage]);
        bf0[4] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] - bf1[4], stage_range[stage]);
        bf0[5] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] - bf1[5], stage_range[stage]);
        bf0[6] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[2] - bf1[6], stage_range[stage]);
        bf0[7] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[3] - bf1[7], stage_range[stage]);
        bf0[8] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[8] + bf1[12], stage_range[stage]);
        bf0[9] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[9] + bf1[13], stage_range[stage]);
        bf0[10] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[10] + bf1[14], stage_range[stage]);
        bf0[11] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[11] + bf1[15], stage_range[stage]);
        bf0[12] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[8] - bf1[12], stage_range[stage]);
        bf0[13] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[9] - bf1[13], stage_range[stage]);
        bf0[14] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[10] - bf1[14], stage_range[stage]);
        bf0[15] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[11] - bf1[15], stage_range[stage]);

        // range_check_buf(stage, input, bf0, size, stage_range[stage]);

        // stage 6
        stage++;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf0[4], cospi[48], bf0[5], cos_bit);
        bf1[5] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf0[4], -cospi[16], bf0[5], cos_bit);
        bf1[6] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf0[6], cospi[16], bf0[7], cos_bit);
        bf1[7] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf0[6], cospi[48], bf0[7], cos_bit);
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = bf0[10];
        bf1[11] = bf0[11];
        bf1[12] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf0[12], cospi[48], bf0[13], cos_bit);
        bf1[13] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[48], bf0[12], -cospi[16], bf0[13], cos_bit);
        bf1[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(-cospi[48], bf0[14], cospi[16], bf0[15], cos_bit);
        bf1[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[16], bf0[14], cospi[48], bf0[15], cos_bit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        bf0[0] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] + bf1[2], stage_range[stage]);
        bf0[1] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] + bf1[3], stage_range[stage]);
        bf0[2] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[0] - bf1[2], stage_range[stage]);
        bf0[3] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[1] - bf1[3], stage_range[stage]);
        bf0[4] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[4] + bf1[6], stage_range[stage]);
        bf0[5] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[5] + bf1[7], stage_range[stage]);
        bf0[6] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[4] - bf1[6], stage_range[stage]);
        bf0[7] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[5] - bf1[7], stage_range[stage]);
        bf0[8] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[8] + bf1[10], stage_range[stage]);
        bf0[9] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[9] + bf1[11], stage_range[stage]);
        bf0[10] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[8] - bf1[10], stage_range[stage]);
        bf0[11] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[9] - bf1[11], stage_range[stage]);
        bf0[12] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[12] + bf1[14], stage_range[stage]);
        bf0[13] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[13] + bf1[15], stage_range[stage]);
        bf0[14] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[12] - bf1[14], stage_range[stage]);
        bf0[15] = Av1Dct4Inverse1dTransformer.ClampValue(bf1[13] - bf1[15], stage_range[stage]);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 8
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[2], cospi[32], bf0[3], cos_bit);
        bf1[3] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[2], -cospi[32], bf0[3], cos_bit);
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[6], cospi[32], bf0[7], cos_bit);
        bf1[7] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[6], -cospi[32], bf0[7], cos_bit);
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[10], cospi[32], bf0[11], cos_bit);
        bf1[11] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[10], -cospi[32], bf0[11], cos_bit);
        bf1[12] = bf0[12];
        bf1[13] = bf0[13];
        bf1[14] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[14], cospi[32], bf0[15], cos_bit);
        bf1[15] = Av1Dct4Inverse1dTransformer.HalfButterfly(cospi[32], bf0[14], -cospi[32], bf0[15], cos_bit);

        // range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 9
        Unsafe.Add(ref output, 0) = bf1[0];
        Unsafe.Add(ref output, 1) = -bf1[8];
        Unsafe.Add(ref output, 2) = bf1[12];
        Unsafe.Add(ref output, 3) = -bf1[4];
        Unsafe.Add(ref output, 4) = bf1[6];
        Unsafe.Add(ref output, 5) = -bf1[14];
        Unsafe.Add(ref output, 6) = bf1[10];
        Unsafe.Add(ref output, 7) = -bf1[2];
        Unsafe.Add(ref output, 8) = bf1[3];
        Unsafe.Add(ref output, 9) = -bf1[11];
        Unsafe.Add(ref output, 10) = bf1[15];
        Unsafe.Add(ref output, 11) = -bf1[7];
        Unsafe.Add(ref output, 12) = bf1[5];
        Unsafe.Add(ref output, 13) = -bf1[13];
        Unsafe.Add(ref output, 14) = bf1[9];
        Unsafe.Add(ref output, 15) = -bf1[1];
    }
}
