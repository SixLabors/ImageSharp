// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the four-point AV1 forward asymmetric discrete sine transform operator.
/// </summary>
internal readonly partial struct Av1Adst4Forward1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative four-point AV1 forward asymmetric discrete sine transform.
    /// </summary>
    /// <param name="input">The four spatial-domain residual values.</param>
    /// <param name="output">The four frequency-domain coefficients.</param>
    /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        _ = step;
        _ = stageRange;

        int x0 = input[0];
        int x1 = input[1];
        int x2 = input[2];
        int x3 = input[3];

        // Avoid the fixed-point multiplies for the common all-zero residual while producing the exact same result.
        if ((x0 | x1 | x2 | x3) == 0)
        {
            output.Clear();
            return;
        }

        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);

        // These products are the sparse four-point ADST matrix factorization from the AV1 transform definition.
        int s0 = sinpi[1] * x0;
        int s1 = sinpi[4] * x0;
        int s2 = sinpi[2] * x1;
        int s3 = sinpi[1] * x1;
        int s4 = sinpi[3] * x2;
        int s5 = sinpi[4] * x3;
        int s6 = sinpi[2] * x3;
        int s7 = x0 + x1 - x3;

        x0 = s0 + s2;
        x1 = sinpi[3] * s7;
        x2 = s1 - s3;
        x3 = s4;

        x0 += s5;
        x2 += s6;

        s0 = x0 + x3;
        s1 = x1;
        s2 = x2 - x3;
        s3 = x2 - x0 + x3;

        // The one-dimensional ADST carries a square-root-of-two scale represented by the selected sine table.
        output[0] = Av1Math.RoundShift(s0, cosBit);
        output[1] = Av1Math.RoundShift(s1, cosBit);
        output[2] = Av1Math.RoundShift(s2, cosBit);
        output[3] = Av1Math.RoundShift(s3, cosBit);
    }
}
