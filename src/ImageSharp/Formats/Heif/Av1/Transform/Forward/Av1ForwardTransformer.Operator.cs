// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the operator contract for one-dimensional AV1 forward transforms.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Defines the scalar and SIMD arithmetic for one AV1 forward transform.
    /// </summary>
    /// <remarks>
    /// Every overload applies the same stage network to independent transform axes. The family traversal selects one
    /// concrete lane width, while the closed semantic operator lets the JIT resolve the static call before the stages.
    /// </remarks>
    internal interface IAv1ForwardTransform1dOperator
    {
        /// <summary>
        /// Transforms one expanded axis without hardware vectorization.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<int> buffer0,
            ref Av1TransformVector<int> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms one packed axis without hardware vectorization.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<short> buffer0,
            ref Av1TransformVector<short> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms eight packed axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<short>> buffer0,
            ref Av1TransformVector<Vector128<short>> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms sixteen packed axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<short>> buffer0,
            ref Av1TransformVector<Vector256<short>> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms thirty-two packed axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<short>> buffer0,
            ref Av1TransformVector<Vector512<short>> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms four expanded axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<int>> buffer0,
            ref Av1TransformVector<Vector128<int>> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms eight expanded axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<int>> buffer0,
            ref Av1TransformVector<Vector256<int>> buffer1,
            int cosBit);

        /// <summary>
        /// Transforms sixteen expanded axes in parallel.
        /// </summary>
        /// <param name="values">The first value in the strided transform storage.</param>
        /// <param name="inputStride">The byte distance between consecutive input positions.</param>
        /// <param name="outputStride">The byte distance between consecutive output positions.</param>
        /// <param name="buffer0">The first transform-stage workspace buffer.</param>
        /// <param name="buffer1">The second transform-stage workspace buffer.</param>
        /// <param name="cosBit">The fixed-point precision of the transform constants.</param>
        public static abstract void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<int>> buffer0,
            ref Av1TransformVector<Vector512<int>> buffer1,
            int cosBit);
    }
}
