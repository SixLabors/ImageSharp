// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 16-point inverse ADST with at most 1 low-frequency input coefficient.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Adst16Low1Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 1;

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
        /// Applies the 16-point inverse ADST with one nonzero input to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the DC position is read.</param>
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
            output.V1 = input.V0;

            // Stage 2 applies the terminal odd-angle rotations in reverse. The other rotation input is zero, so each output is one rounded product.
            step.V0 = TLaneOperator.MultiplyRound(output.V1, cospi[62], cosBit);
            step.V1 = TLaneOperator.MultiplyRound(output.V1, -cospi[2], cosBit);

            // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane. The other half is zero.
            byte range = stageRange[3];
            output.V0 = TLaneOperator.Clamp(step.V0, range);
            output.V1 = TLaneOperator.Clamp(step.V1, range);
            output.V8 = TLaneOperator.Clamp(step.V0, range);
            output.V9 = TLaneOperator.Clamp(step.V1, range);

            // Stage 4 reverses the rotations by odd multiples of pi/16 in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V8 = TLaneOperator.HalfButterfly(cospi[8], output.V8, cospi[56], output.V9, cosBit);
            step.V9 = TLaneOperator.HalfButterfly(cospi[56], output.V8, -cospi[8], output.V9, cosBit);

            // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane. The other group of each half is zero.
            range = stageRange[5];
            output.V0 = TLaneOperator.Clamp(step.V0, range);
            output.V1 = TLaneOperator.Clamp(step.V1, range);
            output.V4 = TLaneOperator.Clamp(step.V0, range);
            output.V5 = TLaneOperator.Clamp(step.V1, range);
            output.V8 = TLaneOperator.Clamp(step.V8, range);
            output.V9 = TLaneOperator.Clamp(step.V9, range);
            output.V12 = TLaneOperator.Clamp(step.V8, range);
            output.V13 = TLaneOperator.Clamp(step.V9, range);

            // Stage 6 reverses the pi/8 and 3pi/8 rotations.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V4 = TLaneOperator.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V12 = TLaneOperator.HalfButterfly(cospi[16], output.V12, cospi[48], output.V13, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[48], output.V12, -cospi[16], output.V13, cosBit);

            // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane. The other pair of each group is zero.
            range = stageRange[7];
            output.V0 = TLaneOperator.Clamp(step.V0, range);
            output.V1 = TLaneOperator.Clamp(step.V1, range);
            output.V2 = TLaneOperator.Clamp(step.V0, range);
            output.V3 = TLaneOperator.Clamp(step.V1, range);
            output.V4 = TLaneOperator.Clamp(step.V4, range);
            output.V5 = TLaneOperator.Clamp(step.V5, range);
            output.V6 = TLaneOperator.Clamp(step.V4, range);
            output.V7 = TLaneOperator.Clamp(step.V5, range);
            output.V8 = TLaneOperator.Clamp(step.V8, range);
            output.V9 = TLaneOperator.Clamp(step.V9, range);
            output.V10 = TLaneOperator.Clamp(step.V8, range);
            output.V11 = TLaneOperator.Clamp(step.V9, range);
            output.V12 = TLaneOperator.Clamp(step.V12, range);
            output.V13 = TLaneOperator.Clamp(step.V13, range);
            output.V14 = TLaneOperator.Clamp(step.V12, range);
            output.V15 = TLaneOperator.Clamp(step.V13, range);

            Adst16Operator.Stages8To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit);
        }
    }
}
