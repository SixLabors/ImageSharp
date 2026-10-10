// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the four-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct4Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 4;

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
        /// Applies the normative four-point AV1 inverse discrete cosine transform to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The four frequency-domain coefficients.</param>
        /// <param name="output">The four spatial-domain residual values.</param>
        /// <param name="step">The four-position stage buffer. It can alias <paramref name="input"/>.</param>
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
            // AV1 stores coefficients in frequency order. This permutation restores the input order of the staged DCT.
            output.V0 = input.V0;
            output.V1 = input.V2;
            output.V2 = input.V1;
            output.V3 = input.V3;

            // Rotate the even and odd coefficient pairs using the same fixed-point basis as the forward transform.
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            step.V0 = TLaneOperator.HalfButterfly(cospi[32], output.V0, cospi[32], output.V1, cosBit);
            step.V1 = TLaneOperator.HalfButterfly(cospi[32], output.V0, -cospi[32], output.V1, cosBit);
            step.V2 = TLaneOperator.HalfButterfly(cospi[48], output.V2, -cospi[16], output.V3, cosBit);
            step.V3 = TLaneOperator.HalfButterfly(cospi[16], output.V2, cospi[48], output.V3, cosBit);

            // The terminal butterflies reconstruct spatial order and clamp every result to the normative stage range.
            byte range = stageRange[3];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V3, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V2, range);
            output.V2 = TLaneOperator.SubtractClamp(step.V1, step.V2, range);
            output.V3 = TLaneOperator.SubtractClamp(step.V0, step.V3, range);
        }
    }
}
