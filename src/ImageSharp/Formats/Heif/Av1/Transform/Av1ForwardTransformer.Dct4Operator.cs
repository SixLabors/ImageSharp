// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the four-point discrete cosine forward transform operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Defines the four-point AV1 forward discrete cosine transform operator.
    /// </summary>
    internal readonly struct Dct4Operator : IAv1ForwardTransform1dOperator
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
            => Av1ForwardTransformOperations.Dct4(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit);
    }
}
