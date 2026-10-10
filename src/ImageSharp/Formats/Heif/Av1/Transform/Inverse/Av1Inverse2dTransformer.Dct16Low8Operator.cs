// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 16-point inverse DCT with at most 8 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct16Low8Operator : IAv1Transform1dOperator
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
        /// Applies the 16-point inverse DCT with eight nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the first eight positions are read.</param>
        /// <param name="output">The sixteen spatial-domain residual values.</param>
        /// <param name="step">The sixteen-position stage buffer. It can alias <paramref name="input"/>.</param>
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
            output.V2 = input.V4;
            output.V4 = input.V2;
            output.V6 = input.V6;
            output.V8 = input.V1;
            output.V10 = input.V5;
            output.V12 = input.V3;
            output.V14 = input.V7;

            // Stage 2 rotates the odd-frequency pairs by odd multiples of pi/32. One input of each rotation is zero, so each output is one rounded product.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = TLaneOperator.MultiplyRound(output.V8, cospi[60], cosBit);
            step.V9 = TLaneOperator.MultiplyRound(output.V14, -cospi[36], cosBit);
            step.V10 = TLaneOperator.MultiplyRound(output.V10, cospi[44], cosBit);
            step.V11 = TLaneOperator.MultiplyRound(output.V12, -cospi[52], cosBit);
            step.V12 = TLaneOperator.MultiplyRound(output.V12, cospi[12], cosBit);
            step.V13 = TLaneOperator.MultiplyRound(output.V10, cospi[20], cosBit);
            step.V14 = TLaneOperator.MultiplyRound(output.V14, cospi[28], cosBit);
            step.V15 = TLaneOperator.MultiplyRound(output.V8, cospi[4], cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = TLaneOperator.MultiplyRound(step.V4, cospi[56], cosBit);
            output.V5 = TLaneOperator.MultiplyRound(step.V6, -cospi[40], cosBit);
            output.V6 = TLaneOperator.MultiplyRound(step.V6, cospi[24], cosBit);
            output.V7 = TLaneOperator.MultiplyRound(step.V4, cospi[8], cosBit);
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V9, range);
            output.V9 = TLaneOperator.SubtractClamp(step.V8, step.V9, range);
            output.V10 = TLaneOperator.SubtractClamp(step.V11, step.V10, range);
            output.V11 = TLaneOperator.AddClamp(step.V10, step.V11, range);
            output.V12 = TLaneOperator.AddClamp(step.V12, step.V13, range);
            output.V13 = TLaneOperator.SubtractClamp(step.V12, step.V13, range);
            output.V14 = TLaneOperator.SubtractClamp(step.V15, step.V14, range);
            output.V15 = TLaneOperator.AddClamp(step.V14, step.V15, range);

            // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            range = stageRange[4];
            step.V0 = TLaneOperator.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V1 = TLaneOperator.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V2 = TLaneOperator.MultiplyRound(output.V2, cospi[48], cosBit);
            step.V3 = TLaneOperator.MultiplyRound(output.V2, cospi[16], cosBit);
            step.V4 = TLaneOperator.AddClamp(output.V4, output.V5, range);
            step.V5 = TLaneOperator.SubtractClamp(output.V4, output.V5, range);
            step.V6 = TLaneOperator.SubtractClamp(output.V7, output.V6, range);
            step.V7 = TLaneOperator.AddClamp(output.V6, output.V7, range);
            step.V8 = output.V8;
            step.V9 = TLaneOperator.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V10 = TLaneOperator.HalfButterfly(-cospi[48], output.V10, -cospi[16], output.V13, cosBit);
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = TLaneOperator.HalfButterfly(-cospi[16], output.V10, cospi[48], output.V13, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;

            // Every stage 4 position is now set, so the remaining stages are those of the complete transform.
            Dct16Operator.Stages5To7<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
