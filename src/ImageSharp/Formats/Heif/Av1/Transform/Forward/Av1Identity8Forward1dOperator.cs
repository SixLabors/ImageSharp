// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the 8-point AV1 forward identity transform operator.
/// </summary>
internal readonly partial struct Av1Identity8Forward1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative 8-point AV1 forward identity transform.
    /// </summary>
    /// <param name="input">The eight spatial-domain residual values.</param>
    /// <param name="output">The eight scaled transform values.</param>
    /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
    /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
    /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        _ = step;
        _ = cosBit;
        _ = stageRange;

        // The AV1 identity transform preserves sample order while applying an exact factor-of-two scale for 2-D normalization.
        for (int i = 0; i < 8; i++)
        {
            output[i] = input[i] << 1;
        }
    }
}
