// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 16-point inverse ADST with at most 8 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Adst16Low8Operator : IAv1Transform1dOperator
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
        /// Applies the 16-point inverse ADST with eight nonzero inputs to every lane.
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

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization. The zero inputs keep no position.
            output.V1 = input.V0;
            output.V3 = input.V2;
            output.V5 = input.V4;
            output.V7 = input.V6;
            output.V8 = input.V7;
            output.V10 = input.V5;
            output.V12 = input.V3;
            output.V14 = input.V1;

            // Stage 2 applies the terminal odd-angle rotations in reverse. One input of each rotation is zero, so each output is one rounded product.
            step.V0 = TLaneOperator.MultiplyRound(output.V1, cospi[62], cosBit);
            step.V1 = TLaneOperator.MultiplyRound(output.V1, -cospi[2], cosBit);
            step.V2 = TLaneOperator.MultiplyRound(output.V3, cospi[54], cosBit);
            step.V3 = TLaneOperator.MultiplyRound(output.V3, -cospi[10], cosBit);
            step.V4 = TLaneOperator.MultiplyRound(output.V5, cospi[46], cosBit);
            step.V5 = TLaneOperator.MultiplyRound(output.V5, -cospi[18], cosBit);
            step.V6 = TLaneOperator.MultiplyRound(output.V7, cospi[38], cosBit);
            step.V7 = TLaneOperator.MultiplyRound(output.V7, -cospi[26], cosBit);
            step.V8 = TLaneOperator.MultiplyRound(output.V8, cospi[34], cosBit);
            step.V9 = TLaneOperator.MultiplyRound(output.V8, cospi[30], cosBit);
            step.V10 = TLaneOperator.MultiplyRound(output.V10, cospi[42], cosBit);
            step.V11 = TLaneOperator.MultiplyRound(output.V10, cospi[22], cosBit);
            step.V12 = TLaneOperator.MultiplyRound(output.V12, cospi[50], cosBit);
            step.V13 = TLaneOperator.MultiplyRound(output.V12, cospi[14], cosBit);
            step.V14 = TLaneOperator.MultiplyRound(output.V14, cospi[58], cosBit);
            step.V15 = TLaneOperator.MultiplyRound(output.V14, cospi[6], cosBit);

            // Every stage 2 position is now set, so the remaining stages are those of the complete transform.
            Adst16Operator.Stages3To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
