// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the 4-point AV1 forward identity transform operator.
/// </summary>
internal readonly partial struct Av1Identity4Forward1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative 4-point AV1 forward identity transform.
    /// </summary>
    /// <param name="input">The four spatial-domain residual values.</param>
    /// <param name="output">The four scaled transform values.</param>
    /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
    /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
    /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        _ = step;
        _ = cosBit;
        _ = stageRange;

        // The AV1 identity transform preserves sample order while applying a square-root-of-two fixed-point scale for 2-D normalization.
        for (int i = 0; i < 4; i++)
        {
            output[i] = Av1Math.RoundShift((long)input[i] * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        }
    }
}
