// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Defines the four-point AV1 inverse discrete cosine transform operator.
/// </summary>
internal readonly partial struct Av1Dct4Inverse1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative four-point AV1 inverse discrete cosine transform.
    /// </summary>
    /// <param name="input">The four frequency-domain coefficients.</param>
    /// <param name="output">The four spatial-domain residual values.</param>
    /// <param name="step">The four-element stage buffer owned by the containing two-dimensional transform.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        // AV1 stores coefficients in frequency order; this permutation restores the order expected by the staged DCT.
        output[0] = input[0];
        output[1] = input[2];
        output[2] = input[1];
        output[3] = input[3];

        // Rotate the even and odd coefficient pairs using the same fixed-point basis as the forward transform.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);

        // The terminal butterflies reconstruct spatial order and clamp every result to the normative stage range.
        byte range = stageRange[3];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
        output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
        output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
    }
}
