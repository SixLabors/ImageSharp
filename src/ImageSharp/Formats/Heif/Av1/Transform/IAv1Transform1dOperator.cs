// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the scalar and SIMD arithmetic for one AV1 one-dimensional transform.
/// </summary>
/// <remarks>
/// Each overload performs the same staged fixed-point transform. In the SIMD overloads, each vector field identifies
/// one coefficient position and each lane identifies an independent row or column. Butterfly arithmetic is therefore
/// lane-local: vectorization changes only how many axes advance together, not coefficient order, rounding, or stage
/// clamping. The two-dimensional traversal selects the concrete operator and lane width once per block, allowing the
/// JIT to specialize every static interface call outside the stage network.
/// </remarks>
internal interface IAv1Transform1dOperator
{
    /// <summary>
    /// Transforms one axis when hardware vectorization is unavailable.
    /// </summary>
    /// <param name="input">The source values for the transform axis.</param>
    /// <param name="output">The destination values for the transform axis.</param>
    /// <param name="step">The fixed stage storage for the transform axis.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static abstract void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange);

    /// <summary>
    /// Transforms four independent axes in parallel.
    /// </summary>
    /// <param name="input">The source values for four transform axes.</param>
    /// <param name="output">The destination values for four transform axes.</param>
    /// <param name="step">The fixed stage storage for four transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static abstract void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange);

    /// <summary>
    /// Transforms eight independent axes in parallel.
    /// </summary>
    /// <param name="input">The source values for eight transform axes.</param>
    /// <param name="output">The destination values for eight transform axes.</param>
    /// <param name="step">The fixed stage storage for eight transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static abstract void Transform(
        ref Av1TransformVector<Vector256<int>> input,
        ref Av1TransformVector<Vector256<int>> output,
        ref Av1TransformVector<Vector256<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange);
}
