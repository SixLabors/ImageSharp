// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 64-point inverse DCT with at most 8 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct64Low8Operator : IAv1Transform1dOperator
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
        /// Applies the 64-point inverse DCT with eight nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the first eight positions are read.</param>
        /// <param name="output">The 64 spatial-domain residual values.</param>
        /// <param name="step">The 64-position stage buffer. It can alias <paramref name="input"/>.</param>
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

            // Stage 1 permutes the coefficients from frequency order into the input order of the recursive butterfly network. Zero inputs keep no position.
            // Each later stage changes only the positions that its comment names. Every stage clamps its sums and differences to its range.
            output.V0 = input.V0;
            output.V8 = input.V4;
            output.V16 = input.V2;
            output.V24 = input.V6;
            output.V32 = input.V1;
            output.V40 = input.V5;
            output.V48 = input.V3;
            output.V56 = input.V7;

            // Stage 2 rotates the mirrored pairs in positions 32 to 63 by odd multiples of pi/128. One input of each rotation is zero, so each output is one
            // rounded product.
            step.V0 = output.V0;
            step.V8 = output.V8;
            step.V16 = output.V16;
            step.V24 = output.V24;
            step.V32 = TLaneOperator.MultiplyRound(output.V32, cospi[63], cosBit);
            step.V39 = TLaneOperator.MultiplyRound(output.V56, -cospi[57], cosBit);
            step.V40 = TLaneOperator.MultiplyRound(output.V40, cospi[59], cosBit);
            step.V47 = TLaneOperator.MultiplyRound(output.V48, -cospi[61], cosBit);
            step.V48 = TLaneOperator.MultiplyRound(output.V48, cospi[3], cosBit);
            step.V55 = TLaneOperator.MultiplyRound(output.V40, cospi[5], cosBit);
            step.V56 = TLaneOperator.MultiplyRound(output.V56, cospi[7], cosBit);
            step.V63 = TLaneOperator.MultiplyRound(output.V32, cospi[1], cosBit);

            // Stage 3 rotates the mirrored pairs in positions 16 to 31 by odd multiples of pi/64. It adds and subtracts adjacent pairs in positions 32 to 63.
            // One term of each pair is zero, so each sum or difference is a clamped copy.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V8 = step.V8;
            output.V16 = TLaneOperator.MultiplyRound(step.V16, cospi[62], cosBit);
            output.V23 = TLaneOperator.MultiplyRound(step.V24, -cospi[58], cosBit);
            output.V24 = TLaneOperator.MultiplyRound(step.V24, cospi[6], cosBit);
            output.V31 = TLaneOperator.MultiplyRound(step.V16, cospi[2], cosBit);
            output.V32 = TLaneOperator.Clamp(step.V32, range);
            output.V33 = TLaneOperator.Clamp(step.V32, range);
            output.V38 = TLaneOperator.Clamp(step.V39, range);
            output.V39 = TLaneOperator.Clamp(step.V39, range);
            output.V40 = TLaneOperator.Clamp(step.V40, range);
            output.V41 = TLaneOperator.Clamp(step.V40, range);
            output.V46 = TLaneOperator.Clamp(step.V47, range);
            output.V47 = TLaneOperator.Clamp(step.V47, range);
            output.V48 = TLaneOperator.Clamp(step.V48, range);
            output.V49 = TLaneOperator.Clamp(step.V48, range);
            output.V54 = TLaneOperator.Clamp(step.V55, range);
            output.V55 = TLaneOperator.Clamp(step.V55, range);
            output.V56 = TLaneOperator.Clamp(step.V56, range);
            output.V57 = TLaneOperator.Clamp(step.V56, range);
            output.V62 = TLaneOperator.Clamp(step.V63, range);
            output.V63 = TLaneOperator.Clamp(step.V63, range);

            // Stage 4 rotates the mirrored pairs in positions 8 to 15 and selected pairs in positions 32 to 63 by odd multiples of pi/32. It adds and subtracts
            // adjacent pairs in positions 16 to 31. One term of each pair in positions 16 to 31 is zero.
            range = stageRange[4];
            step.V0 = output.V0;
            step.V8 = TLaneOperator.MultiplyRound(output.V8, cospi[60], cosBit);
            step.V15 = TLaneOperator.MultiplyRound(output.V8, cospi[4], cosBit);
            step.V16 = TLaneOperator.Clamp(output.V16, range);
            step.V17 = TLaneOperator.Clamp(output.V16, range);
            step.V22 = TLaneOperator.Clamp(output.V23, range);
            step.V23 = TLaneOperator.Clamp(output.V23, range);
            step.V24 = TLaneOperator.Clamp(output.V24, range);
            step.V25 = TLaneOperator.Clamp(output.V24, range);
            step.V30 = TLaneOperator.Clamp(output.V31, range);
            step.V31 = TLaneOperator.Clamp(output.V31, range);
            step.V32 = output.V32;
            step.V33 = TLaneOperator.HalfButterfly(-cospi[4], output.V33, cospi[60], output.V62, cosBit);
            step.V38 = TLaneOperator.HalfButterfly(-cospi[28], output.V38, -cospi[36], output.V57, cosBit);
            step.V39 = output.V39;
            step.V40 = output.V40;
            step.V41 = TLaneOperator.HalfButterfly(-cospi[20], output.V41, cospi[44], output.V54, cosBit);
            step.V46 = TLaneOperator.HalfButterfly(-cospi[12], output.V46, -cospi[52], output.V49, cosBit);
            step.V47 = output.V47;
            step.V48 = output.V48;
            step.V49 = TLaneOperator.HalfButterfly(-cospi[52], output.V46, cospi[12], output.V49, cosBit);
            step.V54 = TLaneOperator.HalfButterfly(cospi[44], output.V41, cospi[20], output.V54, cosBit);
            step.V55 = output.V55;
            step.V56 = output.V56;
            step.V57 = TLaneOperator.HalfButterfly(-cospi[36], output.V38, cospi[28], output.V57, cosBit);
            step.V62 = TLaneOperator.HalfButterfly(cospi[60], output.V33, cospi[4], output.V62, cosBit);
            step.V63 = output.V63;

            Stages5To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 5 to 11 of the 64-point inverse DCT with eight nonzero inputs to every lane.
        /// </summary>
        /// <remarks>
        /// Each method of this stage chain holds two stages, so its lane arithmetic stays inside the JIT inlining budget.
        /// </remarks>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 4 values on entry and the even stages after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        private static void Stages5To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 5 rotates selected pairs in positions 16 to 31 by odd multiples of pi/16. It adds and subtracts adjacent pairs in positions 8 to 15 and
            // mirrored pairs within each group of four in positions 32 to 63. One term of each of these pairs is zero, so each sum or difference is a clamped
            // copy.
            byte range = stageRange[5];
            output.V0 = step.V0;
            output.V8 = TLaneOperator.Clamp(step.V8, range);
            output.V9 = TLaneOperator.Clamp(step.V8, range);
            output.V14 = TLaneOperator.Clamp(step.V15, range);
            output.V15 = TLaneOperator.Clamp(step.V15, range);
            output.V16 = step.V16;
            output.V17 = TLaneOperator.HalfButterfly(-cospi[8], step.V17, cospi[56], step.V30, cosBit);
            output.V22 = TLaneOperator.HalfButterfly(-cospi[24], step.V22, -cospi[40], step.V25, cosBit);
            output.V23 = step.V23;
            output.V24 = step.V24;
            output.V25 = TLaneOperator.HalfButterfly(-cospi[40], step.V22, cospi[24], step.V25, cosBit);
            output.V30 = TLaneOperator.HalfButterfly(cospi[56], step.V17, cospi[8], step.V30, cosBit);
            output.V31 = step.V31;
            output.V32 = TLaneOperator.Clamp(step.V32, range);
            output.V33 = TLaneOperator.Clamp(step.V33, range);
            output.V34 = TLaneOperator.Clamp(step.V33, range);
            output.V35 = TLaneOperator.Clamp(step.V32, range);
            output.V36 = TLaneOperator.Clamp(step.V39, range);
            output.V37 = TLaneOperator.Clamp(step.V38, range);
            output.V38 = TLaneOperator.Clamp(step.V38, range);
            output.V39 = TLaneOperator.Clamp(step.V39, range);
            output.V40 = TLaneOperator.Clamp(step.V40, range);
            output.V41 = TLaneOperator.Clamp(step.V41, range);
            output.V42 = TLaneOperator.Clamp(step.V41, range);
            output.V43 = TLaneOperator.Clamp(step.V40, range);
            output.V44 = TLaneOperator.Clamp(step.V47, range);
            output.V45 = TLaneOperator.Clamp(step.V46, range);
            output.V46 = TLaneOperator.Clamp(step.V46, range);
            output.V47 = TLaneOperator.Clamp(step.V47, range);
            output.V48 = TLaneOperator.Clamp(step.V48, range);
            output.V49 = TLaneOperator.Clamp(step.V49, range);
            output.V50 = TLaneOperator.Clamp(step.V49, range);
            output.V51 = TLaneOperator.Clamp(step.V48, range);
            output.V52 = TLaneOperator.Clamp(step.V55, range);
            output.V53 = TLaneOperator.Clamp(step.V54, range);
            output.V54 = TLaneOperator.Clamp(step.V54, range);
            output.V55 = TLaneOperator.Clamp(step.V55, range);
            output.V56 = TLaneOperator.Clamp(step.V56, range);
            output.V57 = TLaneOperator.Clamp(step.V57, range);
            output.V58 = TLaneOperator.Clamp(step.V57, range);
            output.V59 = TLaneOperator.Clamp(step.V56, range);
            output.V60 = TLaneOperator.Clamp(step.V63, range);
            output.V61 = TLaneOperator.Clamp(step.V62, range);
            output.V62 = TLaneOperator.Clamp(step.V62, range);
            output.V63 = TLaneOperator.Clamp(step.V63, range);

            // Stage 6 rotates positions 0 and 1 by pi/4. It rotates selected pairs in positions 8 to 15 by odd multiples of pi/8 and selected pairs in
            // positions 32 to 63 by odd multiples of pi/16. It adds and subtracts mirrored pairs within each group of four in positions 16 to 31. One term of
            // each of these pairs is zero.
            range = stageRange[6];
            step.V0 = TLaneOperator.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V1 = TLaneOperator.MultiplyRound(output.V0, cospi[32], cosBit);
            step.V8 = output.V8;
            step.V9 = TLaneOperator.HalfButterfly(-cospi[16], output.V9, cospi[48], output.V14, cosBit);
            step.V14 = TLaneOperator.HalfButterfly(cospi[48], output.V9, cospi[16], output.V14, cosBit);
            step.V15 = output.V15;
            step.V16 = TLaneOperator.Clamp(output.V16, range);
            step.V17 = TLaneOperator.Clamp(output.V17, range);
            step.V18 = TLaneOperator.Clamp(output.V17, range);
            step.V19 = TLaneOperator.Clamp(output.V16, range);
            step.V20 = TLaneOperator.Clamp(output.V23, range);
            step.V21 = TLaneOperator.Clamp(output.V22, range);
            step.V22 = TLaneOperator.Clamp(output.V22, range);
            step.V23 = TLaneOperator.Clamp(output.V23, range);
            step.V24 = TLaneOperator.Clamp(output.V24, range);
            step.V25 = TLaneOperator.Clamp(output.V25, range);
            step.V26 = TLaneOperator.Clamp(output.V25, range);
            step.V27 = TLaneOperator.Clamp(output.V24, range);
            step.V28 = TLaneOperator.Clamp(output.V31, range);
            step.V29 = TLaneOperator.Clamp(output.V30, range);
            step.V30 = TLaneOperator.Clamp(output.V30, range);
            step.V31 = TLaneOperator.Clamp(output.V31, range);
            step.V32 = output.V32;
            step.V33 = output.V33;
            step.V34 = TLaneOperator.HalfButterfly(-cospi[8], output.V34, cospi[56], output.V61, cosBit);
            step.V35 = TLaneOperator.HalfButterfly(-cospi[8], output.V35, cospi[56], output.V60, cosBit);
            step.V36 = TLaneOperator.HalfButterfly(-cospi[56], output.V36, -cospi[8], output.V59, cosBit);
            step.V37 = TLaneOperator.HalfButterfly(-cospi[56], output.V37, -cospi[8], output.V58, cosBit);
            step.V38 = output.V38;
            step.V39 = output.V39;
            step.V40 = output.V40;
            step.V41 = output.V41;
            step.V42 = TLaneOperator.HalfButterfly(-cospi[40], output.V42, cospi[24], output.V53, cosBit);
            step.V43 = TLaneOperator.HalfButterfly(-cospi[40], output.V43, cospi[24], output.V52, cosBit);
            step.V44 = TLaneOperator.HalfButterfly(-cospi[24], output.V44, -cospi[40], output.V51, cosBit);
            step.V45 = TLaneOperator.HalfButterfly(-cospi[24], output.V45, -cospi[40], output.V50, cosBit);
            step.V46 = output.V46;
            step.V47 = output.V47;
            step.V48 = output.V48;
            step.V49 = output.V49;
            step.V50 = TLaneOperator.HalfButterfly(-cospi[40], output.V45, cospi[24], output.V50, cosBit);
            step.V51 = TLaneOperator.HalfButterfly(-cospi[40], output.V44, cospi[24], output.V51, cosBit);
            step.V52 = TLaneOperator.HalfButterfly(cospi[24], output.V43, cospi[40], output.V52, cosBit);
            step.V53 = TLaneOperator.HalfButterfly(cospi[24], output.V42, cospi[40], output.V53, cosBit);
            step.V54 = output.V54;
            step.V55 = output.V55;
            step.V56 = output.V56;
            step.V57 = output.V57;
            step.V58 = TLaneOperator.HalfButterfly(-cospi[8], output.V37, cospi[56], output.V58, cosBit);
            step.V59 = TLaneOperator.HalfButterfly(-cospi[8], output.V36, cospi[56], output.V59, cosBit);
            step.V60 = TLaneOperator.HalfButterfly(cospi[56], output.V35, cospi[8], output.V60, cosBit);
            step.V61 = TLaneOperator.HalfButterfly(cospi[56], output.V34, cospi[8], output.V61, cosBit);
            step.V62 = output.V62;
            step.V63 = output.V63;

            Stages7To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 7 to 11 of the 64-point inverse DCT with eight nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 6 values on entry and the even stages after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        private static void Stages7To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 7 rotates selected pairs in positions 16 to 31 by odd multiples of pi/8. It adds and subtracts mirrored pairs within each group of four in
            // positions 0 to 3 and 8 to 15, and within each group of eight in positions 32 to 63. Stage 6 positions 2 to 7 and 10 to 13 are zero. This stage
            // skips positions 4 to 7, because they are zero too.
            byte range = stageRange[7];
            output.V0 = TLaneOperator.Clamp(step.V0, range);
            output.V1 = TLaneOperator.Clamp(step.V1, range);
            output.V2 = TLaneOperator.Clamp(step.V1, range);
            output.V3 = TLaneOperator.Clamp(step.V0, range);
            output.V8 = TLaneOperator.Clamp(step.V8, range);
            output.V9 = TLaneOperator.Clamp(step.V9, range);
            output.V10 = TLaneOperator.Clamp(step.V9, range);
            output.V11 = TLaneOperator.Clamp(step.V8, range);
            output.V12 = TLaneOperator.Clamp(step.V15, range);
            output.V13 = TLaneOperator.Clamp(step.V14, range);
            output.V14 = TLaneOperator.Clamp(step.V14, range);
            output.V15 = TLaneOperator.Clamp(step.V15, range);
            output.V16 = step.V16;
            output.V17 = step.V17;
            output.V18 = TLaneOperator.HalfButterfly(-cospi[16], step.V18, cospi[48], step.V29, cosBit);
            output.V19 = TLaneOperator.HalfButterfly(-cospi[16], step.V19, cospi[48], step.V28, cosBit);
            output.V20 = TLaneOperator.HalfButterfly(-cospi[48], step.V20, -cospi[16], step.V27, cosBit);
            output.V21 = TLaneOperator.HalfButterfly(-cospi[48], step.V21, -cospi[16], step.V26, cosBit);
            output.V22 = step.V22;
            output.V23 = step.V23;
            output.V24 = step.V24;
            output.V25 = step.V25;
            output.V26 = TLaneOperator.HalfButterfly(-cospi[16], step.V21, cospi[48], step.V26, cosBit);
            output.V27 = TLaneOperator.HalfButterfly(-cospi[16], step.V20, cospi[48], step.V27, cosBit);
            output.V28 = TLaneOperator.HalfButterfly(cospi[48], step.V19, cospi[16], step.V28, cosBit);
            output.V29 = TLaneOperator.HalfButterfly(cospi[48], step.V18, cospi[16], step.V29, cosBit);
            output.V30 = step.V30;
            output.V31 = step.V31;
            output.V32 = TLaneOperator.AddClamp(step.V32, step.V39, range);
            output.V33 = TLaneOperator.AddClamp(step.V33, step.V38, range);
            output.V34 = TLaneOperator.AddClamp(step.V34, step.V37, range);
            output.V35 = TLaneOperator.AddClamp(step.V35, step.V36, range);
            output.V36 = TLaneOperator.SubtractClamp(step.V35, step.V36, range);
            output.V37 = TLaneOperator.SubtractClamp(step.V34, step.V37, range);
            output.V38 = TLaneOperator.SubtractClamp(step.V33, step.V38, range);
            output.V39 = TLaneOperator.SubtractClamp(step.V32, step.V39, range);
            output.V40 = TLaneOperator.SubtractClamp(step.V47, step.V40, range);
            output.V41 = TLaneOperator.SubtractClamp(step.V46, step.V41, range);
            output.V42 = TLaneOperator.SubtractClamp(step.V45, step.V42, range);
            output.V43 = TLaneOperator.SubtractClamp(step.V44, step.V43, range);
            output.V44 = TLaneOperator.AddClamp(step.V43, step.V44, range);
            output.V45 = TLaneOperator.AddClamp(step.V42, step.V45, range);
            output.V46 = TLaneOperator.AddClamp(step.V41, step.V46, range);
            output.V47 = TLaneOperator.AddClamp(step.V40, step.V47, range);
            output.V48 = TLaneOperator.AddClamp(step.V48, step.V55, range);
            output.V49 = TLaneOperator.AddClamp(step.V49, step.V54, range);
            output.V50 = TLaneOperator.AddClamp(step.V50, step.V53, range);
            output.V51 = TLaneOperator.AddClamp(step.V51, step.V52, range);
            output.V52 = TLaneOperator.SubtractClamp(step.V51, step.V52, range);
            output.V53 = TLaneOperator.SubtractClamp(step.V50, step.V53, range);
            output.V54 = TLaneOperator.SubtractClamp(step.V49, step.V54, range);
            output.V55 = TLaneOperator.SubtractClamp(step.V48, step.V55, range);
            output.V56 = TLaneOperator.SubtractClamp(step.V63, step.V56, range);
            output.V57 = TLaneOperator.SubtractClamp(step.V62, step.V57, range);
            output.V58 = TLaneOperator.SubtractClamp(step.V61, step.V58, range);
            output.V59 = TLaneOperator.SubtractClamp(step.V60, step.V59, range);
            output.V60 = TLaneOperator.AddClamp(step.V59, step.V60, range);
            output.V61 = TLaneOperator.AddClamp(step.V58, step.V61, range);
            output.V62 = TLaneOperator.AddClamp(step.V57, step.V62, range);
            output.V63 = TLaneOperator.AddClamp(step.V56, step.V63, range);

            // Stage 8 rotates positions 10 to 13 by pi/4 and selected pairs in positions 32 to 63 by odd multiples of pi/8. It adds and subtracts mirrored
            // pairs within each group of eight in positions 0 to 7 and 16 to 31. Positions 4 to 7 of stage 7 are zero.
            range = stageRange[8];
            step.V0 = TLaneOperator.Clamp(output.V0, range);
            step.V1 = TLaneOperator.Clamp(output.V1, range);
            step.V2 = TLaneOperator.Clamp(output.V2, range);
            step.V3 = TLaneOperator.Clamp(output.V3, range);
            step.V4 = TLaneOperator.Clamp(output.V3, range);
            step.V5 = TLaneOperator.Clamp(output.V2, range);
            step.V6 = TLaneOperator.Clamp(output.V1, range);
            step.V7 = TLaneOperator.Clamp(output.V0, range);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = TLaneOperator.HalfButterfly(-cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V11 = TLaneOperator.HalfButterfly(-cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V12 = TLaneOperator.HalfButterfly(cospi[32], output.V11, cospi[32], output.V12, cosBit);
            step.V13 = TLaneOperator.HalfButterfly(cospi[32], output.V10, cospi[32], output.V13, cosBit);
            step.V14 = output.V14;
            step.V15 = output.V15;
            step.V16 = TLaneOperator.AddClamp(output.V16, output.V23, range);
            step.V17 = TLaneOperator.AddClamp(output.V17, output.V22, range);
            step.V18 = TLaneOperator.AddClamp(output.V18, output.V21, range);
            step.V19 = TLaneOperator.AddClamp(output.V19, output.V20, range);
            step.V20 = TLaneOperator.SubtractClamp(output.V19, output.V20, range);
            step.V21 = TLaneOperator.SubtractClamp(output.V18, output.V21, range);
            step.V22 = TLaneOperator.SubtractClamp(output.V17, output.V22, range);
            step.V23 = TLaneOperator.SubtractClamp(output.V16, output.V23, range);
            step.V24 = TLaneOperator.SubtractClamp(output.V31, output.V24, range);
            step.V25 = TLaneOperator.SubtractClamp(output.V30, output.V25, range);
            step.V26 = TLaneOperator.SubtractClamp(output.V29, output.V26, range);
            step.V27 = TLaneOperator.SubtractClamp(output.V28, output.V27, range);
            step.V28 = TLaneOperator.AddClamp(output.V27, output.V28, range);
            step.V29 = TLaneOperator.AddClamp(output.V26, output.V29, range);
            step.V30 = TLaneOperator.AddClamp(output.V25, output.V30, range);
            step.V31 = TLaneOperator.AddClamp(output.V24, output.V31, range);
            step.V32 = output.V32;
            step.V33 = output.V33;
            step.V34 = output.V34;
            step.V35 = output.V35;
            step.V36 = TLaneOperator.HalfButterfly(-cospi[16], output.V36, cospi[48], output.V59, cosBit);
            step.V37 = TLaneOperator.HalfButterfly(-cospi[16], output.V37, cospi[48], output.V58, cosBit);
            step.V38 = TLaneOperator.HalfButterfly(-cospi[16], output.V38, cospi[48], output.V57, cosBit);
            step.V39 = TLaneOperator.HalfButterfly(-cospi[16], output.V39, cospi[48], output.V56, cosBit);
            step.V40 = TLaneOperator.HalfButterfly(-cospi[48], output.V40, -cospi[16], output.V55, cosBit);
            step.V41 = TLaneOperator.HalfButterfly(-cospi[48], output.V41, -cospi[16], output.V54, cosBit);
            step.V42 = TLaneOperator.HalfButterfly(-cospi[48], output.V42, -cospi[16], output.V53, cosBit);
            step.V43 = TLaneOperator.HalfButterfly(-cospi[48], output.V43, -cospi[16], output.V52, cosBit);
            step.V44 = output.V44;
            step.V45 = output.V45;
            step.V46 = output.V46;
            step.V47 = output.V47;
            step.V48 = output.V48;
            step.V49 = output.V49;
            step.V50 = output.V50;
            step.V51 = output.V51;
            step.V52 = TLaneOperator.HalfButterfly(-cospi[16], output.V43, cospi[48], output.V52, cosBit);
            step.V53 = TLaneOperator.HalfButterfly(-cospi[16], output.V42, cospi[48], output.V53, cosBit);
            step.V54 = TLaneOperator.HalfButterfly(-cospi[16], output.V41, cospi[48], output.V54, cosBit);
            step.V55 = TLaneOperator.HalfButterfly(-cospi[16], output.V40, cospi[48], output.V55, cosBit);
            step.V56 = TLaneOperator.HalfButterfly(cospi[48], output.V39, cospi[16], output.V56, cosBit);
            step.V57 = TLaneOperator.HalfButterfly(cospi[48], output.V38, cospi[16], output.V57, cosBit);
            step.V58 = TLaneOperator.HalfButterfly(cospi[48], output.V37, cospi[16], output.V58, cosBit);
            step.V59 = TLaneOperator.HalfButterfly(cospi[48], output.V36, cospi[16], output.V59, cosBit);
            step.V60 = output.V60;
            step.V61 = output.V61;
            step.V62 = output.V62;
            step.V63 = output.V63;

            // Every stage 8 position is now set, so the remaining stages are those of the complete transform.
            Dct64Operator.Stages9To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
