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
    /// <param name="input">The spatial-domain transform values.</param>
    /// <param name="output">The frequency-domain transform values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static abstract void Transform<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct;
}
