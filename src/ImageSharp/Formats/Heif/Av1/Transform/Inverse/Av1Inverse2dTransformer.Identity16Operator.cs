// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the sixteen-point AV1 inverse identity transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. Scaling is lane-local,
/// so the SIMD overloads preserve the scalar fixed-point multiplier and rounding for every axis.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Identity16Operator : IAv1Transform1dOperator
    {
        /// <summary>
        /// Applies the normative sixteen-point AV1 inverse identity transform.
        /// </summary>
        /// <param name="input">The sixteen frequency-domain coefficients.</param>
        /// <param name="output">The sixteen scaled spatial-domain values.</param>
        /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
        /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
        /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
        {
            _ = step;
            _ = cosBit;
            _ = stageRange;

            // The AV1 identity transform preserves coefficient order while applying the twice the square-root-of-two fixed-point scale required for 2-D normalization.
            for (int i = 0; i < 16; i++)
            {
                output[i] = Av1Math.RoundShift((long)input[i] * (2 * Av1Transform1dMath.NewSqrt2), Av1Transform1dMath.NewSqrt2Bits);
            }
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            Av1TransformStageRange stageRange)
        {
            // The doubled scale exceeds Int32 only for the 20-bit twelve-bit row range. Widen that exact product and
            // rounding sequence, matching the reference decoder without changing the established lower-range SIMD path.
            if (stageRange[0] >= Av1Transform1dMath.WidenedIntermediateBitCount)
            {
                Av1IdentityTransform1d.TransformWidened(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }
            else
            {
                Av1IdentityTransform1d.Transform(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }

            _ = step;
            _ = cosBit;
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            Av1TransformStageRange stageRange)
        {
            if (stageRange[0] >= Av1Transform1dMath.WidenedIntermediateBitCount)
            {
                Av1IdentityTransform1d.TransformWidened(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }
            else
            {
                Av1IdentityTransform1d.Transform(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }

            _ = step;
            _ = cosBit;
        }
    }
}
