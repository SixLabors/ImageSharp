// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the eight-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct8Operator : IAv1Transform1dOperator
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
        /// Applies the normative eight-point AV1 inverse discrete cosine transform to every lane.
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

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output.V0 = input.V0;
            output.V1 = input.V4;
            output.V2 = input.V2;
            output.V3 = input.V6;
            output.V4 = input.V1;
            output.V5 = input.V5;
            output.V6 = input.V3;
            output.V7 = input.V7;

            // Stage 2 rotates the odd-frequency coefficient pairs by odd multiples of pi/16.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = TLaneOperator.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[8], output.V4, cospi[56], output.V7, cosBit);

            // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
            byte range = stageRange[3];
            output.V0 = TLaneOperator.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = TLaneOperator.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = TLaneOperator.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = TLaneOperator.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V5, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V4, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V7, step.V6, range);
            output.V7 = TLaneOperator.AddClamp(step.V6, step.V7, range);

            // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation. It keeps the stage 3 range.
            step.V0 = TLaneOperator.AddClamp(output.V0, output.V3, range);
            step.V1 = TLaneOperator.AddClamp(output.V1, output.V2, range);
            step.V2 = TLaneOperator.SubtractClamp(output.V1, output.V2, range);
            step.V3 = TLaneOperator.SubtractClamp(output.V0, output.V3, range);
            step.V4 = output.V4;
            step.V5 = TLaneOperator.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;

            // Stage 5 merges the even and odd halves into spatial order and clamps every result.
            range = stageRange[5];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V7, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V6, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V5, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V4, range);
            output.V4 = TLaneOperator.SubtractClamp(step.V3, step.V4, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V2, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V1, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V0, step.V7, range);
        }
    }
}
