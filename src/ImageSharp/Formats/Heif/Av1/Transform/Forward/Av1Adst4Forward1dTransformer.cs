// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Adst4Forward1dTransformer : IAv1Transformer1d
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0], cosBit);
    }

    private static void TransformScalar(ref int input, ref int output, int cosBit)
    {
        Span<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
        int x0, x1, x2, x3;
        int s0, s1, s2, s3, s4, s5, s6, s7;

        // stage 0
        x0 = input;
        x1 = Unsafe.Add(ref input, 1);
        x2 = Unsafe.Add(ref input, 2);
        x3 = Unsafe.Add(ref input, 3);

        if (!(x0 != 0 | x1 != 0 | x2 != 0 | x3 != 0))
        {
            output = 0;
            Unsafe.Add(ref output, 1) = 0;
            Unsafe.Add(ref output, 2) = 0;
            Unsafe.Add(ref output, 3) = 0;
            return;
        }

        // stage 1
        s0 = sinpi[1] * x0;
        s1 = sinpi[4] * x0;
        s2 = sinpi[2] * x1;
        s3 = sinpi[1] * x1;
        s4 = sinpi[3] * x2;
        s5 = sinpi[4] * x3;
        s6 = sinpi[2] * x3;
        s7 = x0 + x1;

        // stage 2
        s7 -= x3;

        // stage 3
        x0 = s0 + s2;
        x1 = sinpi[3] * s7;
        x2 = s1 - s3;
        x3 = s4;

        // stage 4
        x0 += s5;
        x2 += s6;

        // stage 5
        s0 = x0 + x3;
        s1 = x1;
        s2 = x2 - x3;
        s3 = x2 - x0;

        // stage 6
        s3 += x3;

        // 1-D transform scaling factor is sqrt(2).
        output = Av1Math.RoundShift(s0, cosBit);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift(s1, cosBit);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift(s2, cosBit);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift(s3, cosBit);
    }
}
