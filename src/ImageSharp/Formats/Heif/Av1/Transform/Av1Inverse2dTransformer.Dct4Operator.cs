// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the four-point discrete cosine inverse transform operator.
/// </content>
internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Defines the four-point AV1 inverse discrete cosine transform operator.
    /// </summary>
    /// <remarks>
    /// Vector fields represent transform positions and vector lanes represent independent axes. The SIMD overloads apply
    /// the same staged butterflies, fixed-point rounding, and range clamps as the scalar overload without mixing axes.
    /// </remarks>
    internal readonly struct Dct4Operator : IAv1InverseTransform1dOperator
    {
        /// <summary>
        /// Applies the normative four-point AV1 inverse discrete cosine transform.
        /// </summary>
        /// <param name="input">The four frequency-domain coefficients.</param>
        /// <param name="output">The four spatial-domain residual values.</param>
        /// <param name="step">The four-element stage buffer owned by the containing two-dimensional transform.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
        {
            // AV1 stores coefficients in frequency order; this permutation restores the order expected by the staged DCT.
            output[0] = input[0];
            output[1] = input[2];
            output[2] = input[1];
            output[3] = input[3];

            // Rotate the even and odd coefficient pairs using the same fixed-point basis as the forward transform.
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
            step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
            step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
            step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);

            // The terminal butterflies reconstruct spatial order and clamp every result to the normative stage range.
            byte range = stageRange[3];
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
            output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
            output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            Av1TransformStageRange stageRange)
        {
            // AV1 stores coefficients in frequency order; this permutation restores the order expected by the staged DCT.
            output.V0 = input.V0;
            output.V1 = input.V2;
            output.V2 = input.V1;
            output.V3 = input.V3;

            // Rotate the even and odd coefficient pairs using the same fixed-point basis as the forward transform.
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            step.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);

            // The terminal butterflies reconstruct spatial order and clamp every result to the normative stage range.
            byte range = stageRange[3];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, range);
        }

        /// <summary>
        /// Applies the transform to four independent axes in parallel.
        /// </summary>
        /// <param name="input">The source values for the parallel transform axes.</param>
        /// <param name="output">The destination values for the parallel transform axes.</param>
        /// <param name="step">The fixed stage storage for the parallel transform axes.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            Av1TransformStageRange stageRange)
        {
            // AV1 stores coefficients in frequency order; this permutation restores the order expected by the staged DCT.
            output.V0 = input.V0;
            output.V1 = input.V2;
            output.V2 = input.V1;
            output.V3 = input.V3;

            // Rotate the even and odd coefficient pairs using the same fixed-point basis as the forward transform.
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            step.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);

            // The terminal butterflies reconstruct spatial order and clamp every result to the normative stage range.
            byte range = stageRange[3];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, range);
        }
    }
}
