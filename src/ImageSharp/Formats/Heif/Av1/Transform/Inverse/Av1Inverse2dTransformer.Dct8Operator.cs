// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the eight-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. The SIMD overloads apply
/// the same staged butterflies, fixed-point rounding, and range clamps as the scalar overload without mixing axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct8Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 8;

        /// <summary>
        /// Applies the normative eight-point AV1 inverse discrete cosine transform.
        /// </summary>
        /// <param name="input">The eight frequency-domain coefficients.</param>
        /// <param name="output">The eight spatial-domain residual values.</param>
        /// <param name="step">The eight-element stage buffer owned by the containing two-dimensional transform.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output[0] = input[0];
            output[1] = input[4];
            output[2] = input[2];
            output[3] = input[6];
            output[4] = input[1];
            output[5] = input[5];
            output[6] = input[3];
            output[7] = input[7];

            // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
            stage++;
            step[0] = output[0];
            step[1] = output[1];
            step[2] = output[2];
            step[3] = output[3];
            step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
            step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
            step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
            step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], output[7], cosBit);

            // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
            output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
            output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
            output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
            output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
            output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
            output[6] = Av1Transform1dMath.Clamp(step[7] - step[6], range);
            output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);

            // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
            stage++;
            step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
            step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
            step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
            step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
            step[4] = output[4];
            step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
            step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
            step[7] = output[7];

            // Stage 5 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[7], range);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[6], range);
            output[2] = Av1Transform1dMath.Clamp(step[2] + step[5], range);
            output[3] = Av1Transform1dMath.Clamp(step[3] + step[4], range);
            output[4] = Av1Transform1dMath.Clamp(step[3] - step[4], range);
            output[5] = Av1Transform1dMath.Clamp(step[2] - step[5], range);
            output[6] = Av1Transform1dMath.Clamp(step[1] - step[6], range);
            output[7] = Av1Transform1dMath.Clamp(step[0] - step[7], range);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output.V0 = input.V0;
            output.V1 = input.V4;
            output.V2 = input.V2;
            output.V3 = input.V6;
            output.V4 = input.V1;
            output.V5 = input.V5;
            output.V6 = input.V3;
            output.V7 = input.V7;

            // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
            stage++;
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V4, cospi[56], output.V7, cosBit);

            // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V7 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, range);

            // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
            stage++;
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, range);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;

            // Stage 5 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, range);
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
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output.V0 = input.V0;
            output.V1 = input.V4;
            output.V2 = input.V2;
            output.V3 = input.V6;
            output.V4 = input.V1;
            output.V5 = input.V5;
            output.V6 = input.V3;
            output.V7 = input.V7;

            // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
            stage++;
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V4, cospi[56], output.V7, cosBit);

            // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V7 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, range);

            // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
            stage++;
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, range);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;

            // Stage 5 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, range);
        }
    }
}
