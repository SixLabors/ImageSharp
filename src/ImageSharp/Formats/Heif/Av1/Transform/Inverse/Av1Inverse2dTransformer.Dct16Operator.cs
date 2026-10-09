// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 16-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct16Operator : IAv1Transform1dOperator
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
        /// Applies stages 5 to 7 of the 16-point inverse DCT to every lane. The eight-input sparse operator shares these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives stage 5 and, on return, the sixteen spatial-domain residual values.</param>
        /// <param name="step">The stage 4 values on entry and the stage 6 values after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages5To7<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 5 widens the reconstructed groups through their next butterfly level.
            byte range = stageRange[5];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V3, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V2, range);
            output.V2 = TLaneOperator.SubtractClamp(step.V1, step.V2, range);
            output.V3 = TLaneOperator.SubtractClamp(step.V0, step.V3, range);
            output.V4 = step.V4;
            output.V5 = TLaneOperator.HalfButterfly(-cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V6 = TLaneOperator.HalfButterfly(cospi[32], step.V5, cospi[32], step.V6, cosBit);
            output.V7 = step.V7;
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V11, range);
            output.V9 = TLaneOperator.AddClamp(step.V9, step.V10, range);
            output.V10 = TLaneOperator.SubtractClamp(step.V9, step.V10, range);
            output.V11 = TLaneOperator.SubtractClamp(step.V8, step.V11, range);
            output.V12 = TLaneOperator.SubtractClamp(step.V15, step.V12, range);
            output.V13 = TLaneOperator.SubtractClamp(step.V14, step.V13, range);
            output.V14 = TLaneOperator.AddClamp(step.V13, step.V14, range);
            output.V15 = TLaneOperator.AddClamp(step.V12, step.V15, range);

            // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
            range = stageRange[6];
            step.V0 = TLaneOperator.AddClamp(output.V0, output.V7, range);
            step.V1 = TLaneOperator.AddClamp(output.V1, output.V6, range);
            step.V2 = TLaneOperator.AddClamp(output.V2, output.V5, range);
            step.V3 = TLaneOperator.AddClamp(output.V3, output.V4, range);
            step.V4 = TLaneOperator.SubtractClamp(output.V3, output.V4, range);
            step.V5 = TLaneOperator.SubtractClamp(output.V2, output.V5, range);
            step.V6 = TLaneOperator.SubtractClamp(output.V1, output.V6, range);
            step.V7 = TLaneOperator.SubtractClamp(output.V0, output.V7, range);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = TLaneOperator.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = TLaneOperator.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;

            // Stage 7 merges the even and odd halves into spatial order and clamps every result.
            range = stageRange[7];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V15, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V14, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V13, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V12, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V11, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V10, range);
            output.V6 = TLaneOperator.AddClamp(step.V6, step.V9, range);
            output.V7 = TLaneOperator.AddClamp(step.V7, step.V8, range);
            output.V8 = TLaneOperator.SubtractClamp(step.V7, step.V8, range);
            output.V9 = TLaneOperator.SubtractClamp(step.V6, step.V9, range);
            output.V10 = TLaneOperator.SubtractClamp(step.V5, step.V10, range);
            output.V11 = TLaneOperator.SubtractClamp(step.V4, step.V11, range);
            output.V12 = TLaneOperator.SubtractClamp(step.V3, step.V12, range);
            output.V13 = TLaneOperator.SubtractClamp(step.V2, step.V13, range);
            output.V14 = TLaneOperator.SubtractClamp(step.V1, step.V14, range);
            output.V15 = TLaneOperator.SubtractClamp(step.V0, step.V15, range);
        }

        /// <summary>
        /// Applies the normative 16-point AV1 inverse discrete cosine transform to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The sixteen frequency-domain coefficients.</param>
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

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
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

            // Stage 2 rotates the highest odd-frequency coefficient pairs by odd multiples of pi/32.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = output.V6;
            step.V7 = output.V7;
            step.V8 = TLaneOperator.HalfButterfly(cospi[60], output.V8, -cospi[4], output.V15, cosBit);
            step.V9 = TLaneOperator.HalfButterfly(cospi[28], output.V9, -cospi[36], output.V14, cosBit);
            step.V10 = TLaneOperator.HalfButterfly(cospi[44], output.V10, -cospi[20], output.V13, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(cospi[12], output.V11, -cospi[52], output.V12, cosBit);
            step.V12 = TLaneOperator.HalfButterfly(cospi[52], output.V11, cospi[12], output.V12, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[20], output.V10, cospi[44], output.V13, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(cospi[36], output.V9, cospi[28], output.V14, cosBit);
            step.V15 = TLaneOperator.HalfButterfly(cospi[4], output.V8, cospi[60], output.V15, cosBit);

            // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = TLaneOperator.HalfButterfly(cospi[56], step.V4, -cospi[8], step.V7, cosBit);
            output.V5 = TLaneOperator.HalfButterfly(cospi[24], step.V5, -cospi[40], step.V6, cosBit);
            output.V6 = TLaneOperator.HalfButterfly(cospi[40], step.V5, cospi[24], step.V6, cosBit);
            output.V7 = TLaneOperator.HalfButterfly(cospi[8], step.V4, cospi[56], step.V7, cosBit);
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
            step.V0 = TLaneOperator.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = TLaneOperator.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = TLaneOperator.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);
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

            Stages5To7<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
