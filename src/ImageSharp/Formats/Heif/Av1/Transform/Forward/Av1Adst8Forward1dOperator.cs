// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the eight-point AV1 forward asymmetric discrete sine transform operator.
/// </summary>
internal readonly struct Av1Adst8Forward1dOperator : IAv1ForwardTransform1dOperator
{
    /// <inheritdoc/>
    public static void Transform<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
        => Av1ForwardTransformOperations.Adst8(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit);
}
