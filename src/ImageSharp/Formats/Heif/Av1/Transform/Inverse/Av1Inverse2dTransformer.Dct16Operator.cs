// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 16-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. The SIMD overloads apply
/// the same staged butterflies, fixed-point rounding, and range clamps as the scalar overload without mixing axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct16Operator : IAv1Transform1dOperator
    {
        /// <summary>
        /// Applies the normative 16-point AV1 inverse discrete cosine transform.
        /// </summary>
        /// <param name="input">The sixteen frequency-domain coefficients.</param>
        /// <param name="output">The sixteen spatial-domain residual values.</param>
        /// <param name="step">The sixteen-element stage buffer owned by the containing two-dimensional transform.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output[0] = input[0];
            output[1] = input[8];
            output[2] = input[4];
            output[3] = input[12];
            output[4] = input[2];
            output[5] = input[10];
            output[6] = input[6];
            output[7] = input[14];
            output[8] = input[1];
            output[9] = input[9];
            output[10] = input[5];
            output[11] = input[13];
            output[12] = input[3];
            output[13] = input[11];
            output[14] = input[7];
            output[15] = input[15];

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            stage++;
            step[0] = output[0];
            step[1] = output[1];
            step[2] = output[2];
            step[3] = output[3];
            step[4] = output[4];
            step[5] = output[5];
            step[6] = output[6];
            step[7] = output[7];
            step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], -cospi[4], output[15], cosBit);
            step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], -cospi[36], output[14], cosBit);
            step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], -cospi[20], output[13], cosBit);
            step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], -cospi[52], output[12], cosBit);
            step[12] = Av1Transform1dMath.HalfButterfly(cospi[52], output[11], cospi[12], output[12], cosBit);
            step[13] = Av1Transform1dMath.HalfButterfly(cospi[20], output[10], cospi[44], output[13], cosBit);
            step[14] = Av1Transform1dMath.HalfButterfly(cospi[36], output[9], cospi[28], output[14], cosBit);
            step[15] = Av1Transform1dMath.HalfButterfly(cospi[4], output[8], cospi[60], output[15], cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output[0] = step[0];
            output[1] = step[1];
            output[2] = step[2];
            output[3] = step[3];
            output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], -cospi[8], step[7], cosBit);
            output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], -cospi[40], step[6], cosBit);
            output[6] = Av1Transform1dMath.HalfButterfly(cospi[40], step[5], cospi[24], step[6], cosBit);
            output[7] = Av1Transform1dMath.HalfButterfly(cospi[8], step[4], cospi[56], step[7], cosBit);
            output[8] = Av1Transform1dMath.Clamp(step[8] + step[9], range);
            output[9] = Av1Transform1dMath.Clamp(step[8] - step[9], range);
            output[10] = Av1Transform1dMath.Clamp(step[11] - step[10], range);
            output[11] = Av1Transform1dMath.Clamp(step[10] + step[11], range);
            output[12] = Av1Transform1dMath.Clamp(step[12] + step[13], range);
            output[13] = Av1Transform1dMath.Clamp(step[12] - step[13], range);
            output[14] = Av1Transform1dMath.Clamp(step[15] - step[14], range);
            output[15] = Av1Transform1dMath.Clamp(step[14] + step[15], range);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            stage++;
            range = stageRange[stage];
            step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
            step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
            step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
            step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);
            step[4] = Av1Transform1dMath.Clamp(output[4] + output[5], range);
            step[5] = Av1Transform1dMath.Clamp(output[4] - output[5], range);
            step[6] = Av1Transform1dMath.Clamp(output[7] - output[6], range);
            step[7] = Av1Transform1dMath.Clamp(output[6] + output[7], range);
            step[8] = output[8];
            step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
            step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
            step[11] = output[11];
            step[12] = output[12];
            step[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[10], cospi[48], output[13], cosBit);
            step[14] = Av1Transform1dMath.HalfButterfly(cospi[48], output[9], cospi[16], output[14], cosBit);
            step[15] = output[15];

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            stage++;
            range = stageRange[stage];
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
            output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
            output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
            output[4] = step[4];
            output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
            output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[5], cospi[32], step[6], cosBit);
            output[7] = step[7];
            output[8] = Av1Transform1dMath.Clamp(step[8] + step[11], range);
            output[9] = Av1Transform1dMath.Clamp(step[9] + step[10], range);
            output[10] = Av1Transform1dMath.Clamp(step[9] - step[10], range);
            output[11] = Av1Transform1dMath.Clamp(step[8] - step[11], range);
            output[12] = Av1Transform1dMath.Clamp(step[15] - step[12], range);
            output[13] = Av1Transform1dMath.Clamp(step[14] - step[13], range);
            output[14] = Av1Transform1dMath.Clamp(step[13] + step[14], range);
            output[15] = Av1Transform1dMath.Clamp(step[12] + step[15], range);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            stage++;
            range = stageRange[stage];
            step[0] = Av1Transform1dMath.Clamp(output[0] + output[7], range);
            step[1] = Av1Transform1dMath.Clamp(output[1] + output[6], range);
            step[2] = Av1Transform1dMath.Clamp(output[2] + output[5], range);
            step[3] = Av1Transform1dMath.Clamp(output[3] + output[4], range);
            step[4] = Av1Transform1dMath.Clamp(output[3] - output[4], range);
            step[5] = Av1Transform1dMath.Clamp(output[2] - output[5], range);
            step[6] = Av1Transform1dMath.Clamp(output[1] - output[6], range);
            step[7] = Av1Transform1dMath.Clamp(output[0] - output[7], range);
            step[8] = output[8];
            step[9] = output[9];
            step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
            step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
            step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[11], cospi[32], output[12], cosBit);
            step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[13], cosBit);
            step[14] = output[14];
            step[15] = output[15];

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[15], range);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[14], range);
            output[2] = Av1Transform1dMath.Clamp(step[2] + step[13], range);
            output[3] = Av1Transform1dMath.Clamp(step[3] + step[12], range);
            output[4] = Av1Transform1dMath.Clamp(step[4] + step[11], range);
            output[5] = Av1Transform1dMath.Clamp(step[5] + step[10], range);
            output[6] = Av1Transform1dMath.Clamp(step[6] + step[9], range);
            output[7] = Av1Transform1dMath.Clamp(step[7] + step[8], range);
            output[8] = Av1Transform1dMath.Clamp(step[7] - step[8], range);
            output[9] = Av1Transform1dMath.Clamp(step[6] - step[9], range);
            output[10] = Av1Transform1dMath.Clamp(step[5] - step[10], range);
            output[11] = Av1Transform1dMath.Clamp(step[4] - step[11], range);
            output[12] = Av1Transform1dMath.Clamp(step[3] - step[12], range);
            output[13] = Av1Transform1dMath.Clamp(step[2] - step[13], range);
            output[14] = Av1Transform1dMath.Clamp(step[1] - step[14], range);
            output[15] = Av1Transform1dMath.Clamp(step[0] - step[15], range);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            Av1TransformStageRange stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output.V0 = input.V0;
            output.V1 = input.V8;
            output.V2 = input.V4;
            output.V3 = input.V12;
            output.V4 = input.V2;
            output.V5 = input.V10;
            output.V6 = input.V6;
            output.V7 = input.V14;
            output.V8 = input.V1;
            output.V9 = input.V9;
            output.V10 = input.V5;
            output.V11 = input.V13;
            output.V12 = input.V3;
            output.V13 = input.V11;
            output.V14 = input.V7;
            output.V15 = input.V15;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            stage++;
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = output.V6;
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.HalfButterfly(cospi[60], output.V8, -cospi[4], output.V15, cosBit);
            step.V9 = Av1Transform1dMath.HalfButterfly(cospi[28], output.V9, -cospi[36], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(cospi[44], output.V10, -cospi[20], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(cospi[12], output.V11, -cospi[52], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[52], output.V11, cospi[12], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[20], output.V10, cospi[44], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[36], output.V9, cospi[28], output.V14, cosBit);
            step.V15 = Av1Transform1dMath.HalfButterfly(cospi[4], output.V8, cospi[60], output.V15, cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], step.V4, -cospi[8], step.V7, cosBit);
            output.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], step.V5, -cospi[40], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], step.V5, cospi[24], step.V6, cosBit);
            output.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], step.V4, cospi[56], step.V7, cosBit);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V9, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V8 - step.V9, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V11 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V10 + step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V13, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V12 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V15 - step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V14 + step.V15, range);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V5, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V4 - output.V5, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V7 - output.V6, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V6 + output.V7, range);
            step.V8 = output.V8;
            step.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V10, -cospi[16], output.V13, cosBit);
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V10, cospi[48], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, range);
            output.V4 = step.V4;
            output.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V11, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V10, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V9 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V8 - step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V15 - step.V12, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V14 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V13 + step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V12 + step.V15, range);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V7, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V6, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V5, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V4, range);
            step.V4 = Av1Transform1dMath.Clamp(output.V3 - output.V4, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V2 - output.V5, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V1 - output.V6, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V0 - output.V7, range);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V15, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V14, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V13, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V12, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V11, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V10, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V9, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V8, range);
            output.V8 = Av1Transform1dMath.Clamp(step.V7 - step.V8, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V6 - step.V9, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V5 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V4 - step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V3 - step.V12, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V2 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V1 - step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V0 - step.V15, range);
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
            output.V0 = input.V0;
            output.V1 = input.V8;
            output.V2 = input.V4;
            output.V3 = input.V12;
            output.V4 = input.V2;
            output.V5 = input.V10;
            output.V6 = input.V6;
            output.V7 = input.V14;
            output.V8 = input.V1;
            output.V9 = input.V9;
            output.V10 = input.V5;
            output.V11 = input.V13;
            output.V12 = input.V3;
            output.V13 = input.V11;
            output.V14 = input.V7;
            output.V15 = input.V15;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            stage++;
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = output.V6;
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.HalfButterfly(cospi[60], output.V8, -cospi[4], output.V15, cosBit);
            step.V9 = Av1Transform1dMath.HalfButterfly(cospi[28], output.V9, -cospi[36], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(cospi[44], output.V10, -cospi[20], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(cospi[12], output.V11, -cospi[52], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[52], output.V11, cospi[12], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[20], output.V10, cospi[44], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[36], output.V9, cospi[28], output.V14, cosBit);
            step.V15 = Av1Transform1dMath.HalfButterfly(cospi[4], output.V8, cospi[60], output.V15, cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], step.V4, -cospi[8], step.V7, cosBit);
            output.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], step.V5, -cospi[40], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], step.V5, cospi[24], step.V6, cosBit);
            output.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], step.V4, cospi[56], step.V7, cosBit);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V9, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V8 - step.V9, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V11 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V10 + step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V13, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V12 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V15 - step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V14 + step.V15, range);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V5, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V4 - output.V5, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V7 - output.V6, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V6 + output.V7, range);
            step.V8 = output.V8;
            step.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V10, -cospi[16], output.V13, cosBit);
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V10, cospi[48], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, range);
            output.V4 = step.V4;
            output.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V11, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V10, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V9 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V8 - step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V15 - step.V12, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V14 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V13 + step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V12 + step.V15, range);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V7, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V6, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V5, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V4, range);
            step.V4 = Av1Transform1dMath.Clamp(output.V3 - output.V4, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V2 - output.V5, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V1 - output.V6, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V0 - output.V7, range);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V15, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V14, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V13, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V12, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V11, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V10, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V9, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V8, range);
            output.V8 = Av1Transform1dMath.Clamp(step.V7 - step.V8, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V6 - step.V9, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V5 - step.V10, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V4 - step.V11, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V3 - step.V12, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V2 - step.V13, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V1 - step.V14, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V0 - step.V15, range);
        }
    }
}
