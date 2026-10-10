// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 32-point inverse DCT with at most 8 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct32Low8Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 8;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
            => TransformLanes<int, ScalarLaneOperator>(ref AsPositions(input), ref AsPositions(output), ref AsPositions(step), cosBit, stageRange);

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
            => TransformLanes<Vector128<int>, Vector128LaneOperator>(ref input, ref output, ref step, cosBit, stageRange);

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
            => TransformLanes<Vector256<int>, Vector256LaneOperator>(ref input, ref output, ref step, cosBit, stageRange);

        /// <summary>
        /// Applies the 32-point inverse DCT with eight nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the first eight positions are read.</param>
        /// <param name="output">The thirty-two spatial-domain residual values.</param>
        /// <param name="step">The thirty-two-position stage buffer. It can alias <paramref name="input"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TransformLanes<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> input,
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order. The zero inputs keep no position.
            output.V0 = input.V0;
            output.V4 = input.V4;
            output.V8 = input.V2;
            output.V12 = input.V6;
            output.V16 = input.V1;
            output.V20 = input.V5;
            output.V24 = input.V3;
            output.V28 = input.V7;

            // Stage 2 rotates the odd-frequency pairs by odd multiples of pi/64. One input of each rotation is zero, so each output is one rounded product.
            step.V0 = output.V0;
            step.V4 = output.V4;
            step.V8 = output.V8;
            step.V12 = output.V12;
            step.V16 = TLaneOperator.MultiplyRound(output.V16, cospi[62], cosBit);
            step.V19 = TLaneOperator.MultiplyRound(output.V28, -cospi[50], cosBit);
            step.V20 = TLaneOperator.MultiplyRound(output.V20, cospi[54], cosBit);
            step.V23 = TLaneOperator.MultiplyRound(output.V24, -cospi[58], cosBit);
            step.V24 = TLaneOperator.MultiplyRound(output.V24, cospi[6], cosBit);
            step.V27 = TLaneOperator.MultiplyRound(output.V20, cospi[10], cosBit);
            step.V28 = TLaneOperator.MultiplyRound(output.V28, cospi[14], cosBit);
            step.V31 = TLaneOperator.MultiplyRound(output.V16, cospi[2], cosBit);

            // Stage 3 reconstructs the first nested groups and combines adjacent odd terms. One term of each pair is zero, so each result is a clamped copy.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V4 = step.V4;
            output.V8 = TLaneOperator.MultiplyRound(step.V8, cospi[60], cosBit);
            output.V11 = TLaneOperator.MultiplyRound(step.V12, -cospi[52], cosBit);
            output.V12 = TLaneOperator.MultiplyRound(step.V12, cospi[12], cosBit);
            output.V15 = TLaneOperator.MultiplyRound(step.V8, cospi[4], cosBit);
            output.V16 = TLaneOperator.Clamp(step.V16, range);
            output.V17 = TLaneOperator.Clamp(step.V16, range);
            output.V18 = TLaneOperator.Clamp(step.V19, range);
            output.V19 = TLaneOperator.Clamp(step.V19, range);
            output.V20 = TLaneOperator.Clamp(step.V20, range);
            output.V21 = TLaneOperator.Clamp(step.V20, range);
            output.V22 = TLaneOperator.Clamp(step.V23, range);
            output.V23 = TLaneOperator.Clamp(step.V23, range);
            output.V24 = TLaneOperator.Clamp(step.V24, range);
            output.V25 = TLaneOperator.Clamp(step.V24, range);
            output.V26 = TLaneOperator.Clamp(step.V27, range);
            output.V27 = TLaneOperator.Clamp(step.V27, range);
            output.V28 = TLaneOperator.Clamp(step.V28, range);
            output.V29 = TLaneOperator.Clamp(step.V28, range);
            output.V30 = TLaneOperator.Clamp(step.V31, range);
            output.V31 = TLaneOperator.Clamp(step.V31, range);

            // Stage 4 rotates the next odd-frequency level. Positions 0 to 3 pass through unchanged.
            range = stageRange[4];
            step.V0 = output.V0;
            step.V4 = TLaneOperator.MultiplyRound(output.V4, cospi[56], cosBit);
            step.V7 = TLaneOperator.MultiplyRound(output.V4, cospi[8], cosBit);
            step.V8 = TLaneOperator.Clamp(output.V8, range);
            step.V9 = TLaneOperator.Clamp(output.V8, range);
            step.V10 = TLaneOperator.Clamp(output.V11, range);
            step.V11 = TLaneOperator.Clamp(output.V11, range);
            step.V12 = TLaneOperator.Clamp(output.V12, range);
            step.V13 = TLaneOperator.Clamp(output.V12, range);
            step.V14 = TLaneOperator.Clamp(output.V15, range);
            step.V15 = TLaneOperator.Clamp(output.V15, range);
            step.V16 = output.V16;
            step.V17 = TLaneOperator.HalfButterfly(-cospi[8], output.V17, cospi[56], output.V30, cosBit);
            step.V18 = TLaneOperator.HalfButterfly(-cospi[56], output.V18, -cospi[8], output.V29, cosBit);
            step.V19 = output.V19;
            step.V20 = output.V20;
            step.V21 = TLaneOperator.HalfButterfly(-cospi[40], output.V21, cospi[24], output.V26, cosBit);
            step.V22 = TLaneOperator.HalfButterfly(-cospi[24], output.V22, -cospi[40], output.V25, cosBit);
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = TLaneOperator.HalfButterfly(-cospi[40], output.V22, cospi[24], output.V25, cosBit);
            step.V26 = TLaneOperator.HalfButterfly(cospi[24], output.V21, cospi[40], output.V26, cosBit);
            step.V27 = output.V27;
            step.V28 = output.V28;
            step.V29 = TLaneOperator.HalfButterfly(-cospi[8], output.V18, cospi[56], output.V29, cosBit);
            step.V30 = TLaneOperator.HalfButterfly(cospi[56], output.V17, cospi[8], output.V30, cosBit);
            step.V31 = output.V31;

            // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            range = stageRange[5];
            output.V0 = TLaneOperator.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V1 = TLaneOperator.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V4 = TLaneOperator.Clamp(step.V4, range);
            output.V5 = TLaneOperator.Clamp(step.V4, range);
            output.V6 = TLaneOperator.Clamp(step.V7, range);
            output.V7 = TLaneOperator.Clamp(step.V7, range);
            output.V8 = step.V8;
            output.V9 = TLaneOperator.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = TLaneOperator.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = TLaneOperator.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = TLaneOperator.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V19, range);
            output.V17 = TLaneOperator.AddClamp(step.V17, step.V18, range);
            output.V18 = TLaneOperator.SubtractClamp(step.V17, step.V18, range);
            output.V19 = TLaneOperator.SubtractClamp(step.V16, step.V19, range);
            output.V20 = TLaneOperator.SubtractClamp(step.V23, step.V20, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V22, step.V21, range);
            output.V22 = TLaneOperator.AddClamp(step.V21, step.V22, range);
            output.V23 = TLaneOperator.AddClamp(step.V20, step.V23, range);
            output.V24 = TLaneOperator.AddClamp(step.V24, step.V27, range);
            output.V25 = TLaneOperator.AddClamp(step.V25, step.V26, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V25, step.V26, range);
            output.V27 = TLaneOperator.SubtractClamp(step.V24, step.V27, range);
            output.V28 = TLaneOperator.SubtractClamp(step.V31, step.V28, range);
            output.V29 = TLaneOperator.SubtractClamp(step.V30, step.V29, range);
            output.V30 = TLaneOperator.AddClamp(step.V29, step.V30, range);
            output.V31 = TLaneOperator.AddClamp(step.V28, step.V31, range);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs. Positions 2 and 3 of stage 5 are zero.
            range = stageRange[6];
            step.V0 = TLaneOperator.Clamp(output.V0, range);
            step.V1 = TLaneOperator.Clamp(output.V1, range);
            step.V2 = TLaneOperator.Clamp(output.V1, range);
            step.V3 = TLaneOperator.Clamp(output.V0, range);
            step.V4 = output.V4;
            step.V5 = TLaneOperator.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = TLaneOperator.AddClamp(output.V8, output.V11, range);
            step.V9 = TLaneOperator.AddClamp(output.V9, output.V10, range);
            step.V10 = TLaneOperator.SubtractClamp(output.V9, output.V10, range);
            step.V11 = TLaneOperator.SubtractClamp(output.V8, output.V11, range);
            step.V12 = TLaneOperator.SubtractClamp(output.V15, output.V12, range);
            step.V13 = TLaneOperator.SubtractClamp(output.V14, output.V13, range);
            step.V14 = TLaneOperator.AddClamp(output.V13, output.V14, range);
            step.V15 = TLaneOperator.AddClamp(output.V12, output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = TLaneOperator.HalfButterfly(-cospi[16], output.V18, cospi[48], output.V29, cosBit);
            step.V19 = TLaneOperator.HalfButterfly(-cospi[16], output.V19, cospi[48], output.V28, cosBit);
            step.V20 = TLaneOperator.HalfButterfly(-cospi[48], output.V20, -cospi[16], output.V27, cosBit);
            step.V21 = TLaneOperator.HalfButterfly(-cospi[48], output.V21, -cospi[16], output.V26, cosBit);
            step.V22 = output.V22;
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = output.V25;
            step.V26 = TLaneOperator.HalfButterfly(-cospi[16], output.V21, cospi[48], output.V26, cosBit);
            step.V27 = TLaneOperator.HalfButterfly(-cospi[16], output.V20, cospi[48], output.V27, cosBit);
            step.V28 = TLaneOperator.HalfButterfly(cospi[48], output.V19, cospi[16], output.V28, cosBit);
            step.V29 = TLaneOperator.HalfButterfly(cospi[48], output.V18, cospi[16], output.V29, cosBit);
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Every stage 6 position is now set, so the remaining stages are those of the complete transform.
            Dct32Operator.Stages7To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
