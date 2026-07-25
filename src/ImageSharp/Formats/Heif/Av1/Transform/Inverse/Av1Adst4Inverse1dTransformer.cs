// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

internal class Av1Adst4Inverse1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit, stageRange);
    }

    /// <summary>
    /// SVT: svt_av1_iadst4_new
    /// </summary>
    private static void TransformScalar(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        int bit = cosBit;
        Span<int> sinpi = Av1SinusConstants.SinusPi(bit);
        int s0, s1, s2, s3, s4, s5, s6, s7;

        int x0 = input;
        int x1 = Unsafe.Add(ref input, 1);
        int x2 = Unsafe.Add(ref input, 2);
        int x3 = Unsafe.Add(ref input, 3);

        if (!(x0 != 0 | x1 != 0 | x2 != 0 | x3 != 0))
        {
            output = 0;
            Unsafe.Add(ref output, 1) = 0;
            Unsafe.Add(ref output, 2) = 0;
            Unsafe.Add(ref output, 3) = 0;
            return;
        }

        Guard.IsTrue(sinpi[1] + sinpi[2] == sinpi[4], nameof(sinpi), "Sinus Pi check failed.");

        s0 = sinpi[1] * x0;
        s1 = sinpi[2] * x0;
        s2 = sinpi[3] * x1;
        s3 = sinpi[4] * x2;
        s4 = sinpi[1] * x2;
        s5 = sinpi[2] * x3;
        s6 = sinpi[4] * x3;

        s7 = (x0 - x2) + x3;

        // stage 3
        s0 = s0 + s3;
        s1 = s1 - s4;
        s3 = s2;
        s2 = sinpi[3] * s7;

        // stage 4
        s0 = s0 + s5;
        s1 = s1 - s6;

        // stage 5
        x0 = s0 + s3;
        x1 = s1 + s3;
        x2 = s2;
        x3 = s0 + s1;

        // stage 6
        x3 = x3 - s3;

        output = Av1Math.RoundShift(x0, bit);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift(x1, bit);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift(x2, bit);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift(x3, bit);

        // range_check_buf(6, input, output, 4, stage_range[6]);
    }
}
