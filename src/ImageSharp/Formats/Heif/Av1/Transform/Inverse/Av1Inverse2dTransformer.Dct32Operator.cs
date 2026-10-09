// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 32-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct32Operator : IAv1Transform1dOperator
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
        /// Applies stages 6 to 9 of the 32-point inverse DCT to every lane. The sixteen-input sparse operator shares these stages.
        /// </summary>
        /// <remarks>
        /// The stage networks of this family run in separate methods. Each method then keeps all of its lane arithmetic inside the JIT inlining budget.
        /// </remarks>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage 5 values on entry and the thirty-two spatial-domain residual values on return.</param>
        /// <param name="step">The stage buffer that receives the even stages.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages6To9<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            byte range = stageRange[6];
            step.V0 = TLaneOperator.AddClamp(output.V0, output.V3, range);
            step.V1 = TLaneOperator.AddClamp(output.V1, output.V2, range);
            step.V2 = TLaneOperator.SubtractClamp(output.V1, output.V2, range);
            step.V3 = TLaneOperator.SubtractClamp(output.V0, output.V3, range);
            step.V4 = output.V4;
            step.V5 = TLaneOperator.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = TLaneOperator.AddClamp(output.V8, output.V11, range);
            step.V9 = TLaneOperator.AddClamp(output.V9, output.V10, range);
            step.V10 = TLaneOperator.SubtractClamp(output.V9, output.V10, range);
            step.V11 = TLaneOperator.SubtractClamp(output.V8, output.V11, range);
            step.V12 = TLaneOperator.SubtractClamp(output.V15, output.V12, range);
            step.V13 = TLaneOperator.SubtractClamp(output.V14, output.V13, range);
            step.V14 = TLaneOperator.AddClamp(output.V13, output.V14, range);
            step.V15 = TLaneOperator.AddClamp(output.V12, output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = TLaneOperator.HalfButterfly(-cospi[16], output.V18, cospi[48], output.V29, cosBit);
            step.V19 = TLaneOperator.HalfButterfly(-cospi[16], output.V19, cospi[48], output.V28, cosBit);
            step.V20 = TLaneOperator.HalfButterfly(-cospi[48], output.V20, -cospi[16], output.V27, cosBit);
            step.V21 = TLaneOperator.HalfButterfly(-cospi[48], output.V21, -cospi[16], output.V26, cosBit);
            step.V22 = output.V22;
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = output.V25;
            step.V26 = TLaneOperator.HalfButterfly(-cospi[16], output.V21, cospi[48], output.V26, cosBit);
            step.V27 = TLaneOperator.HalfButterfly(-cospi[16], output.V20, cospi[48], output.V27, cosBit);
            step.V28 = TLaneOperator.HalfButterfly(cospi[48], output.V19, cospi[16], output.V28, cosBit);
            step.V29 = TLaneOperator.HalfButterfly(cospi[48], output.V18, cospi[16], output.V29, cosBit);
            step.V30 = output.V30;
            step.V31 = output.V31;

            Stages7To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 7 to 9 of the 32-point inverse DCT to every lane. Both multi-input sparse operators share these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the thirty-two spatial-domain residual values.</param>
        /// <param name="step">The stage 6 values on entry and the stage 8 values after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages7To9<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 7 widens the reconstructed groups through their next butterfly level.
            byte range = stageRange[7];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V7, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V6, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V5, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V4, range);
            output.V4 = TLaneOperator.SubtractClamp(step.V3, step.V4, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V2, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V1, step.V6, range);
            output.V7 = TLaneOperator.SubtractClamp(step.V0, step.V7, range);
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = TLaneOperator.HalfButterfly(-cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V11 = TLaneOperator.HalfButterfly(-cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V12 = TLaneOperator.HalfButterfly(cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V13 = TLaneOperator.HalfButterfly(cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V23, range);
            output.V17 = TLaneOperator.AddClamp(step.V17, step.V22, range);
            output.V18 = TLaneOperator.AddClamp(step.V18, step.V21, range);
            output.V19 = TLaneOperator.AddClamp(step.V19, step.V20, range);
            output.V20 = TLaneOperator.SubtractClamp(step.V19, step.V20, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V18, step.V21, range);
            output.V22 = TLaneOperator.SubtractClamp(step.V17, step.V22, range);
            output.V23 = TLaneOperator.SubtractClamp(step.V16, step.V23, range);
            output.V24 = TLaneOperator.SubtractClamp(step.V31, step.V24, range);
            output.V25 = TLaneOperator.SubtractClamp(step.V30, step.V25, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V29, step.V26, range);
            output.V27 = TLaneOperator.SubtractClamp(step.V28, step.V27, range);
            output.V28 = TLaneOperator.AddClamp(step.V27, step.V28, range);
            output.V29 = TLaneOperator.AddClamp(step.V26, step.V29, range);
            output.V30 = TLaneOperator.AddClamp(step.V25, step.V30, range);
            output.V31 = TLaneOperator.AddClamp(step.V24, step.V31, range);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            range = stageRange[8];
            step.V0 = TLaneOperator.AddClamp(output.V0, output.V15, range);
            step.V1 = TLaneOperator.AddClamp(output.V1, output.V14, range);
            step.V2 = TLaneOperator.AddClamp(output.V2, output.V13, range);
            step.V3 = TLaneOperator.AddClamp(output.V3, output.V12, range);
            step.V4 = TLaneOperator.AddClamp(output.V4, output.V11, range);
            step.V5 = TLaneOperator.AddClamp(output.V5, output.V10, range);
            step.V6 = TLaneOperator.AddClamp(output.V6, output.V9, range);
            step.V7 = TLaneOperator.AddClamp(output.V7, output.V8, range);
            step.V8 = TLaneOperator.SubtractClamp(output.V7, output.V8, range);
            step.V9 = TLaneOperator.SubtractClamp(output.V6, output.V9, range);
            step.V10 = TLaneOperator.SubtractClamp(output.V5, output.V10, range);
            step.V11 = TLaneOperator.SubtractClamp(output.V4, output.V11, range);
            step.V12 = TLaneOperator.SubtractClamp(output.V3, output.V12, range);
            step.V13 = TLaneOperator.SubtractClamp(output.V2, output.V13, range);
            step.V14 = TLaneOperator.SubtractClamp(output.V1, output.V14, range);
            step.V15 = TLaneOperator.SubtractClamp(output.V0, output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = output.V18;
            step.V19 = output.V19;
            step.V20 = TLaneOperator.HalfButterfly(-cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V21 = TLaneOperator.HalfButterfly(-cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V22 = TLaneOperator.HalfButterfly(-cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V23 = TLaneOperator.HalfButterfly(-cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V24 = TLaneOperator.HalfButterfly(cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V25 = TLaneOperator.HalfButterfly(cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V26 = TLaneOperator.HalfButterfly(cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V27 = TLaneOperator.HalfButterfly(cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V28 = output.V28;
            step.V29 = output.V29;
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Stage 9 merges the even and odd halves into spatial order and clamps every result.
            range = stageRange[9];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V31, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V30, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V29, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V28, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V27, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V26, range);
            output.V6 = TLaneOperator.AddClamp(step.V6, step.V25, range);
            output.V7 = TLaneOperator.AddClamp(step.V7, step.V24, range);
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V23, range);
            output.V9 = TLaneOperator.AddClamp(step.V9, step.V22, range);
            output.V10 = TLaneOperator.AddClamp(step.V10, step.V21, range);
            output.V11 = TLaneOperator.AddClamp(step.V11, step.V20, range);
            output.V12 = TLaneOperator.AddClamp(step.V12, step.V19, range);
            output.V13 = TLaneOperator.AddClamp(step.V13, step.V18, range);
            output.V14 = TLaneOperator.AddClamp(step.V14, step.V17, range);
            output.V15 = TLaneOperator.AddClamp(step.V15, step.V16, range);
            output.V16 = TLaneOperator.SubtractClamp(step.V15, step.V16, range);
            output.V17 = TLaneOperator.SubtractClamp(step.V14, step.V17, range);
            output.V18 = TLaneOperator.SubtractClamp(step.V13, step.V18, range);
            output.V19 = TLaneOperator.SubtractClamp(step.V12, step.V19, range);
            output.V20 = TLaneOperator.SubtractClamp(step.V11, step.V20, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V10, step.V21, range);
            output.V22 = TLaneOperator.SubtractClamp(step.V9, step.V22, range);
            output.V23 = TLaneOperator.SubtractClamp(step.V8, step.V23, range);
            output.V24 = TLaneOperator.SubtractClamp(step.V7, step.V24, range);
            output.V25 = TLaneOperator.SubtractClamp(step.V6, step.V25, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V5, step.V26, range);
            output.V27 = TLaneOperator.SubtractClamp(step.V4, step.V27, range);
            output.V28 = TLaneOperator.SubtractClamp(step.V3, step.V28, range);
            output.V29 = TLaneOperator.SubtractClamp(step.V2, step.V29, range);
            output.V30 = TLaneOperator.SubtractClamp(step.V1, step.V30, range);
            output.V31 = TLaneOperator.SubtractClamp(step.V0, step.V31, range);
        }

        /// <summary>
        /// Applies the normative 32-point AV1 inverse discrete cosine transform to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The thirty-two frequency-domain coefficients.</param>
        /// <param name="output">The thirty-two spatial-domain residual values.</param>
        /// <param name="step">The thirty-two-position stage buffer. It can alias <paramref name="input"/>.</param>
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
            output.V1 = input.V16;
            output.V2 = input.V8;
            output.V3 = input.V24;
            output.V4 = input.V4;
            output.V5 = input.V20;
            output.V6 = input.V12;
            output.V7 = input.V28;
            output.V8 = input.V2;
            output.V9 = input.V18;
            output.V10 = input.V10;
            output.V11 = input.V26;
            output.V12 = input.V6;
            output.V13 = input.V22;
            output.V14 = input.V14;
            output.V15 = input.V30;
            output.V16 = input.V1;
            output.V17 = input.V17;
            output.V18 = input.V9;
            output.V19 = input.V25;
            output.V20 = input.V5;
            output.V21 = input.V21;
            output.V22 = input.V13;
            output.V23 = input.V29;
            output.V24 = input.V3;
            output.V25 = input.V19;
            output.V26 = input.V11;
            output.V27 = input.V27;
            output.V28 = input.V7;
            output.V29 = input.V23;
            output.V30 = input.V15;
            output.V31 = input.V31;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by odd multiples of pi/64.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = output.V6;
            step.V7 = output.V7;
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = output.V10;
            step.V11 = output.V11;
            step.V12 = output.V12;
            step.V13 = output.V13;
            step.V14 = output.V14;
            step.V15 = output.V15;
            step.V16 = TLaneOperator.HalfButterfly(cospi[62], output.V16, -cospi[2], output.V31, cosBit);
            step.V17 = TLaneOperator.HalfButterfly(cospi[30], output.V17, -cospi[34], output.V30, cosBit);
            step.V18 = TLaneOperator.HalfButterfly(cospi[46], output.V18, -cospi[18], output.V29, cosBit);
            step.V19 = TLaneOperator.HalfButterfly(cospi[14], output.V19, -cospi[50], output.V28, cosBit);
            step.V20 = TLaneOperator.HalfButterfly(cospi[54], output.V20, -cospi[10], output.V27, cosBit);
            step.V21 = TLaneOperator.HalfButterfly(cospi[22], output.V21, -cospi[42], output.V26, cosBit);
            step.V22 = TLaneOperator.HalfButterfly(cospi[38], output.V22, -cospi[26], output.V25, cosBit);
            step.V23 = TLaneOperator.HalfButterfly(cospi[6], output.V23, -cospi[58], output.V24, cosBit);
            step.V24 = TLaneOperator.HalfButterfly(cospi[58], output.V23, cospi[6], output.V24, cosBit);
            step.V25 = TLaneOperator.HalfButterfly(cospi[26], output.V22, cospi[38], output.V25, cosBit);
            step.V26 = TLaneOperator.HalfButterfly(cospi[42], output.V21, cospi[22], output.V26, cosBit);
            step.V27 = TLaneOperator.HalfButterfly(cospi[10], output.V20, cospi[54], output.V27, cosBit);
            step.V28 = TLaneOperator.HalfButterfly(cospi[50], output.V19, cospi[14], output.V28, cosBit);
            step.V29 = TLaneOperator.HalfButterfly(cospi[18], output.V18, cospi[46], output.V29, cosBit);
            step.V30 = TLaneOperator.HalfButterfly(cospi[34], output.V17, cospi[30], output.V30, cosBit);
            step.V31 = TLaneOperator.HalfButterfly(cospi[2], output.V16, cospi[62], output.V31, cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            byte range = stageRange[3];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = step.V4;
            output.V5 = step.V5;
            output.V6 = step.V6;
            output.V7 = step.V7;
            output.V8 = TLaneOperator.HalfButterfly(cospi[60], step.V8, -cospi[4], step.V15, cosBit);
            output.V9 = TLaneOperator.HalfButterfly(cospi[28], step.V9, -cospi[36], step.V14, cosBit);
            output.V10 = TLaneOperator.HalfButterfly(cospi[44], step.V10, -cospi[20], step.V13, cosBit);
            output.V11 = TLaneOperator.HalfButterfly(cospi[12], step.V11, -cospi[52], step.V12, cosBit);
            output.V12 = TLaneOperator.HalfButterfly(cospi[52], step.V11, cospi[12], step.V12, cosBit);
            output.V13 = TLaneOperator.HalfButterfly(cospi[20], step.V10, cospi[44], step.V13, cosBit);
            output.V14 = TLaneOperator.HalfButterfly(cospi[36], step.V9, cospi[28], step.V14, cosBit);
            output.V15 = TLaneOperator.HalfButterfly(cospi[4], step.V8, cospi[60], step.V15, cosBit);
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V17, range);
            output.V17 = TLaneOperator.SubtractClamp(step.V16, step.V17, range);
            output.V18 = TLaneOperator.SubtractClamp(step.V19, step.V18, range);
            output.V19 = TLaneOperator.AddClamp(step.V18, step.V19, range);
            output.V20 = TLaneOperator.AddClamp(step.V20, step.V21, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V20, step.V21, range);
            output.V22 = TLaneOperator.SubtractClamp(step.V23, step.V22, range);
            output.V23 = TLaneOperator.AddClamp(step.V22, step.V23, range);
            output.V24 = TLaneOperator.AddClamp(step.V24, step.V25, range);
            output.V25 = TLaneOperator.SubtractClamp(step.V24, step.V25, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V27, step.V26, range);
            output.V27 = TLaneOperator.AddClamp(step.V26, step.V27, range);
            output.V28 = TLaneOperator.AddClamp(step.V28, step.V29, range);
            output.V29 = TLaneOperator.SubtractClamp(step.V28, step.V29, range);
            output.V30 = TLaneOperator.SubtractClamp(step.V31, step.V30, range);
            output.V31 = TLaneOperator.AddClamp(step.V30, step.V31, range);

            // Stage 4 rotates the next odd-frequency level. Positions 0 to 3 pass through unchanged.
            range = stageRange[4];
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = TLaneOperator.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = TLaneOperator.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = TLaneOperator.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = TLaneOperator.HalfButterfly(cospi[8], output.V4, cospi[56], output.V7, cosBit);
            step.V8 = TLaneOperator.AddClamp(output.V8, output.V9, range);
            step.V9 = TLaneOperator.SubtractClamp(output.V8, output.V9, range);
            step.V10 = TLaneOperator.SubtractClamp(output.V11, output.V10, range);
            step.V11 = TLaneOperator.AddClamp(output.V10, output.V11, range);
            step.V12 = TLaneOperator.AddClamp(output.V12, output.V13, range);
            step.V13 = TLaneOperator.SubtractClamp(output.V12, output.V13, range);
            step.V14 = TLaneOperator.SubtractClamp(output.V15, output.V14, range);
            step.V15 = TLaneOperator.AddClamp(output.V14, output.V15, range);
            step.V16 = output.V16;
            step.V17 = TLaneOperator.HalfButterfly(-cospi[8], output.V17, cospi[56], output.V30, cosBit);
            step.V18 = TLaneOperator.HalfButterfly(-cospi[56], output.V18, -cospi[8], output.V29, cosBit);
            step.V19 = output.V19;
            step.V20 = output.V20;
            step.V21 = TLaneOperator.HalfButterfly(-cospi[40], output.V21, cospi[24], output.V26, cosBit);
            step.V22 = TLaneOperator.HalfButterfly(-cospi[24], output.V22, -cospi[40], output.V25, cosBit);
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = TLaneOperator.HalfButterfly(-cospi[40], output.V22, cospi[24], output.V25, cosBit);
            step.V26 = TLaneOperator.HalfButterfly(cospi[24], output.V21, cospi[40], output.V26, cosBit);
            step.V27 = output.V27;
            step.V28 = output.V28;
            step.V29 = TLaneOperator.HalfButterfly(-cospi[8], output.V18, cospi[56], output.V29, cosBit);
            step.V30 = TLaneOperator.HalfButterfly(cospi[56], output.V17, cospi[8], output.V30, cosBit);
            step.V31 = output.V31;

            // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            range = stageRange[5];
            output.V0 = TLaneOperator.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = TLaneOperator.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = TLaneOperator.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = TLaneOperator.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V5, range);
            output.V5 = TLaneOperator.SubtractClamp(step.V4, step.V5, range);
            output.V6 = TLaneOperator.SubtractClamp(step.V7, step.V6, range);
            output.V7 = TLaneOperator.AddClamp(step.V6, step.V7, range);
            output.V8 = step.V8;
            output.V9 = TLaneOperator.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = TLaneOperator.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = TLaneOperator.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = TLaneOperator.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V19, range);
            output.V17 = TLaneOperator.AddClamp(step.V17, step.V18, range);
            output.V18 = TLaneOperator.SubtractClamp(step.V17, step.V18, range);
            output.V19 = TLaneOperator.SubtractClamp(step.V16, step.V19, range);
            output.V20 = TLaneOperator.SubtractClamp(step.V23, step.V20, range);
            output.V21 = TLaneOperator.SubtractClamp(step.V22, step.V21, range);
            output.V22 = TLaneOperator.AddClamp(step.V21, step.V22, range);
            output.V23 = TLaneOperator.AddClamp(step.V20, step.V23, range);
            output.V24 = TLaneOperator.AddClamp(step.V24, step.V27, range);
            output.V25 = TLaneOperator.AddClamp(step.V25, step.V26, range);
            output.V26 = TLaneOperator.SubtractClamp(step.V25, step.V26, range);
            output.V27 = TLaneOperator.SubtractClamp(step.V24, step.V27, range);
            output.V28 = TLaneOperator.SubtractClamp(step.V31, step.V28, range);
            output.V29 = TLaneOperator.SubtractClamp(step.V30, step.V29, range);
            output.V30 = TLaneOperator.AddClamp(step.V29, step.V30, range);
            output.V31 = TLaneOperator.AddClamp(step.V28, step.V31, range);

            Stages6To9<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }
    }
}
