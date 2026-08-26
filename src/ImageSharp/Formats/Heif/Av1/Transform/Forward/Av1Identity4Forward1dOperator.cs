// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the four-point AV1 forward identity transform operator.
/// </summary>
internal readonly struct Av1Identity4Forward1dOperator : IAv1ForwardTransform1dOperator
{
    /// <inheritdoc/>
    public static void Transform<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
        => Av1ForwardTransformOperations.Identity4(ref input, ref output, ref step, cosBit);
}
