// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Applies the four-point AV1 forward identity transform to a one-dimensional residual vector.
/// </summary>
internal class Av1Identity4Forward1dTransformer : IAv1Transformer1d
{
    /// <inheritdoc/>
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 4, nameof(input));
        Guard.MustBeSizedAtLeast(output, 4, nameof(output));
        TransformScalar(ref input[0], ref output[0]);
    }

    /// <summary>
    /// Scales four residual values according to the AV1 forward identity-transform definition.
    /// </summary>
    /// <param name="input">A reference to the first input value.</param>
    /// <param name="output">A reference to the first output coefficient.</param>
    private static void TransformScalar(ref int input, ref int output)
    {
        output = Av1Math.RoundShift((long)input * Av1Forward2dTransformerBase.NewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 1) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 1) * Av1Forward2dTransformerBase.NewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 2) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 2) * Av1Forward2dTransformerBase.NewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
        Unsafe.Add(ref output, 3) = Av1Math.RoundShift((long)Unsafe.Add(ref input, 3) * Av1Forward2dTransformerBase.NewSqrt2, Av1Forward2dTransformerBase.NewSqrt2BitCount);
    }
}
