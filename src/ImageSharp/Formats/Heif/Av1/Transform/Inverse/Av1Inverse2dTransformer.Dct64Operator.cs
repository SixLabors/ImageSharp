// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 64-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions. Vector lanes represent independent axes. The scalar and SIMD overloads run one stage network with the lane
/// arithmetic of their width. The network never mixes axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct64Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 64;

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
        /// Applies stages 7 to 11 of the 64-point inverse DCT to every lane. The 32-input sparse operator shares these stages.
        /// </summary>
        /// <remarks>
        /// Each method of this stage chain holds one or two stages and calls the method for the next stage. Each method then keeps all of its lane arithmetic
        /// inside the JIT inlining budget, and a sparse operator can join the chain at the first stage it shares.
        /// </remarks>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives the odd stages and, on return, the 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 6 values on entry and the even stages after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages7To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 7 rotates positions 5 and 6 by pi/4 and selected pairs in positions 16 to 31 by odd multiples of pi/8. It adds and subtracts mirrored pairs
            // within each group of four in positions 0 to 3 and 8 to 15, and within each group of eight in positions 32 to 63.
            byte range = stageRange[7];
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

            Stages8To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 8 to 11 of the 64-point inverse DCT to every lane. The 16-input and 32-input sparse operators share these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage 7 values on entry and the 64 spatial-domain residual values on return.</param>
        /// <param name="step">The stage buffer that receives the even stages.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages8To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 8 rotates positions 10 to 13 by pi/4 and selected pairs in positions 32 to 63 by odd multiples of pi/8. It adds and subtracts mirrored
            // pairs within each group of eight in positions 0 to 7 and 16 to 31.
            byte range = stageRange[8];
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

            Stages9To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 9 to 11 of the 64-point inverse DCT to every lane. Every multi-input sparse operator shares these stages.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage buffer that receives stage 9 and, on return, the 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 8 values on entry and the stage 10 values after that.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        internal static void Stages9To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 9 rotates positions 20 to 27 by pi/4. It adds and subtracts mirrored pairs within each group of sixteen in positions 0 to 15 and 32 to 63.
            byte range = stageRange[9];
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
            output.V16 = step.V16;
            output.V17 = step.V17;
            output.V18 = step.V18;
            output.V19 = step.V19;
            output.V20 = TLaneOperator.HalfButterfly(-cospi[32], step.V20, cospi[32], step.V27, cosBit);
            output.V21 = TLaneOperator.HalfButterfly(-cospi[32], step.V21, cospi[32], step.V26, cosBit);
            output.V22 = TLaneOperator.HalfButterfly(-cospi[32], step.V22, cospi[32], step.V25, cosBit);
            output.V23 = TLaneOperator.HalfButterfly(-cospi[32], step.V23, cospi[32], step.V24, cosBit);
            output.V24 = TLaneOperator.HalfButterfly(cospi[32], step.V23, cospi[32], step.V24, cosBit);
            output.V25 = TLaneOperator.HalfButterfly(cospi[32], step.V22, cospi[32], step.V25, cosBit);
            output.V26 = TLaneOperator.HalfButterfly(cospi[32], step.V21, cospi[32], step.V26, cosBit);
            output.V27 = TLaneOperator.HalfButterfly(cospi[32], step.V20, cospi[32], step.V27, cosBit);
            output.V28 = step.V28;
            output.V29 = step.V29;
            output.V30 = step.V30;
            output.V31 = step.V31;
            output.V32 = TLaneOperator.AddClamp(step.V32, step.V47, range);
            output.V33 = TLaneOperator.AddClamp(step.V33, step.V46, range);
            output.V34 = TLaneOperator.AddClamp(step.V34, step.V45, range);
            output.V35 = TLaneOperator.AddClamp(step.V35, step.V44, range);
            output.V36 = TLaneOperator.AddClamp(step.V36, step.V43, range);
            output.V37 = TLaneOperator.AddClamp(step.V37, step.V42, range);
            output.V38 = TLaneOperator.AddClamp(step.V38, step.V41, range);
            output.V39 = TLaneOperator.AddClamp(step.V39, step.V40, range);
            output.V40 = TLaneOperator.SubtractClamp(step.V39, step.V40, range);
            output.V41 = TLaneOperator.SubtractClamp(step.V38, step.V41, range);
            output.V42 = TLaneOperator.SubtractClamp(step.V37, step.V42, range);
            output.V43 = TLaneOperator.SubtractClamp(step.V36, step.V43, range);
            output.V44 = TLaneOperator.SubtractClamp(step.V35, step.V44, range);
            output.V45 = TLaneOperator.SubtractClamp(step.V34, step.V45, range);
            output.V46 = TLaneOperator.SubtractClamp(step.V33, step.V46, range);
            output.V47 = TLaneOperator.SubtractClamp(step.V32, step.V47, range);
            output.V48 = TLaneOperator.SubtractClamp(step.V63, step.V48, range);
            output.V49 = TLaneOperator.SubtractClamp(step.V62, step.V49, range);
            output.V50 = TLaneOperator.SubtractClamp(step.V61, step.V50, range);
            output.V51 = TLaneOperator.SubtractClamp(step.V60, step.V51, range);
            output.V52 = TLaneOperator.SubtractClamp(step.V59, step.V52, range);
            output.V53 = TLaneOperator.SubtractClamp(step.V58, step.V53, range);
            output.V54 = TLaneOperator.SubtractClamp(step.V57, step.V54, range);
            output.V55 = TLaneOperator.SubtractClamp(step.V56, step.V55, range);
            output.V56 = TLaneOperator.AddClamp(step.V55, step.V56, range);
            output.V57 = TLaneOperator.AddClamp(step.V54, step.V57, range);
            output.V58 = TLaneOperator.AddClamp(step.V53, step.V58, range);
            output.V59 = TLaneOperator.AddClamp(step.V52, step.V59, range);
            output.V60 = TLaneOperator.AddClamp(step.V51, step.V60, range);
            output.V61 = TLaneOperator.AddClamp(step.V50, step.V61, range);
            output.V62 = TLaneOperator.AddClamp(step.V49, step.V62, range);
            output.V63 = TLaneOperator.AddClamp(step.V48, step.V63, range);

            Stages10To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies the normative 64-point AV1 inverse discrete cosine transform to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="input">The 64 frequency-domain coefficients.</param>
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

            // Stage 1 permutes the coefficients from frequency order into the input order of the recursive butterfly network. Each later stage changes only the
            // positions that its comment names. The other positions pass through. Every stage clamps its sums and differences to the range of that stage.
            output.V0 = input.V0;
            output.V1 = input.V32;
            output.V2 = input.V16;
            output.V3 = input.V48;
            output.V4 = input.V8;
            output.V5 = input.V40;
            output.V6 = input.V24;
            output.V7 = input.V56;
            output.V8 = input.V4;
            output.V9 = input.V36;
            output.V10 = input.V20;
            output.V11 = input.V52;
            output.V12 = input.V12;
            output.V13 = input.V44;
            output.V14 = input.V28;
            output.V15 = input.V60;
            output.V16 = input.V2;
            output.V17 = input.V34;
            output.V18 = input.V18;
            output.V19 = input.V50;
            output.V20 = input.V10;
            output.V21 = input.V42;
            output.V22 = input.V26;
            output.V23 = input.V58;
            output.V24 = input.V6;
            output.V25 = input.V38;
            output.V26 = input.V22;
            output.V27 = input.V54;
            output.V28 = input.V14;
            output.V29 = input.V46;
            output.V30 = input.V30;
            output.V31 = input.V62;
            output.V32 = input.V1;
            output.V33 = input.V33;
            output.V34 = input.V17;
            output.V35 = input.V49;
            output.V36 = input.V9;
            output.V37 = input.V41;
            output.V38 = input.V25;
            output.V39 = input.V57;
            output.V40 = input.V5;
            output.V41 = input.V37;
            output.V42 = input.V21;
            output.V43 = input.V53;
            output.V44 = input.V13;
            output.V45 = input.V45;
            output.V46 = input.V29;
            output.V47 = input.V61;
            output.V48 = input.V3;
            output.V49 = input.V35;
            output.V50 = input.V19;
            output.V51 = input.V51;
            output.V52 = input.V11;
            output.V53 = input.V43;
            output.V54 = input.V27;
            output.V55 = input.V59;
            output.V56 = input.V7;
            output.V57 = input.V39;
            output.V58 = input.V23;
            output.V59 = input.V55;
            output.V60 = input.V15;
            output.V61 = input.V47;
            output.V62 = input.V31;
            output.V63 = input.V63;

            // Stage 2 rotates the mirrored pairs in positions 32 to 63 by odd multiples of pi/128.
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
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = output.V18;
            step.V19 = output.V19;
            step.V20 = output.V20;
            step.V21 = output.V21;
            step.V22 = output.V22;
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = output.V25;
            step.V26 = output.V26;
            step.V27 = output.V27;
            step.V28 = output.V28;
            step.V29 = output.V29;
            step.V30 = output.V30;
            step.V31 = output.V31;
            step.V32 = TLaneOperator.HalfButterfly(cospi[63], output.V32, -cospi[1], output.V63, cosBit);
            step.V33 = TLaneOperator.HalfButterfly(cospi[31], output.V33, -cospi[33], output.V62, cosBit);
            step.V34 = TLaneOperator.HalfButterfly(cospi[47], output.V34, -cospi[17], output.V61, cosBit);
            step.V35 = TLaneOperator.HalfButterfly(cospi[15], output.V35, -cospi[49], output.V60, cosBit);
            step.V36 = TLaneOperator.HalfButterfly(cospi[55], output.V36, -cospi[9], output.V59, cosBit);
            step.V37 = TLaneOperator.HalfButterfly(cospi[23], output.V37, -cospi[41], output.V58, cosBit);
            step.V38 = TLaneOperator.HalfButterfly(cospi[39], output.V38, -cospi[25], output.V57, cosBit);
            step.V39 = TLaneOperator.HalfButterfly(cospi[7], output.V39, -cospi[57], output.V56, cosBit);
            step.V40 = TLaneOperator.HalfButterfly(cospi[59], output.V40, -cospi[5], output.V55, cosBit);
            step.V41 = TLaneOperator.HalfButterfly(cospi[27], output.V41, -cospi[37], output.V54, cosBit);
            step.V42 = TLaneOperator.HalfButterfly(cospi[43], output.V42, -cospi[21], output.V53, cosBit);
            step.V43 = TLaneOperator.HalfButterfly(cospi[11], output.V43, -cospi[53], output.V52, cosBit);
            step.V44 = TLaneOperator.HalfButterfly(cospi[51], output.V44, -cospi[13], output.V51, cosBit);
            step.V45 = TLaneOperator.HalfButterfly(cospi[19], output.V45, -cospi[45], output.V50, cosBit);
            step.V46 = TLaneOperator.HalfButterfly(cospi[35], output.V46, -cospi[29], output.V49, cosBit);
            step.V47 = TLaneOperator.HalfButterfly(cospi[3], output.V47, -cospi[61], output.V48, cosBit);
            step.V48 = TLaneOperator.HalfButterfly(cospi[61], output.V47, cospi[3], output.V48, cosBit);
            step.V49 = TLaneOperator.HalfButterfly(cospi[29], output.V46, cospi[35], output.V49, cosBit);
            step.V50 = TLaneOperator.HalfButterfly(cospi[45], output.V45, cospi[19], output.V50, cosBit);
            step.V51 = TLaneOperator.HalfButterfly(cospi[13], output.V44, cospi[51], output.V51, cosBit);
            step.V52 = TLaneOperator.HalfButterfly(cospi[53], output.V43, cospi[11], output.V52, cosBit);
            step.V53 = TLaneOperator.HalfButterfly(cospi[21], output.V42, cospi[43], output.V53, cosBit);
            step.V54 = TLaneOperator.HalfButterfly(cospi[37], output.V41, cospi[27], output.V54, cosBit);
            step.V55 = TLaneOperator.HalfButterfly(cospi[5], output.V40, cospi[59], output.V55, cosBit);
            step.V56 = TLaneOperator.HalfButterfly(cospi[57], output.V39, cospi[7], output.V56, cosBit);
            step.V57 = TLaneOperator.HalfButterfly(cospi[25], output.V38, cospi[39], output.V57, cosBit);
            step.V58 = TLaneOperator.HalfButterfly(cospi[41], output.V37, cospi[23], output.V58, cosBit);
            step.V59 = TLaneOperator.HalfButterfly(cospi[9], output.V36, cospi[55], output.V59, cosBit);
            step.V60 = TLaneOperator.HalfButterfly(cospi[49], output.V35, cospi[15], output.V60, cosBit);
            step.V61 = TLaneOperator.HalfButterfly(cospi[17], output.V34, cospi[47], output.V61, cosBit);
            step.V62 = TLaneOperator.HalfButterfly(cospi[33], output.V33, cospi[31], output.V62, cosBit);
            step.V63 = TLaneOperator.HalfButterfly(cospi[1], output.V32, cospi[63], output.V63, cosBit);

            Stages3To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 3 to 11 of the 64-point inverse DCT to every lane.
        /// </summary>
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
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = step.V4;
            output.V5 = step.V5;
            output.V6 = step.V6;
            output.V7 = step.V7;
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = step.V10;
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = step.V13;
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = TLaneOperator.HalfButterfly(cospi[62], step.V16, -cospi[2], step.V31, cosBit);
            output.V17 = TLaneOperator.HalfButterfly(cospi[30], step.V17, -cospi[34], step.V30, cosBit);
            output.V18 = TLaneOperator.HalfButterfly(cospi[46], step.V18, -cospi[18], step.V29, cosBit);
            output.V19 = TLaneOperator.HalfButterfly(cospi[14], step.V19, -cospi[50], step.V28, cosBit);
            output.V20 = TLaneOperator.HalfButterfly(cospi[54], step.V20, -cospi[10], step.V27, cosBit);
            output.V21 = TLaneOperator.HalfButterfly(cospi[22], step.V21, -cospi[42], step.V26, cosBit);
            output.V22 = TLaneOperator.HalfButterfly(cospi[38], step.V22, -cospi[26], step.V25, cosBit);
            output.V23 = TLaneOperator.HalfButterfly(cospi[6], step.V23, -cospi[58], step.V24, cosBit);
            output.V24 = TLaneOperator.HalfButterfly(cospi[58], step.V23, cospi[6], step.V24, cosBit);
            output.V25 = TLaneOperator.HalfButterfly(cospi[26], step.V22, cospi[38], step.V25, cosBit);
            output.V26 = TLaneOperator.HalfButterfly(cospi[42], step.V21, cospi[22], step.V26, cosBit);
            output.V27 = TLaneOperator.HalfButterfly(cospi[10], step.V20, cospi[54], step.V27, cosBit);
            output.V28 = TLaneOperator.HalfButterfly(cospi[50], step.V19, cospi[14], step.V28, cosBit);
            output.V29 = TLaneOperator.HalfButterfly(cospi[18], step.V18, cospi[46], step.V29, cosBit);
            output.V30 = TLaneOperator.HalfButterfly(cospi[34], step.V17, cospi[30], step.V30, cosBit);
            output.V31 = TLaneOperator.HalfButterfly(cospi[2], step.V16, cospi[62], step.V31, cosBit);
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
        /// Applies stages 5 to 11 of the 64-point inverse DCT to every lane.
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
            // pairs within each group of four in positions 16 to 31.
            range = stageRange[6];
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

            Stages7To11<TLanes, TLaneOperator>(ref output, ref step, cospi, cosBit, stageRange);
        }

        /// <summary>
        /// Applies stages 10 and 11 of the 64-point inverse DCT to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The stage 9 values on entry and the 64 spatial-domain residual values on return.</param>
        /// <param name="step">The stage buffer that receives the stage 10 values.</param>
        /// <param name="cospi">The cosine constants for <paramref name="cosBit"/>.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        private static void Stages10To11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            ReadOnlySpan<int> cospi,
            int cosBit,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 10 rotates positions 40 to 55 by pi/4. It adds and subtracts mirrored pairs in positions 0 to 31.
            byte range = stageRange[10];
            step.V0 = TLaneOperator.AddClamp(output.V0, output.V31, range);
            step.V1 = TLaneOperator.AddClamp(output.V1, output.V30, range);
            step.V2 = TLaneOperator.AddClamp(output.V2, output.V29, range);
            step.V3 = TLaneOperator.AddClamp(output.V3, output.V28, range);
            step.V4 = TLaneOperator.AddClamp(output.V4, output.V27, range);
            step.V5 = TLaneOperator.AddClamp(output.V5, output.V26, range);
            step.V6 = TLaneOperator.AddClamp(output.V6, output.V25, range);
            step.V7 = TLaneOperator.AddClamp(output.V7, output.V24, range);
            step.V8 = TLaneOperator.AddClamp(output.V8, output.V23, range);
            step.V9 = TLaneOperator.AddClamp(output.V9, output.V22, range);
            step.V10 = TLaneOperator.AddClamp(output.V10, output.V21, range);
            step.V11 = TLaneOperator.AddClamp(output.V11, output.V20, range);
            step.V12 = TLaneOperator.AddClamp(output.V12, output.V19, range);
            step.V13 = TLaneOperator.AddClamp(output.V13, output.V18, range);
            step.V14 = TLaneOperator.AddClamp(output.V14, output.V17, range);
            step.V15 = TLaneOperator.AddClamp(output.V15, output.V16, range);
            step.V16 = TLaneOperator.SubtractClamp(output.V15, output.V16, range);
            step.V17 = TLaneOperator.SubtractClamp(output.V14, output.V17, range);
            step.V18 = TLaneOperator.SubtractClamp(output.V13, output.V18, range);
            step.V19 = TLaneOperator.SubtractClamp(output.V12, output.V19, range);
            step.V20 = TLaneOperator.SubtractClamp(output.V11, output.V20, range);
            step.V21 = TLaneOperator.SubtractClamp(output.V10, output.V21, range);
            step.V22 = TLaneOperator.SubtractClamp(output.V9, output.V22, range);
            step.V23 = TLaneOperator.SubtractClamp(output.V8, output.V23, range);
            step.V24 = TLaneOperator.SubtractClamp(output.V7, output.V24, range);
            step.V25 = TLaneOperator.SubtractClamp(output.V6, output.V25, range);
            step.V26 = TLaneOperator.SubtractClamp(output.V5, output.V26, range);
            step.V27 = TLaneOperator.SubtractClamp(output.V4, output.V27, range);
            step.V28 = TLaneOperator.SubtractClamp(output.V3, output.V28, range);
            step.V29 = TLaneOperator.SubtractClamp(output.V2, output.V29, range);
            step.V30 = TLaneOperator.SubtractClamp(output.V1, output.V30, range);
            step.V31 = TLaneOperator.SubtractClamp(output.V0, output.V31, range);
            step.V32 = output.V32;
            step.V33 = output.V33;
            step.V34 = output.V34;
            step.V35 = output.V35;
            step.V36 = output.V36;
            step.V37 = output.V37;
            step.V38 = output.V38;
            step.V39 = output.V39;
            step.V40 = TLaneOperator.HalfButterfly(-cospi[32], output.V40, cospi[32], output.V55, cosBit);
            step.V41 = TLaneOperator.HalfButterfly(-cospi[32], output.V41, cospi[32], output.V54, cosBit);
            step.V42 = TLaneOperator.HalfButterfly(-cospi[32], output.V42, cospi[32], output.V53, cosBit);
            step.V43 = TLaneOperator.HalfButterfly(-cospi[32], output.V43, cospi[32], output.V52, cosBit);
            step.V44 = TLaneOperator.HalfButterfly(-cospi[32], output.V44, cospi[32], output.V51, cosBit);
            step.V45 = TLaneOperator.HalfButterfly(-cospi[32], output.V45, cospi[32], output.V50, cosBit);
            step.V46 = TLaneOperator.HalfButterfly(-cospi[32], output.V46, cospi[32], output.V49, cosBit);
            step.V47 = TLaneOperator.HalfButterfly(-cospi[32], output.V47, cospi[32], output.V48, cosBit);
            step.V48 = TLaneOperator.HalfButterfly(cospi[32], output.V47, cospi[32], output.V48, cosBit);
            step.V49 = TLaneOperator.HalfButterfly(cospi[32], output.V46, cospi[32], output.V49, cosBit);
            step.V50 = TLaneOperator.HalfButterfly(cospi[32], output.V45, cospi[32], output.V50, cosBit);
            step.V51 = TLaneOperator.HalfButterfly(cospi[32], output.V44, cospi[32], output.V51, cosBit);
            step.V52 = TLaneOperator.HalfButterfly(cospi[32], output.V43, cospi[32], output.V52, cosBit);
            step.V53 = TLaneOperator.HalfButterfly(cospi[32], output.V42, cospi[32], output.V53, cosBit);
            step.V54 = TLaneOperator.HalfButterfly(cospi[32], output.V41, cospi[32], output.V54, cosBit);
            step.V55 = TLaneOperator.HalfButterfly(cospi[32], output.V40, cospi[32], output.V55, cosBit);
            step.V56 = output.V56;
            step.V57 = output.V57;
            step.V58 = output.V58;
            step.V59 = output.V59;
            step.V60 = output.V60;
            step.V61 = output.V61;
            step.V62 = output.V62;
            step.V63 = output.V63;

            Stage11<TLanes, TLaneOperator>(ref output, ref step, stageRange);
        }

        /// <summary>
        /// Applies stage 11 of the 64-point inverse DCT to every lane.
        /// </summary>
        /// <typeparam name="TLanes">The lane type. Each lane holds one transform axis.</typeparam>
        /// <typeparam name="TLaneOperator">The arithmetic for <typeparamref name="TLanes"/>.</typeparam>
        /// <param name="output">The 64 spatial-domain residual values.</param>
        /// <param name="step">The stage 10 values.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        private static void Stage11<TLanes, TLaneOperator>(
            ref Av1TransformVector<TLanes> output,
            ref Av1TransformVector<TLanes> step,
            InlineArray12<byte> stageRange)
            where TLanes : unmanaged
            where TLaneOperator : struct, IAv1TransformLaneOperator<TLanes>
        {
            // Stage 11 adds and subtracts mirrored pairs across all 64 positions. It clamps each result to the final stage range and writes the output in
            // spatial order.
            byte range = stageRange[11];
            output.V0 = TLaneOperator.AddClamp(step.V0, step.V63, range);
            output.V1 = TLaneOperator.AddClamp(step.V1, step.V62, range);
            output.V2 = TLaneOperator.AddClamp(step.V2, step.V61, range);
            output.V3 = TLaneOperator.AddClamp(step.V3, step.V60, range);
            output.V4 = TLaneOperator.AddClamp(step.V4, step.V59, range);
            output.V5 = TLaneOperator.AddClamp(step.V5, step.V58, range);
            output.V6 = TLaneOperator.AddClamp(step.V6, step.V57, range);
            output.V7 = TLaneOperator.AddClamp(step.V7, step.V56, range);
            output.V8 = TLaneOperator.AddClamp(step.V8, step.V55, range);
            output.V9 = TLaneOperator.AddClamp(step.V9, step.V54, range);
            output.V10 = TLaneOperator.AddClamp(step.V10, step.V53, range);
            output.V11 = TLaneOperator.AddClamp(step.V11, step.V52, range);
            output.V12 = TLaneOperator.AddClamp(step.V12, step.V51, range);
            output.V13 = TLaneOperator.AddClamp(step.V13, step.V50, range);
            output.V14 = TLaneOperator.AddClamp(step.V14, step.V49, range);
            output.V15 = TLaneOperator.AddClamp(step.V15, step.V48, range);
            output.V16 = TLaneOperator.AddClamp(step.V16, step.V47, range);
            output.V17 = TLaneOperator.AddClamp(step.V17, step.V46, range);
            output.V18 = TLaneOperator.AddClamp(step.V18, step.V45, range);
            output.V19 = TLaneOperator.AddClamp(step.V19, step.V44, range);
            output.V20 = TLaneOperator.AddClamp(step.V20, step.V43, range);
            output.V21 = TLaneOperator.AddClamp(step.V21, step.V42, range);
            output.V22 = TLaneOperator.AddClamp(step.V22, step.V41, range);
            output.V23 = TLaneOperator.AddClamp(step.V23, step.V40, range);
            output.V24 = TLaneOperator.AddClamp(step.V24, step.V39, range);
            output.V25 = TLaneOperator.AddClamp(step.V25, step.V38, range);
            output.V26 = TLaneOperator.AddClamp(step.V26, step.V37, range);
            output.V27 = TLaneOperator.AddClamp(step.V27, step.V36, range);
            output.V28 = TLaneOperator.AddClamp(step.V28, step.V35, range);
            output.V29 = TLaneOperator.AddClamp(step.V29, step.V34, range);
            output.V30 = TLaneOperator.AddClamp(step.V30, step.V33, range);
            output.V31 = TLaneOperator.AddClamp(step.V31, step.V32, range);
            output.V32 = TLaneOperator.SubtractClamp(step.V31, step.V32, range);
            output.V33 = TLaneOperator.SubtractClamp(step.V30, step.V33, range);
            output.V34 = TLaneOperator.SubtractClamp(step.V29, step.V34, range);
            output.V35 = TLaneOperator.SubtractClamp(step.V28, step.V35, range);
            output.V36 = TLaneOperator.SubtractClamp(step.V27, step.V36, range);
            output.V37 = TLaneOperator.SubtractClamp(step.V26, step.V37, range);
            output.V38 = TLaneOperator.SubtractClamp(step.V25, step.V38, range);
            output.V39 = TLaneOperator.SubtractClamp(step.V24, step.V39, range);
            output.V40 = TLaneOperator.SubtractClamp(step.V23, step.V40, range);
            output.V41 = TLaneOperator.SubtractClamp(step.V22, step.V41, range);
            output.V42 = TLaneOperator.SubtractClamp(step.V21, step.V42, range);
            output.V43 = TLaneOperator.SubtractClamp(step.V20, step.V43, range);
            output.V44 = TLaneOperator.SubtractClamp(step.V19, step.V44, range);
            output.V45 = TLaneOperator.SubtractClamp(step.V18, step.V45, range);
            output.V46 = TLaneOperator.SubtractClamp(step.V17, step.V46, range);
            output.V47 = TLaneOperator.SubtractClamp(step.V16, step.V47, range);
            output.V48 = TLaneOperator.SubtractClamp(step.V15, step.V48, range);
            output.V49 = TLaneOperator.SubtractClamp(step.V14, step.V49, range);
            output.V50 = TLaneOperator.SubtractClamp(step.V13, step.V50, range);
            output.V51 = TLaneOperator.SubtractClamp(step.V12, step.V51, range);
            output.V52 = TLaneOperator.SubtractClamp(step.V11, step.V52, range);
            output.V53 = TLaneOperator.SubtractClamp(step.V10, step.V53, range);
            output.V54 = TLaneOperator.SubtractClamp(step.V9, step.V54, range);
            output.V55 = TLaneOperator.SubtractClamp(step.V8, step.V55, range);
            output.V56 = TLaneOperator.SubtractClamp(step.V7, step.V56, range);
            output.V57 = TLaneOperator.SubtractClamp(step.V6, step.V57, range);
            output.V58 = TLaneOperator.SubtractClamp(step.V5, step.V58, range);
            output.V59 = TLaneOperator.SubtractClamp(step.V4, step.V59, range);
            output.V60 = TLaneOperator.SubtractClamp(step.V3, step.V60, range);
            output.V61 = TLaneOperator.SubtractClamp(step.V2, step.V61, range);
            output.V62 = TLaneOperator.SubtractClamp(step.V1, step.V62, range);
            output.V63 = TLaneOperator.SubtractClamp(step.V0, step.V63, range);
        }
    }
}
