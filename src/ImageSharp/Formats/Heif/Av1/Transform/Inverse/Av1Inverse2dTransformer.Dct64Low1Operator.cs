// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 64-point inverse DCT with at most 1 low-frequency input coefficient.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that only the first input can be nonzero. That input is the DC coefficient. Its basis function is constant, so every
    /// output has the same value. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct64Low1Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 1;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
            => TransformLanes<int, ScalarLaneOperator>(ref AsPositions(input), ref AsPositions(output), cosBit, stageRange);

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
            => TransformLanes<Vector128<int>, Vector128LaneOperator>(ref input, ref output, cosBit, stageRange);

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
            => TransformLanes<Vector256<int>, Vector256LaneOperator>(ref input, ref output, cosBit, stageRange);

        /// <summary>
        /// Applies the 64-point inverse DCT with one nonzero input to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the DC position is read.</param>
        /// <param name="output">The sixty-four spatial-domain residual values.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TransformLanes<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> input,
            ref Av1TransformVector<TLanes> output,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // The full network scales the DC input by cos(pi/4) in stage 6. The remaining butterflies copy that value into every output. The inverse stage
            // ranges are the same for every stage of an axis, so one clamp gives the same result as the repeated stage clamps.
            int cosine = Av1SinusConstants.CosinusPi(cosBit)[32];
            TLanes value = TLaneOperator.Clamp(TLaneOperator.MultiplyRound(input.V0, cosine, cosBit), stageRange[11]);

            MemoryMarshal.CreateSpan(ref output.V0, 64).Fill(value);
        }
    }
}
