// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 16-point inverse DCT with at most 8 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct16Low8Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 8;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output[0] = input[0];
            output[2] = input[4];
            output[4] = input[2];
            output[6] = input[6];
            output[8] = input[1];
            output[10] = input[5];
            output[12] = input[3];
            output[14] = input[7];

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            step[0] = output[0];
            step[2] = output[2];
            step[4] = output[4];
            step[6] = output[6];
            step[8] = Av1Math.RoundShift((long)output[8] * cospi[60], cosBit);
            step[9] = Av1Math.RoundShift((long)output[14] * -cospi[36], cosBit);
            step[10] = Av1Math.RoundShift((long)output[10] * cospi[44], cosBit);
            step[11] = Av1Math.RoundShift((long)output[12] * -cospi[52], cosBit);
            step[12] = Av1Math.RoundShift((long)output[12] * cospi[12], cosBit);
            step[13] = Av1Math.RoundShift((long)output[10] * cospi[20], cosBit);
            step[14] = Av1Math.RoundShift((long)output[14] * cospi[28], cosBit);
            step[15] = Av1Math.RoundShift((long)output[8] * cospi[4], cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            output[0] = step[0];
            output[2] = step[2];
            output[4] = Av1Math.RoundShift((long)step[4] * cospi[56], cosBit);
            output[5] = Av1Math.RoundShift((long)step[6] * -cospi[40], cosBit);
            output[6] = Av1Math.RoundShift((long)step[6] * cospi[24], cosBit);
            output[7] = Av1Math.RoundShift((long)step[4] * cospi[8], cosBit);
            output[8] = Av1Transform1dMath.Clamp(step[8] + step[9], stageRange[3]);
            output[9] = Av1Transform1dMath.Clamp(step[8] - step[9], stageRange[3]);
            output[10] = Av1Transform1dMath.Clamp(step[11] - step[10], stageRange[3]);
            output[11] = Av1Transform1dMath.Clamp(step[10] + step[11], stageRange[3]);
            output[12] = Av1Transform1dMath.Clamp(step[12] + step[13], stageRange[3]);
            output[13] = Av1Transform1dMath.Clamp(step[12] - step[13], stageRange[3]);
            output[14] = Av1Transform1dMath.Clamp(step[15] - step[14], stageRange[3]);
            output[15] = Av1Transform1dMath.Clamp(step[14] + step[15], stageRange[3]);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step[0] = Av1Math.RoundShift((long)output[0] * cospi[32], cosBit);
            step[1] = Av1Math.RoundShift((long)output[0] * cospi[32], cosBit);
            step[2] = Av1Math.RoundShift((long)output[2] * cospi[48], cosBit);
            step[3] = Av1Math.RoundShift((long)output[2] * cospi[16], cosBit);
            step[4] = Av1Transform1dMath.Clamp(output[4] + output[5], stageRange[4]);
            step[5] = Av1Transform1dMath.Clamp(output[4] - output[5], stageRange[4]);
            step[6] = Av1Transform1dMath.Clamp(output[7] - output[6], stageRange[4]);
            step[7] = Av1Transform1dMath.Clamp(output[6] + output[7], stageRange[4]);
            step[8] = output[8];
            step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
            step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
            step[11] = output[11];
            step[12] = output[12];
            step[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[10], cospi[48], output[13], cosBit);
            step[14] = Av1Transform1dMath.HalfButterfly(cospi[48], output[9], cospi[16], output[14], cosBit);
            step[15] = output[15];

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], stageRange[5]);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], stageRange[5]);
            output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], stageRange[5]);
            output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], stageRange[5]);
            output[4] = step[4];
            output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
            output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[5], cospi[32], step[6], cosBit);
            output[7] = step[7];
            output[8] = Av1Transform1dMath.Clamp(step[8] + step[11], stageRange[5]);
            output[9] = Av1Transform1dMath.Clamp(step[9] + step[10], stageRange[5]);
            output[10] = Av1Transform1dMath.Clamp(step[9] - step[10], stageRange[5]);
            output[11] = Av1Transform1dMath.Clamp(step[8] - step[11], stageRange[5]);
            output[12] = Av1Transform1dMath.Clamp(step[15] - step[12], stageRange[5]);
            output[13] = Av1Transform1dMath.Clamp(step[14] - step[13], stageRange[5]);
            output[14] = Av1Transform1dMath.Clamp(step[13] + step[14], stageRange[5]);
            output[15] = Av1Transform1dMath.Clamp(step[12] + step[15], stageRange[5]);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            step[0] = Av1Transform1dMath.Clamp(output[0] + output[7], stageRange[6]);
            step[1] = Av1Transform1dMath.Clamp(output[1] + output[6], stageRange[6]);
            step[2] = Av1Transform1dMath.Clamp(output[2] + output[5], stageRange[6]);
            step[3] = Av1Transform1dMath.Clamp(output[3] + output[4], stageRange[6]);
            step[4] = Av1Transform1dMath.Clamp(output[3] - output[4], stageRange[6]);
            step[5] = Av1Transform1dMath.Clamp(output[2] - output[5], stageRange[6]);
            step[6] = Av1Transform1dMath.Clamp(output[1] - output[6], stageRange[6]);
            step[7] = Av1Transform1dMath.Clamp(output[0] - output[7], stageRange[6]);
            step[8] = output[8];
            step[9] = output[9];
            step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
            step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
            step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[11], cospi[32], output[12], cosBit);
            step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[13], cosBit);
            step[14] = output[14];
            step[15] = output[15];

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[15], stageRange[7]);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[14], stageRange[7]);
            output[2] = Av1Transform1dMath.Clamp(step[2] + step[13], stageRange[7]);
            output[3] = Av1Transform1dMath.Clamp(step[3] + step[12], stageRange[7]);
            output[4] = Av1Transform1dMath.Clamp(step[4] + step[11], stageRange[7]);
            output[5] = Av1Transform1dMath.Clamp(step[5] + step[10], stageRange[7]);
            output[6] = Av1Transform1dMath.Clamp(step[6] + step[9], stageRange[7]);
            output[7] = Av1Transform1dMath.Clamp(step[7] + step[8], stageRange[7]);
            output[8] = Av1Transform1dMath.Clamp(step[7] - step[8], stageRange[7]);
            output[9] = Av1Transform1dMath.Clamp(step[6] - step[9], stageRange[7]);
            output[10] = Av1Transform1dMath.Clamp(step[5] - step[10], stageRange[7]);
            output[11] = Av1Transform1dMath.Clamp(step[4] - step[11], stageRange[7]);
            output[12] = Av1Transform1dMath.Clamp(step[3] - step[12], stageRange[7]);
            output[13] = Av1Transform1dMath.Clamp(step[2] - step[13], stageRange[7]);
            output[14] = Av1Transform1dMath.Clamp(step[1] - step[14], stageRange[7]);
            output[15] = Av1Transform1dMath.Clamp(step[0] - step[15], stageRange[7]);
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

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output.V0 = input.V0;
            output.V2 = input.V4;
            output.V4 = input.V2;
            output.V6 = input.V6;
            output.V8 = input.V1;
            output.V10 = input.V5;
            output.V12 = input.V3;
            output.V14 = input.V7;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = Av1Transform1dMath.MultiplyRound(output.V8, cospi[60], cosBit);
            step.V9 = Av1Transform1dMath.MultiplyRound(output.V14, -cospi[36], cosBit);
            step.V10 = Av1Transform1dMath.MultiplyRound(output.V10, cospi[44], cosBit);
            step.V11 = Av1Transform1dMath.MultiplyRound(output.V12, -cospi[52], cosBit);
            step.V12 = Av1Transform1dMath.MultiplyRound(output.V12, cospi[12], cosBit);
            step.V13 = Av1Transform1dMath.MultiplyRound(output.V10, cospi[20], cosBit);
            step.V14 = Av1Transform1dMath.MultiplyRound(output.V14, cospi[28], cosBit);
            step.V15 = Av1Transform1dMath.MultiplyRound(output.V8, cospi[4], cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = Av1Transform1dMath.MultiplyRound(step.V4, cospi[56], cosBit);
            output.V5 = Av1Transform1dMath.MultiplyRound(step.V6, -cospi[40], cosBit);
            output.V6 = Av1Transform1dMath.MultiplyRound(step.V6, cospi[24], cosBit);
            output.V7 = Av1Transform1dMath.MultiplyRound(step.V4, cospi[8], cosBit);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V9, stageRange[3]);
            output.V9 = Av1Transform1dMath.Clamp(step.V8 - step.V9, stageRange[3]);
            output.V10 = Av1Transform1dMath.Clamp(step.V11 - step.V10, stageRange[3]);
            output.V11 = Av1Transform1dMath.Clamp(step.V10 + step.V11, stageRange[3]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V13, stageRange[3]);
            output.V13 = Av1Transform1dMath.Clamp(step.V12 - step.V13, stageRange[3]);
            output.V14 = Av1Transform1dMath.Clamp(step.V15 - step.V14, stageRange[3]);
            output.V15 = Av1Transform1dMath.Clamp(step.V14 + step.V15, stageRange[3]);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V2 = Av1Transform1dMath.MultiplyRound(output.V2, cospi[48], cosBit);
            step.V3 = Av1Transform1dMath.MultiplyRound(output.V2, cospi[16], cosBit);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V5, stageRange[4]);
            step.V5 = Av1Transform1dMath.Clamp(output.V4 - output.V5, stageRange[4]);
            step.V6 = Av1Transform1dMath.Clamp(output.V7 - output.V6, stageRange[4]);
            step.V7 = Av1Transform1dMath.Clamp(output.V6 + output.V7, stageRange[4]);
            step.V8 = output.V8;
            step.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V10, -cospi[16], output.V13, cosBit);
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V10, cospi[48], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, stageRange[5]);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, stageRange[5]);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, stageRange[5]);
            output.V4 = step.V4;
            output.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V11, stageRange[5]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V10, stageRange[5]);
            output.V10 = Av1Transform1dMath.Clamp(step.V9 - step.V10, stageRange[5]);
            output.V11 = Av1Transform1dMath.Clamp(step.V8 - step.V11, stageRange[5]);
            output.V12 = Av1Transform1dMath.Clamp(step.V15 - step.V12, stageRange[5]);
            output.V13 = Av1Transform1dMath.Clamp(step.V14 - step.V13, stageRange[5]);
            output.V14 = Av1Transform1dMath.Clamp(step.V13 + step.V14, stageRange[5]);
            output.V15 = Av1Transform1dMath.Clamp(step.V12 + step.V15, stageRange[5]);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V7, stageRange[6]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V6, stageRange[6]);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V5, stageRange[6]);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V4, stageRange[6]);
            step.V4 = Av1Transform1dMath.Clamp(output.V3 - output.V4, stageRange[6]);
            step.V5 = Av1Transform1dMath.Clamp(output.V2 - output.V5, stageRange[6]);
            step.V6 = Av1Transform1dMath.Clamp(output.V1 - output.V6, stageRange[6]);
            step.V7 = Av1Transform1dMath.Clamp(output.V0 - output.V7, stageRange[6]);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V15, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V14, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V13, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V12, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V11, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V10, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V9, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V8, stageRange[7]);
            output.V8 = Av1Transform1dMath.Clamp(step.V7 - step.V8, stageRange[7]);
            output.V9 = Av1Transform1dMath.Clamp(step.V6 - step.V9, stageRange[7]);
            output.V10 = Av1Transform1dMath.Clamp(step.V5 - step.V10, stageRange[7]);
            output.V11 = Av1Transform1dMath.Clamp(step.V4 - step.V11, stageRange[7]);
            output.V12 = Av1Transform1dMath.Clamp(step.V3 - step.V12, stageRange[7]);
            output.V13 = Av1Transform1dMath.Clamp(step.V2 - step.V13, stageRange[7]);
            output.V14 = Av1Transform1dMath.Clamp(step.V1 - step.V14, stageRange[7]);
            output.V15 = Av1Transform1dMath.Clamp(step.V0 - step.V15, stageRange[7]);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output.V0 = input.V0;
            output.V2 = input.V4;
            output.V4 = input.V2;
            output.V6 = input.V6;
            output.V8 = input.V1;
            output.V10 = input.V5;
            output.V12 = input.V3;
            output.V14 = input.V7;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = Av1Transform1dMath.MultiplyRound(output.V8, cospi[60], cosBit);
            step.V9 = Av1Transform1dMath.MultiplyRound(output.V14, -cospi[36], cosBit);
            step.V10 = Av1Transform1dMath.MultiplyRound(output.V10, cospi[44], cosBit);
            step.V11 = Av1Transform1dMath.MultiplyRound(output.V12, -cospi[52], cosBit);
            step.V12 = Av1Transform1dMath.MultiplyRound(output.V12, cospi[12], cosBit);
            step.V13 = Av1Transform1dMath.MultiplyRound(output.V10, cospi[20], cosBit);
            step.V14 = Av1Transform1dMath.MultiplyRound(output.V14, cospi[28], cosBit);
            step.V15 = Av1Transform1dMath.MultiplyRound(output.V8, cospi[4], cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = Av1Transform1dMath.MultiplyRound(step.V4, cospi[56], cosBit);
            output.V5 = Av1Transform1dMath.MultiplyRound(step.V6, -cospi[40], cosBit);
            output.V6 = Av1Transform1dMath.MultiplyRound(step.V6, cospi[24], cosBit);
            output.V7 = Av1Transform1dMath.MultiplyRound(step.V4, cospi[8], cosBit);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V9, stageRange[3]);
            output.V9 = Av1Transform1dMath.Clamp(step.V8 - step.V9, stageRange[3]);
            output.V10 = Av1Transform1dMath.Clamp(step.V11 - step.V10, stageRange[3]);
            output.V11 = Av1Transform1dMath.Clamp(step.V10 + step.V11, stageRange[3]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V13, stageRange[3]);
            output.V13 = Av1Transform1dMath.Clamp(step.V12 - step.V13, stageRange[3]);
            output.V14 = Av1Transform1dMath.Clamp(step.V15 - step.V14, stageRange[3]);
            output.V15 = Av1Transform1dMath.Clamp(step.V14 + step.V15, stageRange[3]);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V2 = Av1Transform1dMath.MultiplyRound(output.V2, cospi[48], cosBit);
            step.V3 = Av1Transform1dMath.MultiplyRound(output.V2, cospi[16], cosBit);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V5, stageRange[4]);
            step.V5 = Av1Transform1dMath.Clamp(output.V4 - output.V5, stageRange[4]);
            step.V6 = Av1Transform1dMath.Clamp(output.V7 - output.V6, stageRange[4]);
            step.V7 = Av1Transform1dMath.Clamp(output.V6 + output.V7, stageRange[4]);
            step.V8 = output.V8;
            step.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V10, -cospi[16], output.V13, cosBit);
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V10, cospi[48], output.V13, cosBit);
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;

            // Stage 5 widens the reconstructed groups through their next butterfly level.
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V3, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V2, stageRange[5]);
            output.V2 = Av1Transform1dMath.Clamp(step.V1 - step.V2, stageRange[5]);
            output.V3 = Av1Transform1dMath.Clamp(step.V0 - step.V3, stageRange[5]);
            output.V4 = step.V4;
            output.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V11, stageRange[5]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V10, stageRange[5]);
            output.V10 = Av1Transform1dMath.Clamp(step.V9 - step.V10, stageRange[5]);
            output.V11 = Av1Transform1dMath.Clamp(step.V8 - step.V11, stageRange[5]);
            output.V12 = Av1Transform1dMath.Clamp(step.V15 - step.V12, stageRange[5]);
            output.V13 = Av1Transform1dMath.Clamp(step.V14 - step.V13, stageRange[5]);
            output.V14 = Av1Transform1dMath.Clamp(step.V13 + step.V14, stageRange[5]);
            output.V15 = Av1Transform1dMath.Clamp(step.V12 + step.V15, stageRange[5]);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V7, stageRange[6]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V6, stageRange[6]);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V5, stageRange[6]);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V4, stageRange[6]);
            step.V4 = Av1Transform1dMath.Clamp(output.V3 - output.V4, stageRange[6]);
            step.V5 = Av1Transform1dMath.Clamp(output.V2 - output.V5, stageRange[6]);
            step.V6 = Av1Transform1dMath.Clamp(output.V1 - output.V6, stageRange[6]);
            step.V7 = Av1Transform1dMath.Clamp(output.V0 - output.V7, stageRange[6]);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V15, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V14, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V13, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V12, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V11, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V10, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V9, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V8, stageRange[7]);
            output.V8 = Av1Transform1dMath.Clamp(step.V7 - step.V8, stageRange[7]);
            output.V9 = Av1Transform1dMath.Clamp(step.V6 - step.V9, stageRange[7]);
            output.V10 = Av1Transform1dMath.Clamp(step.V5 - step.V10, stageRange[7]);
            output.V11 = Av1Transform1dMath.Clamp(step.V4 - step.V11, stageRange[7]);
            output.V12 = Av1Transform1dMath.Clamp(step.V3 - step.V12, stageRange[7]);
            output.V13 = Av1Transform1dMath.Clamp(step.V2 - step.V13, stageRange[7]);
            output.V14 = Av1Transform1dMath.Clamp(step.V1 - step.V14, stageRange[7]);
            output.V15 = Av1Transform1dMath.Clamp(step.V0 - step.V15, stageRange[7]);
        }
    }
}
