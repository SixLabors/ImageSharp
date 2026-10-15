// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 32-point inverse DCT with at most 16 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct32Low16Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 16;

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
        /// Applies the 32-point inverse DCT with sixteen nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the first sixteen positions are read.</param>
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
            output.V2 = input.V8;
            output.V4 = input.V4;
            output.V6 = input.V12;
            output.V8 = input.V2;
            output.V10 = input.V10;
            output.V12 = input.V6;
            output.V14 = input.V14;
            output.V16 = input.V1;
            output.V18 = input.V9;
            output.V20 = input.V5;
            output.V22 = input.V13;
            output.V24 = input.V3;
            output.V26 = input.V11;
            output.V28 = input.V7;
            output.V30 = input.V15;

            // Stage 2 rotates the odd-frequency pairs by odd multiples of pi/64. One input of each rotation is zero, so each output is one rounded product.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = output.V8;
            step.V10 = output.V10;
            step.V12 = output.V12;
            step.V14 = output.V14;
            step.V16 = TLaneOperator.MultiplyRound(output.V16, cospi[62], cosBit);
            step.V17 = TLaneOperator.MultiplyRound(output.V30, -cospi[34], cosBit);
            step.V18 = TLaneOperator.MultiplyRound(output.V18, cospi[46], cosBit);
            step.V19 = TLaneOperator.MultiplyRound(output.V28, -cospi[50], cosBit);
            step.V20 = TLaneOperator.MultiplyRound(output.V20, cospi[54], cosBit);
            step.V21 = TLaneOperator.MultiplyRound(output.V26, -cospi[42], cosBit);
            step.V22 = TLaneOperator.MultiplyRound(output.V22, cospi[38], cosBit);
            step.V23 = TLaneOperator.MultiplyRound(output.V24, -cospi[58], cosBit);
            step.V24 = TLaneOperator.MultiplyRound(output.V24, cospi[6], cosBit);
            step.V25 = TLaneOperator.MultiplyRound(output.V22, cospi[26], cosBit);
            step.V26 = TLaneOperator.MultiplyRound(output.V26, cospi[22], cosBit);
            step.V27 = TLaneOperator.MultiplyRound(output.V20, cospi[10], cosBit);
            step.V28 = TLaneOperator.MultiplyRound(output.V28, cospi[14], cosBit);
            step.V29 = TLaneOperator.MultiplyRound(output.V18, cospi[18], cosBit);
            step.V30 = TLaneOperator.MultiplyRound(output.V30, cospi[30], cosBit);
            step.V31 = TLaneOperator.MultiplyRound(output.V16, cospi[2], cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = step.V4;
            output.V6 = step.V6;
            output.V8 = TLaneOperator.MultiplyRound(step.V8, cospi[60], cosBit);
            output.V9 = TLaneOperator.MultiplyRound(step.V14, -cospi[36], cosBit);
            output.V10 = TLaneOperator.MultiplyRound(step.V10, cospi[44], cosBit);
            output.V11 = TLaneOperator.MultiplyRound(step.V12, -cospi[52], cosBit);
            output.V12 = TLaneOperator.MultiplyRound(step.V12, cospi[12], cosBit);
            output.V13 = TLaneOperator.MultiplyRound(step.V10, cospi[20], cosBit);
            output.V14 = TLaneOperator.MultiplyRound(step.V14, cospi[28], cosBit);
            output.V15 = TLaneOperator.MultiplyRound(step.V8, cospi[4], cosBit);
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V17, range);
            output.V17 = TLaneOperator.SubtractClamp(step.V16, step.V17, range);
            output.V18 = TLaneOperator.SubtractClamp(step.V19, step.V18, range);
            output.V19 = TLaneOperator.AddClamp(step.V18, step.V19, range);
            output.V20 = TLaneOperator.AddClamp(step.V20, step.V21, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V20, step.V21, range);
            output.V22 = TLaneOperator.SubtractClamp(step.V23, step.V22, range);
            output.V23 = TLaneOperator.AddClamp(step.V22, step.V23, range);
            output.V24 = TLaneOperator.AddClamp(step.V24, step.V25, range);
            output.V25 = TLaneOperator.SubtractClamp(step.V24, step.V25, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V27, step.V26, range);
            output.V27 = TLaneOperator.AddClamp(step.V26, step.V27, range);
            output.V28 = TLaneOperator.AddClamp(step.V28, step.V29, range);
            output.V29 = TLaneOperator.SubtractClamp(step.V28, step.V29, range);
            output.V30 = TLaneOperator.SubtractClamp(step.V31, step.V30, range);
            output.V31 = TLaneOperator.AddClamp(step.V30, step.V31, range);

            // Stage 4 rotates the next odd-frequency level. Positions 0 to 3 pass through unchanged.
            range = stageRange[4];
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = TLaneOperator.MultiplyRound(output.V4, cospi[56], cosBit);
            step.V5 = TLaneOperator.MultiplyRound(output.V6, -cospi[40], cosBit);
            step.V6 = TLaneOperator.MultiplyRound(output.V6, cospi[24], cosBit);
            step.V7 = TLaneOperator.MultiplyRound(output.V4, cospi[8], cosBit);
            step.V8 = TLaneOperator.AddClamp(output.V8, output.V9, range);
            step.V9 = TLaneOperator.SubtractClamp(output.V8, output.V9, range);
            step.V10 = TLaneOperator.SubtractClamp(output.V11, output.V10, range);
            step.V11 = TLaneOperator.AddClamp(output.V10, output.V11, range);
            step.V12 = TLaneOperator.AddClamp(output.V12, output.V13, range);
            step.V13 = TLaneOperator.SubtractClamp(output.V12, output.V13, range);
            step.V14 = TLaneOperator.SubtractClamp(output.V15, output.V14, range);
            step.V15 = TLaneOperator.AddClamp(output.V14, output.V15, range);
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
            output.V2 = TLaneOperator.MultiplyRound(step.V2, cospi[48], cosBit);
            output.V3 = TLaneOperator.MultiplyRound(step.V2, cospi[16], cosBit);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V5, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V4, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V7, step.V6, range);
            output.V7 = TLaneOperator.AddClamp(step.V6, step.V7, range);
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

            // Every stage 5 position is now set, so the remaining stages are those of the complete transform.
            Dct32Operator.Stages6To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
