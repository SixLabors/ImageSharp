// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the inverse-transform operator contract.
/// </content>
internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Defines the scalar and SIMD arithmetic for one AV1 one-dimensional inverse transform.
    /// </summary>
    /// <remarks>
    /// Each overload performs the same staged fixed-point transform. Vector fields identify coefficient positions. Vector lanes identify independent rows or
    /// columns. Each overload reads all input values before it writes the stage buffer, so the input and stage storage can alias. The butterfly operators write
    /// their stage network once over a lane type. Each overload runs that network with the matching <see cref="IAv1TransformLaneOperator{TLanes}"/>.
    /// </remarks>
    internal interface IAv1Transform1dOperator
    {
        /// <summary>
        /// Gets the number of coefficient positions read by this transform.
        /// </summary>
        public static abstract int InputLength { get; }

        /// <summary>
        /// Transforms one axis when hardware vectorization is unavailable.
        /// </summary>
        /// <remarks>
        /// Each span holds at least the transform length. The butterfly operators address the spans without bounds checks.
        /// </remarks>
        /// <param name="input">The source values for the transform axis.</param>
        /// <param name="output">The destination values for the transform axis.</param>
        /// <param name="step">The fixed stage storage for the transform axis.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static abstract void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange);

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
            InlineArray12<byte> stageRange);

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
            InlineArray12<byte> stageRange);
    }
}
