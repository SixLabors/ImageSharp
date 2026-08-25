// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Defines the four-point AV1 inverse asymmetric discrete sine transform operator.
/// </summary>
internal readonly partial struct Av1Adst4Inverse1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative four-point AV1 inverse asymmetric discrete sine transform.
    /// </summary>
    /// <param name="input">The four frequency-domain coefficients.</param>
    /// <param name="output">The four spatial-domain residual values.</param>
    /// <param name="step">The stage buffer owned by the containing two-dimensional transform.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);

        // libaom widens the complete four-point factorization because the products retain their fixed-point scale
        // until the final shift. The stage buffer is therefore unnecessary for this transform size.
        long x0 = input[0];
        long x1 = input[1];
        long x2 = input[2];
        long x3 = input[3];

        _ = step;
        _ = stageRange;

        // Avoid the multiplications for the all-zero coefficient vector, matching libaom's scalar kernel.
        if ((x0 | x1 | x2 | x3) == 0)
        {
            output[..4].Clear();
            return;
        }

        // Stages 1 and 2 form the seven sine products and the one unscaled combination used by stage 3.
        long s0 = sinpi[1] * x0;
        long s1 = sinpi[2] * x0;
        long s2 = sinpi[3] * x1;
        long s3 = sinpi[4] * x2;
        long s4 = sinpi[1] * x2;
        long s5 = sinpi[2] * x3;
        long s6 = sinpi[4] * x3;
        long s7 = (x0 - x2) + x3;

        // Stages 3 through 6 combine the products while preserving the fixed-point scale until the final rounding.
        s0 += s3;
        s1 -= s4;
        s3 = s2;
        s2 = sinpi[3] * s7;
        s0 += s5;
        s1 -= s6;
        x0 = s0 + s3;
        x1 = s1 + s3;
        x2 = s2;
        x3 = (s0 + s1) - s3;

        output[0] = Av1Math.RoundShift(x0, cosBit);
        output[1] = Av1Math.RoundShift(x1, cosBit);
        output[2] = Av1Math.RoundShift(x2, cosBit);
        output[3] = Av1Math.RoundShift(x3, cosBit);
    }
}
