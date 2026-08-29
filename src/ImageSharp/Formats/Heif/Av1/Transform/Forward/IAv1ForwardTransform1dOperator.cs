// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines one AV1 forward transform which can be specialized for the selected sample and SIMD lane type.
/// </summary>
/// <remarks>
/// A concrete operator identifies the transform stage network. The two-dimensional driver selects the sample type
/// and vector width once per block, allowing the JIT to specialize the complete network without interface dispatch
/// inside the transform stages.
/// </remarks>
internal interface IAv1ForwardTransform1dOperator
{
    /// <summary>
    /// Transforms the independent axes stored in each value lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static abstract void Transform<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct;
}
