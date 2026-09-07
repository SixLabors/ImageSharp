// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the thirty-two-point AV1 inverse identity transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. Scaling is lane-local,
/// so the SIMD overloads preserve the scalar fixed-point multiplier and rounding for every axis.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Identity32Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 32;

        /// <summary>
        /// Applies the normative thirty-two-point AV1 inverse identity transform.
        /// </summary>
        /// <param name="input">The thirty-two frequency-domain coefficients.</param>
        /// <param name="output">The thirty-two scaled spatial-domain values.</param>
        /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
        /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
        /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            _ = step;
            _ = cosBit;
            _ = stageRange;

            // The AV1 identity transform preserves coefficient order while applying the exact factor-of-four scale required for 2-D normalization.
            for (int i = 0; i < 32; i++)
            {
                output[i] = input[i] * 4;
            }
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            Av1IdentityTransform1d.Transform(ref input, ref output, 32, 4, 0);
            _ = step;
            _ = cosBit;
            _ = stageRange;
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            Av1IdentityTransform1d.Transform(ref input, ref output, 32, 4, 0);
            _ = step;
            _ = cosBit;
            _ = stageRange;
        }
    }
}
