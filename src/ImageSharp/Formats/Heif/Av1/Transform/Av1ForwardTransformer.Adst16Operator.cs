// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the sixteen-point asymmetric discrete sine forward transform operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Defines the sixteen-point AV1 forward asymmetric discrete sine transform operator.
    /// </summary>
    internal readonly struct Adst16Operator : IAv1ForwardTransform1dOperator
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
            => Av1ForwardTransformOperations.Adst16(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit);
    }
}
