// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the four-point identity inverse transform operator.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// Defines the four-point AV1 inverse identity transform operator.
    /// </summary>
    /// <remarks>
    /// Vector fields represent transform positions and vector lanes represent independent axes. Scaling is lane-local,
    /// so the SIMD overloads preserve the scalar fixed-point multiplier and rounding for every axis.
    /// </remarks>
    internal readonly struct Identity4Operator : IAv1InverseTransform1dOperator
    {
        /// <summary>
        /// Applies the normative four-point AV1 inverse identity transform.
        /// </summary>
        /// <param name="input">The four frequency-domain coefficients.</param>
        /// <param name="output">The four scaled spatial-domain values.</param>
        /// <param name="step">Unused stage storage supplied by the common transform-kernel contract.</param>
        /// <param name="cosBit">Unused cosine precision supplied by the common transform-kernel contract.</param>
        /// <param name="stageRange">The signed-bit range assigned to the transform output.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
        {
            _ = step;
            _ = cosBit;
            _ = stageRange;

            // The AV1 identity transform preserves coefficient order while applying the square-root-of-two fixed-point scale required for 2-D normalization.
            for (int i = 0; i < 4; i++)
            {
                output[i] = Av1Math.RoundShift((long)input[i] * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
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
            // Only a twelve-bit row transform has the 20-bit input range that can overflow this fixed-point product.
            // Match libaom's high-bit-depth kernel there while retaining the compact Int32 path for narrower ranges.
            if (stageRange[0] >= WidenedIntermediateBitCount)
            {
                Av1IdentityTransform1d.TransformWidened(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }
            else
            {
                Av1IdentityTransform1d.Transform(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
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
            if (stageRange[0] >= WidenedIntermediateBitCount)
            {
                Av1IdentityTransform1d.TransformWidened(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }
            else
            {
                Av1IdentityTransform1d.Transform(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }

            _ = step;
            _ = cosBit;
        }
    }
}
