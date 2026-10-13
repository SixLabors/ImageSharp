// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 64-point inverse DCT with at most 32 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees that all later inputs are zero. Rotations with one surviving input retain their original rounding boundary. All
    /// nonzero butterfly outputs retain their stage clamps. Vector fields identify transform positions. Lanes stay independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct64Low32Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 32;

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
        /// Applies the 64-point inverse DCT with thirty-two nonzero inputs to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The frequency-domain coefficients. Only the first thirty-two positions are read.</param>
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
            output.V2 = input.V16;
            output.V4 = input.V8;
            output.V6 = input.V24;
            output.V8 = input.V4;
            output.V10 = input.V20;
            output.V12 = input.V12;
            output.V14 = input.V28;
            output.V16 = input.V2;
            output.V18 = input.V18;
            output.V20 = input.V10;
            output.V22 = input.V26;
            output.V24 = input.V6;
            output.V26 = input.V22;
            output.V28 = input.V14;
            output.V30 = input.V30;
            output.V32 = input.V1;
            output.V34 = input.V17;
            output.V36 = input.V9;
            output.V38 = input.V25;
            output.V40 = input.V5;
            output.V42 = input.V21;
            output.V44 = input.V13;
            output.V46 = input.V29;
            output.V48 = input.V3;
            output.V50 = input.V19;
            output.V52 = input.V11;
            output.V54 = input.V27;
            output.V56 = input.V7;
            output.V58 = input.V23;
            output.V60 = input.V15;
            output.V62 = input.V31;

            // Stage 2 rotates the mirrored pairs in positions 32 to 63 by odd multiples of pi/128. One input of each rotation is zero, so each output is one
            // rounded product.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = output.V8;
            step.V10 = output.V10;
            step.V12 = output.V12;
            step.V14 = output.V14;
            step.V16 = output.V16;
            step.V18 = output.V18;
            step.V20 = output.V20;
            step.V22 = output.V22;
            step.V24 = output.V24;
            step.V26 = output.V26;
            step.V28 = output.V28;
            step.V30 = output.V30;
            step.V32 = TLaneOperator.MultiplyRound(output.V32, cospi[63], cosBit);
            step.V33 = TLaneOperator.MultiplyRound(output.V62, -cospi[33], cosBit);
            step.V34 = TLaneOperator.MultiplyRound(output.V34, cospi[47], cosBit);
            step.V35 = TLaneOperator.MultiplyRound(output.V60, -cospi[49], cosBit);
            step.V36 = TLaneOperator.MultiplyRound(output.V36, cospi[55], cosBit);
            step.V37 = TLaneOperator.MultiplyRound(output.V58, -cospi[41], cosBit);
            step.V38 = TLaneOperator.MultiplyRound(output.V38, cospi[39], cosBit);
            step.V39 = TLaneOperator.MultiplyRound(output.V56, -cospi[57], cosBit);
            step.V40 = TLaneOperator.MultiplyRound(output.V40, cospi[59], cosBit);
            step.V41 = TLaneOperator.MultiplyRound(output.V54, -cospi[37], cosBit);
            step.V42 = TLaneOperator.MultiplyRound(output.V42, cospi[43], cosBit);
            step.V43 = TLaneOperator.MultiplyRound(output.V52, -cospi[53], cosBit);
            step.V44 = TLaneOperator.MultiplyRound(output.V44, cospi[51], cosBit);
            step.V45 = TLaneOperator.MultiplyRound(output.V50, -cospi[45], cosBit);
            step.V46 = TLaneOperator.MultiplyRound(output.V46, cospi[35], cosBit);
            step.V47 = TLaneOperator.MultiplyRound(output.V48, -cospi[61], cosBit);
            step.V48 = TLaneOperator.MultiplyRound(output.V48, cospi[3], cosBit);
            step.V49 = TLaneOperator.MultiplyRound(output.V46, cospi[29], cosBit);
            step.V50 = TLaneOperator.MultiplyRound(output.V50, cospi[19], cosBit);
            step.V51 = TLaneOperator.MultiplyRound(output.V44, cospi[13], cosBit);
            step.V52 = TLaneOperator.MultiplyRound(output.V52, cospi[11], cosBit);
            step.V53 = TLaneOperator.MultiplyRound(output.V42, cospi[21], cosBit);
            step.V54 = TLaneOperator.MultiplyRound(output.V54, cospi[27], cosBit);
            step.V55 = TLaneOperator.MultiplyRound(output.V40, cospi[5], cosBit);
            step.V56 = TLaneOperator.MultiplyRound(output.V56, cospi[7], cosBit);
            step.V57 = TLaneOperator.MultiplyRound(output.V38, cospi[25], cosBit);
            step.V58 = TLaneOperator.MultiplyRound(output.V58, cospi[23], cosBit);
            step.V59 = TLaneOperator.MultiplyRound(output.V36, cospi[9], cosBit);
            step.V60 = TLaneOperator.MultiplyRound(output.V60, cospi[15], cosBit);
            step.V61 = TLaneOperator.MultiplyRound(output.V34, cospi[17], cosBit);
            step.V62 = TLaneOperator.MultiplyRound(output.V62, cospi[31], cosBit);
            step.V63 = TLaneOperator.MultiplyRound(output.V32, cospi[1], cosBit);

            Stages3To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 3 to 11 of the 64-point inverse DCT with thirty-two nonzero inputs to every lane.
        /// </summary>
        /// <remarks>
        /// Each method of this stage chain holds two stages, so its lane arithmetic stays inside the JIT inlining budget.
        /// </remarks>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 2 values on entry and the even stages after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        private static void Stages3To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 3 rotates the mirrored pairs in positions 16 to 31 by odd multiples of pi/64. It adds and subtracts adjacent pairs in positions 32 to 63.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = step.V4;
            output.V6 = step.V6;
            output.V8 = step.V8;
            output.V10 = step.V10;
            output.V12 = step.V12;
            output.V14 = step.V14;
            output.V16 = TLaneOperator.MultiplyRound(step.V16, cospi[62], cosBit);
            output.V17 = TLaneOperator.MultiplyRound(step.V30, -cospi[34], cosBit);
            output.V18 = TLaneOperator.MultiplyRound(step.V18, cospi[46], cosBit);
            output.V19 = TLaneOperator.MultiplyRound(step.V28, -cospi[50], cosBit);
            output.V20 = TLaneOperator.MultiplyRound(step.V20, cospi[54], cosBit);
            output.V21 = TLaneOperator.MultiplyRound(step.V26, -cospi[42], cosBit);
            output.V22 = TLaneOperator.MultiplyRound(step.V22, cospi[38], cosBit);
            output.V23 = TLaneOperator.MultiplyRound(step.V24, -cospi[58], cosBit);
            output.V24 = TLaneOperator.MultiplyRound(step.V24, cospi[6], cosBit);
            output.V25 = TLaneOperator.MultiplyRound(step.V22, cospi[26], cosBit);
            output.V26 = TLaneOperator.MultiplyRound(step.V26, cospi[22], cosBit);
            output.V27 = TLaneOperator.MultiplyRound(step.V20, cospi[10], cosBit);
            output.V28 = TLaneOperator.MultiplyRound(step.V28, cospi[14], cosBit);
            output.V29 = TLaneOperator.MultiplyRound(step.V18, cospi[18], cosBit);
            output.V30 = TLaneOperator.MultiplyRound(step.V30, cospi[30], cosBit);
            output.V31 = TLaneOperator.MultiplyRound(step.V16, cospi[2], cosBit);
            output.V32 = TLaneOperator.AddClamp(step.V32, step.V33, range);
            output.V33 = TLaneOperator.SubtractClamp(step.V32, step.V33, range);
            output.V34 = TLaneOperator.SubtractClamp(step.V35, step.V34, range);
            output.V35 = TLaneOperator.AddClamp(step.V34, step.V35, range);
            output.V36 = TLaneOperator.AddClamp(step.V36, step.V37, range);
            output.V37 = TLaneOperator.SubtractClamp(step.V36, step.V37, range);
            output.V38 = TLaneOperator.SubtractClamp(step.V39, step.V38, range);
            output.V39 = TLaneOperator.AddClamp(step.V38, step.V39, range);
            output.V40 = TLaneOperator.AddClamp(step.V40, step.V41, range);
            output.V41 = TLaneOperator.SubtractClamp(step.V40, step.V41, range);
            output.V42 = TLaneOperator.SubtractClamp(step.V43, step.V42, range);
            output.V43 = TLaneOperator.AddClamp(step.V42, step.V43, range);
            output.V44 = TLaneOperator.AddClamp(step.V44, step.V45, range);
            output.V45 = TLaneOperator.SubtractClamp(step.V44, step.V45, range);
            output.V46 = TLaneOperator.SubtractClamp(step.V47, step.V46, range);
            output.V47 = TLaneOperator.AddClamp(step.V46, step.V47, range);
            output.V48 = TLaneOperator.AddClamp(step.V48, step.V49, range);
            output.V49 = TLaneOperator.SubtractClamp(step.V48, step.V49, range);
            output.V50 = TLaneOperator.SubtractClamp(step.V51, step.V50, range);
            output.V51 = TLaneOperator.AddClamp(step.V50, step.V51, range);
            output.V52 = TLaneOperator.AddClamp(step.V52, step.V53, range);
            output.V53 = TLaneOperator.SubtractClamp(step.V52, step.V53, range);
            output.V54 = TLaneOperator.SubtractClamp(step.V55, step.V54, range);
            output.V55 = TLaneOperator.AddClamp(step.V54, step.V55, range);
            output.V56 = TLaneOperator.AddClamp(step.V56, step.V57, range);
            output.V57 = TLaneOperator.SubtractClamp(step.V56, step.V57, range);
            output.V58 = TLaneOperator.SubtractClamp(step.V59, step.V58, range);
            output.V59 = TLaneOperator.AddClamp(step.V58, step.V59, range);
            output.V60 = TLaneOperator.AddClamp(step.V60, step.V61, range);
            output.V61 = TLaneOperator.SubtractClamp(step.V60, step.V61, range);
            output.V62 = TLaneOperator.SubtractClamp(step.V63, step.V62, range);
            output.V63 = TLaneOperator.AddClamp(step.V62, step.V63, range);

            // Stage 4 rotates the mirrored pairs in positions 8 to 15 and selected pairs in positions 32 to 63 by odd multiples of pi/32. It adds and subtracts
            // adjacent pairs in positions 16 to 31.
            range = stageRange[4];
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
            step.V16 = TLaneOperator.AddClamp(output.V16, output.V17, range);
            step.V17 = TLaneOperator.SubtractClamp(output.V16, output.V17, range);
            step.V18 = TLaneOperator.SubtractClamp(output.V19, output.V18, range);
            step.V19 = TLaneOperator.AddClamp(output.V18, output.V19, range);
            step.V20 = TLaneOperator.AddClamp(output.V20, output.V21, range);
            step.V21 = TLaneOperator.SubtractClamp(output.V20, output.V21, range);
            step.V22 = TLaneOperator.SubtractClamp(output.V23, output.V22, range);
            step.V23 = TLaneOperator.AddClamp(output.V22, output.V23, range);
            step.V24 = TLaneOperator.AddClamp(output.V24, output.V25, range);
            step.V25 = TLaneOperator.SubtractClamp(output.V24, output.V25, range);
            step.V26 = TLaneOperator.SubtractClamp(output.V27, output.V26, range);
            step.V27 = TLaneOperator.AddClamp(output.V26, output.V27, range);
            step.V28 = TLaneOperator.AddClamp(output.V28, output.V29, range);
            step.V29 = TLaneOperator.SubtractClamp(output.V28, output.V29, range);
            step.V30 = TLaneOperator.SubtractClamp(output.V31, output.V30, range);
            step.V31 = TLaneOperator.AddClamp(output.V30, output.V31, range);
            step.V32 = output.V32;
            step.V33 = TLaneOperator.HalfButterfly(-cospi[4], output.V33, cospi[60], output.V62, cosBit);
            step.V34 = TLaneOperator.HalfButterfly(-cospi[60], output.V34, -cospi[4], output.V61, cosBit);
            step.V35 = output.V35;
            step.V36 = output.V36;
            step.V37 = TLaneOperator.HalfButterfly(-cospi[36], output.V37, cospi[28], output.V58, cosBit);
            step.V38 = TLaneOperator.HalfButterfly(-cospi[28], output.V38, -cospi[36], output.V57, cosBit);
            step.V39 = output.V39;
            step.V40 = output.V40;
            step.V41 = TLaneOperator.HalfButterfly(-cospi[20], output.V41, cospi[44], output.V54, cosBit);
            step.V42 = TLaneOperator.HalfButterfly(-cospi[44], output.V42, -cospi[20], output.V53, cosBit);
            step.V43 = output.V43;
            step.V44 = output.V44;
            step.V45 = TLaneOperator.HalfButterfly(-cospi[52], output.V45, cospi[12], output.V50, cosBit);
            step.V46 = TLaneOperator.HalfButterfly(-cospi[12], output.V46, -cospi[52], output.V49, cosBit);
            step.V47 = output.V47;
            step.V48 = output.V48;
            step.V49 = TLaneOperator.HalfButterfly(-cospi[52], output.V46, cospi[12], output.V49, cosBit);
            step.V50 = TLaneOperator.HalfButterfly(cospi[12], output.V45, cospi[52], output.V50, cosBit);
            step.V51 = output.V51;
            step.V52 = output.V52;
            step.V53 = TLaneOperator.HalfButterfly(-cospi[20], output.V42, cospi[44], output.V53, cosBit);
            step.V54 = TLaneOperator.HalfButterfly(cospi[44], output.V41, cospi[20], output.V54, cosBit);
            step.V55 = output.V55;
            step.V56 = output.V56;
            step.V57 = TLaneOperator.HalfButterfly(-cospi[36], output.V38, cospi[28], output.V57, cosBit);
            step.V58 = TLaneOperator.HalfButterfly(cospi[28], output.V37, cospi[36], output.V58, cosBit);
            step.V59 = output.V59;
            step.V60 = output.V60;
            step.V61 = TLaneOperator.HalfButterfly(-cospi[4], output.V34, cospi[60], output.V61, cosBit);
            step.V62 = TLaneOperator.HalfButterfly(cospi[60], output.V33, cospi[4], output.V62, cosBit);
            step.V63 = output.V63;

            Stages5To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 5 to 11 of the 64-point inverse DCT with thirty-two nonzero inputs to every lane.
        /// </summary>
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
            // Stage 5 rotates the mirrored pairs in positions 4 to 7 and selected pairs in positions 16 to 31 by odd multiples of pi/16. It adds and subtracts
            // adjacent pairs in positions 8 to 15 and mirrored pairs within each group of four in positions 32 to 63.
            byte range = stageRange[5];
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
            output.V16 = step.V16;
            output.V17 = TLaneOperator.HalfButterfly(-cospi[8], step.V17, cospi[56], step.V30, cosBit);
            output.V18 = TLaneOperator.HalfButterfly(-cospi[56], step.V18, -cospi[8], step.V29, cosBit);
            output.V19 = step.V19;
            output.V20 = step.V20;
            output.V21 = TLaneOperator.HalfButterfly(-cospi[40], step.V21, cospi[24], step.V26, cosBit);
            output.V22 = TLaneOperator.HalfButterfly(-cospi[24], step.V22, -cospi[40], step.V25, cosBit);
            output.V23 = step.V23;
            output.V24 = step.V24;
            output.V25 = TLaneOperator.HalfButterfly(-cospi[40], step.V22, cospi[24], step.V25, cosBit);
            output.V26 = TLaneOperator.HalfButterfly(cospi[24], step.V21, cospi[40], step.V26, cosBit);
            output.V27 = step.V27;
            output.V28 = step.V28;
            output.V29 = TLaneOperator.HalfButterfly(-cospi[8], step.V18, cospi[56], step.V29, cosBit);
            output.V30 = TLaneOperator.HalfButterfly(cospi[56], step.V17, cospi[8], step.V30, cosBit);
            output.V31 = step.V31;
            output.V32 = TLaneOperator.AddClamp(step.V32, step.V35, range);
            output.V33 = TLaneOperator.AddClamp(step.V33, step.V34, range);
            output.V34 = TLaneOperator.SubtractClamp(step.V33, step.V34, range);
            output.V35 = TLaneOperator.SubtractClamp(step.V32, step.V35, range);
            output.V36 = TLaneOperator.SubtractClamp(step.V39, step.V36, range);
            output.V37 = TLaneOperator.SubtractClamp(step.V38, step.V37, range);
            output.V38 = TLaneOperator.AddClamp(step.V37, step.V38, range);
            output.V39 = TLaneOperator.AddClamp(step.V36, step.V39, range);
            output.V40 = TLaneOperator.AddClamp(step.V40, step.V43, range);
            output.V41 = TLaneOperator.AddClamp(step.V41, step.V42, range);
            output.V42 = TLaneOperator.SubtractClamp(step.V41, step.V42, range);
            output.V43 = TLaneOperator.SubtractClamp(step.V40, step.V43, range);
            output.V44 = TLaneOperator.SubtractClamp(step.V47, step.V44, range);
            output.V45 = TLaneOperator.SubtractClamp(step.V46, step.V45, range);
            output.V46 = TLaneOperator.AddClamp(step.V45, step.V46, range);
            output.V47 = TLaneOperator.AddClamp(step.V44, step.V47, range);
            output.V48 = TLaneOperator.AddClamp(step.V48, step.V51, range);
            output.V49 = TLaneOperator.AddClamp(step.V49, step.V50, range);
            output.V50 = TLaneOperator.SubtractClamp(step.V49, step.V50, range);
            output.V51 = TLaneOperator.SubtractClamp(step.V48, step.V51, range);
            output.V52 = TLaneOperator.SubtractClamp(step.V55, step.V52, range);
            output.V53 = TLaneOperator.SubtractClamp(step.V54, step.V53, range);
            output.V54 = TLaneOperator.AddClamp(step.V53, step.V54, range);
            output.V55 = TLaneOperator.AddClamp(step.V52, step.V55, range);
            output.V56 = TLaneOperator.AddClamp(step.V56, step.V59, range);
            output.V57 = TLaneOperator.AddClamp(step.V57, step.V58, range);
            output.V58 = TLaneOperator.SubtractClamp(step.V57, step.V58, range);
            output.V59 = TLaneOperator.SubtractClamp(step.V56, step.V59, range);
            output.V60 = TLaneOperator.SubtractClamp(step.V63, step.V60, range);
            output.V61 = TLaneOperator.SubtractClamp(step.V62, step.V61, range);
            output.V62 = TLaneOperator.AddClamp(step.V61, step.V62, range);
            output.V63 = TLaneOperator.AddClamp(step.V60, step.V63, range);

            // Stage 6 rotates positions 0 and 1 by pi/4. It rotates positions 2 and 3 and selected pairs in positions 8 to 15 by odd multiples of pi/8. It
            // rotates selected pairs in positions 32 to 63 by odd multiples of pi/16. It adds and subtracts adjacent pairs in positions 4 to 7 and mirrored
            // pairs within each group of four in positions 16 to 31. Positions 1 and 3 of stage 5 are zero.
            range = stageRange[6];
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
            step.V16 = TLaneOperator.AddClamp(output.V16, output.V19, range);
            step.V17 = TLaneOperator.AddClamp(output.V17, output.V18, range);
            step.V18 = TLaneOperator.SubtractClamp(output.V17, output.V18, range);
            step.V19 = TLaneOperator.SubtractClamp(output.V16, output.V19, range);
            step.V20 = TLaneOperator.SubtractClamp(output.V23, output.V20, range);
            step.V21 = TLaneOperator.SubtractClamp(output.V22, output.V21, range);
            step.V22 = TLaneOperator.AddClamp(output.V21, output.V22, range);
            step.V23 = TLaneOperator.AddClamp(output.V20, output.V23, range);
            step.V24 = TLaneOperator.AddClamp(output.V24, output.V27, range);
            step.V25 = TLaneOperator.AddClamp(output.V25, output.V26, range);
            step.V26 = TLaneOperator.SubtractClamp(output.V25, output.V26, range);
            step.V27 = TLaneOperator.SubtractClamp(output.V24, output.V27, range);
            step.V28 = TLaneOperator.SubtractClamp(output.V31, output.V28, range);
            step.V29 = TLaneOperator.SubtractClamp(output.V30, output.V29, range);
            step.V30 = TLaneOperator.AddClamp(output.V29, output.V30, range);
            step.V31 = TLaneOperator.AddClamp(output.V28, output.V31, range);
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

            // Every stage 6 position is now set, so the remaining stages are those of the complete transform.
            Dct64Operator.Stages7To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
