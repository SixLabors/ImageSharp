// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Defines the thirty-two-point AV1 inverse identity transform operator.
/// </summary>
internal readonly partial struct Av1Identity32Inverse1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative thirty-two-point AV1 inverse identity transform.
    /// </summary>
    /// <param name="input">The thirty-two frequency-domain coefficients.</param>
    /// <param name="output">The thirty-two scaled spatial-domain values.</param>
    /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
    /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
    /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        _ = step;
        _ = cosBit;
        _ = stageRange;

        // The AV1 identity transform preserves coefficient order while applying the exact factor-of-four scale required for 2-D normalization.
        for (int i = 0; i < 32; i++)
        {
            output[i] = input[i] * 4;
        }
    }
}
