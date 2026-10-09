// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 16-point AV1 inverse asymmetric discrete sine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Adst16Operator : IAv1Transform1dOperator
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
        /// Applies stages 3 to 9 of the 16-point inverse ADST to every lane. The eight-input sparse operator shares these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the sixteen spatial-domain residual values.</param>
        /// <param name="step">The stage 2 values on entry and the even stages after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages3To9<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
            byte range = stageRange[3];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V8, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V9, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V10, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V11, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V12, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V13, range);
            output.V6 = TLaneOperator.AddClamp(step.V6, step.V14, range);
            output.V7 = TLaneOperator.AddClamp(step.V7, step.V15, range);
            output.V8 = TLaneOperator.SubtractClamp(step.V0, step.V8, range);
            output.V9 = TLaneOperator.SubtractClamp(step.V1, step.V9, range);
            output.V10 = TLaneOperator.SubtractClamp(step.V2, step.V10, range);
            output.V11 = TLaneOperator.SubtractClamp(step.V3, step.V11, range);
            output.V12 = TLaneOperator.SubtractClamp(step.V4, step.V12, range);
            output.V13 = TLaneOperator.SubtractClamp(step.V5, step.V13, range);
            output.V14 = TLaneOperator.SubtractClamp(step.V6, step.V14, range);
            output.V15 = TLaneOperator.SubtractClamp(step.V7, step.V15, range);

            // Stage 4 reverses the rotations by odd multiples of pi/16 in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = output.V6;
            step.V7 = output.V7;
            step.V8 = TLaneOperator.HalfButterfly(cospi[8], output.V8, cospi[56], output.V9, cosBit);
            step.V9 = TLaneOperator.HalfButterfly(cospi[56], output.V8, -cospi[8], output.V9, cosBit);
            step.V10 = TLaneOperator.HalfButterfly(cospi[40], output.V10, cospi[24], output.V11, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(cospi[24], output.V10, -cospi[40], output.V11, cosBit);
            step.V12 = TLaneOperator.HalfButterfly(-cospi[56], output.V12, cospi[8], output.V13, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[8], output.V12, cospi[56], output.V13, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(-cospi[24], output.V14, cospi[40], output.V15, cosBit);
            step.V15 = TLaneOperator.HalfButterfly(cospi[40], output.V14, cospi[24], output.V15, cosBit);

            // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
            range = stageRange[5];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V4, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V5, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V6, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V7, range);
            output.V4 = TLaneOperator.SubtractClamp(step.V0, step.V4, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V1, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V2, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V3, step.V7, range);
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V12, range);
            output.V9 = TLaneOperator.AddClamp(step.V9, step.V13, range);
            output.V10 = TLaneOperator.AddClamp(step.V10, step.V14, range);
            output.V11 = TLaneOperator.AddClamp(step.V11, step.V15, range);
            output.V12 = TLaneOperator.SubtractClamp(step.V8, step.V12, range);
            output.V13 = TLaneOperator.SubtractClamp(step.V9, step.V13, range);
            output.V14 = TLaneOperator.SubtractClamp(step.V10, step.V14, range);
            output.V15 = TLaneOperator.SubtractClamp(step.V11, step.V15, range);

            // Stage 6 reverses the pi/8 and 3pi/8 rotations.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = TLaneOperator.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(-cospi[48], output.V6, cospi[16], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[16], output.V6, cospi[48], output.V7, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = output.V10;
            step.V11 = output.V11;
            step.V12 = TLaneOperator.HalfButterfly(cospi[16], output.V12, cospi[48], output.V13, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[48], output.V12, -cospi[16], output.V13, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(-cospi[48], output.V14, cospi[16], output.V15, cosBit);
            step.V15 = TLaneOperator.HalfButterfly(cospi[16], output.V14, cospi[48], output.V15, cosBit);

            // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
            range = stageRange[7];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V2, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V3, range);
            output.V2 = TLaneOperator.SubtractClamp(step.V0, step.V2, range);
            output.V3 = TLaneOperator.SubtractClamp(step.V1, step.V3, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V6, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V7, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V4, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V5, step.V7, range);
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V10, range);
            output.V9 = TLaneOperator.AddClamp(step.V9, step.V11, range);
            output.V10 = TLaneOperator.SubtractClamp(step.V8, step.V10, range);
            output.V11 = TLaneOperator.SubtractClamp(step.V9, step.V11, range);
            output.V12 = TLaneOperator.AddClamp(step.V12, step.V14, range);
            output.V13 = TLaneOperator.AddClamp(step.V13, step.V15, range);
            output.V14 = TLaneOperator.SubtractClamp(step.V12, step.V14, range);
            output.V15 = TLaneOperator.SubtractClamp(step.V13, step.V15, range);

            Stages8To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit);
        }

        /// <summary>
        /// Applies stages 8 and 9 of the 16-point inverse ADST to every lane. Both sparse operators share these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage 7 values on entry and the sixteen spatial-domain residual values on return.</param>
        /// <param name="step">The stage buffer that receives the stage 8 values.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Stages8To9<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 8 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = TLaneOperator.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = TLaneOperator.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = TLaneOperator.HalfButterfly(cospi[32], output.V10, cospi[32], output.V11, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(cospi[32], output.V10, -cospi[32], output.V11, cosBit);
            step.V12 = output.V12;
            step.V13 = output.V13;
            step.V14 = TLaneOperator.HalfButterfly(cospi[32], output.V14, cospi[32], output.V15, cosBit);
            step.V15 = TLaneOperator.HalfButterfly(cospi[32], output.V14, -cospi[32], output.V15, cosBit);

            // Stage 9 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = TLaneOperator.Negate(step.V8);
            output.V2 = step.V12;
            output.V3 = TLaneOperator.Negate(step.V4);
            output.V4 = step.V6;
            output.V5 = TLaneOperator.Negate(step.V14);
            output.V6 = step.V10;
            output.V7 = TLaneOperator.Negate(step.V2);
            output.V8 = step.V3;
            output.V9 = TLaneOperator.Negate(step.V11);
            output.V10 = step.V15;
            output.V11 = TLaneOperator.Negate(step.V7);
            output.V12 = step.V5;
            output.V13 = TLaneOperator.Negate(step.V13);
            output.V14 = step.V9;
            output.V15 = TLaneOperator.Negate(step.V1);
        }

        /// <summary>
        /// Applies the normative 16-point AV1 inverse asymmetric discrete sine transform to every lane.
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

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
            output.V0 = input.V15;
            output.V1 = input.V0;
            output.V2 = input.V13;
            output.V3 = input.V2;
            output.V4 = input.V11;
            output.V5 = input.V4;
            output.V6 = input.V9;
            output.V7 = input.V6;
            output.V8 = input.V7;
            output.V9 = input.V8;
            output.V10 = input.V5;
            output.V11 = input.V10;
            output.V12 = input.V3;
            output.V13 = input.V12;
            output.V14 = input.V1;
            output.V15 = input.V14;

            // Stage 2 applies the terminal odd-angle rotations in reverse.
            step.V0 = TLaneOperator.HalfButterfly(cospi[2], output.V0, cospi[62], output.V1, cosBit);
            step.V1 = TLaneOperator.HalfButterfly(cospi[62], output.V0, -cospi[2], output.V1, cosBit);
            step.V2 = TLaneOperator.HalfButterfly(cospi[10], output.V2, cospi[54], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[54], output.V2, -cospi[10], output.V3, cosBit);
            step.V4 = TLaneOperator.HalfButterfly(cospi[18], output.V4, cospi[46], output.V5, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[46], output.V4, -cospi[18], output.V5, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[26], output.V6, cospi[38], output.V7, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[38], output.V6, -cospi[26], output.V7, cosBit);
            step.V8 = TLaneOperator.HalfButterfly(cospi[34], output.V8, cospi[30], output.V9, cosBit);
            step.V9 = TLaneOperator.HalfButterfly(cospi[30], output.V8, -cospi[34], output.V9, cosBit);
            step.V10 = TLaneOperator.HalfButterfly(cospi[42], output.V10, cospi[22], output.V11, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(cospi[22], output.V10, -cospi[42], output.V11, cosBit);
            step.V12 = TLaneOperator.HalfButterfly(cospi[50], output.V12, cospi[14], output.V13, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[14], output.V12, -cospi[50], output.V13, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(cospi[58], output.V14, cospi[6], output.V15, cosBit);
            step.V15 = TLaneOperator.HalfButterfly(cospi[6], output.V14, -cospi[58], output.V15, cosBit);

            Stages3To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
