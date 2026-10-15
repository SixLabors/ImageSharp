// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the eight-point AV1 inverse asymmetric discrete sine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Adst8Operator : IAv1Transform1dOperator
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
        /// Applies stages 6 and 7 of the eight-point inverse ADST to every lane. The sparse operator shares these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage 5 values on entry and the eight spatial-domain residual values on return.</param>
        /// <param name="step">The stage buffer that receives the stage 6 values.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Stages6To7<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 6 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = TLaneOperator.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = TLaneOperator.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);

            // Stage 7 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = TLaneOperator.Negate(step.V4);
            output.V2 = step.V6;
            output.V3 = TLaneOperator.Negate(step.V2);
            output.V4 = step.V3;
            output.V5 = TLaneOperator.Negate(step.V7);
            output.V6 = step.V5;
            output.V7 = TLaneOperator.Negate(step.V1);
        }

        /// <summary>
        /// Applies the normative eight-point AV1 inverse asymmetric discrete sine transform to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The eight frequency-domain coefficients.</param>
        /// <param name="output">The eight spatial-domain residual values.</param>
        /// <param name="step">The eight-position stage buffer. It can alias <paramref name="input"/>.</param>
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

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
            output.V0 = input.V7;
            output.V1 = input.V0;
            output.V2 = input.V5;
            output.V3 = input.V2;
            output.V4 = input.V3;
            output.V5 = input.V4;
            output.V6 = input.V1;
            output.V7 = input.V6;

            // Stage 2 applies the terminal odd-angle rotations in reverse.
            step.V0 = TLaneOperator.HalfButterfly(cospi[4], output.V0, cospi[60], output.V1, cosBit);
            step.V1 = TLaneOperator.HalfButterfly(cospi[60], output.V0, -cospi[4], output.V1, cosBit);
            step.V2 = TLaneOperator.HalfButterfly(cospi[20], output.V2, cospi[44], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[44], output.V2, -cospi[20], output.V3, cosBit);
            step.V4 = TLaneOperator.HalfButterfly(cospi[36], output.V4, cospi[28], output.V5, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[28], output.V4, -cospi[36], output.V5, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[52], output.V6, cospi[12], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[12], output.V6, -cospi[52], output.V7, cosBit);

            // Stage 3 separates the complete butterfly into two four-sample halves and clamps each lane.
            byte range = stageRange[3];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V4, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V5, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V6, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V7, range);
            output.V4 = TLaneOperator.SubtractClamp(step.V0, step.V4, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V1, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V2, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V3, step.V7, range);

            // Stage 4 reverses the pi/8 and 3pi/8 rotations in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = TLaneOperator.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(-cospi[48], output.V6, cospi[16], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[16], output.V6, cospi[48], output.V7, cosBit);

            // Stage 5 separates the four-sample halves into adjacent coefficient pairs and clamps each lane.
            range = stageRange[5];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V2, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V3, range);
            output.V2 = TLaneOperator.SubtractClamp(step.V0, step.V2, range);
            output.V3 = TLaneOperator.SubtractClamp(step.V1, step.V3, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V6, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V7, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V4, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V5, step.V7, range);

            Stages6To7<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit);
        }
    }
}
