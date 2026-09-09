// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 16-point inverse ADST with at most 1 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Adst16Low1Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 1;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
            output[1] = input[0];

            // Stage 2 applies the terminal odd-angle rotations in reverse.
            step[0] = Av1Math.RoundShift((long)output[1] * cospi[62], cosBit);
            step[1] = Av1Math.RoundShift((long)output[1] * -cospi[2], cosBit);

            // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
            output[0] = Av1Transform1dMath.Clamp(step[0], stageRange[3]);
            output[1] = Av1Transform1dMath.Clamp(step[1], stageRange[3]);
            output[8] = Av1Transform1dMath.Clamp(step[0], stageRange[3]);
            output[9] = Av1Transform1dMath.Clamp(step[1], stageRange[3]);

            // Stage 4 reverses the pi/16 rotations in the upper half.
            step[0] = output[0];
            step[1] = output[1];
            step[8] = Av1Transform1dMath.HalfButterfly(cospi[8], output[8], cospi[56], output[9], cosBit);
            step[9] = Av1Transform1dMath.HalfButterfly(cospi[56], output[8], -cospi[8], output[9], cosBit);

            // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
            output[0] = Av1Transform1dMath.Clamp(step[0], stageRange[5]);
            output[1] = Av1Transform1dMath.Clamp(step[1], stageRange[5]);
            output[4] = Av1Transform1dMath.Clamp(step[0], stageRange[5]);
            output[5] = Av1Transform1dMath.Clamp(step[1], stageRange[5]);
            output[8] = Av1Transform1dMath.Clamp(step[8], stageRange[5]);
            output[9] = Av1Transform1dMath.Clamp(step[9], stageRange[5]);
            output[12] = Av1Transform1dMath.Clamp(step[8], stageRange[5]);
            output[13] = Av1Transform1dMath.Clamp(step[9], stageRange[5]);

            // Stage 6 reverses the pi/8 and 3pi/8 rotations.
            step[0] = output[0];
            step[1] = output[1];
            step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
            step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
            step[8] = output[8];
            step[9] = output[9];
            step[12] = Av1Transform1dMath.HalfButterfly(cospi[16], output[12], cospi[48], output[13], cosBit);
            step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[12], -cospi[16], output[13], cosBit);

            // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
            output[0] = Av1Transform1dMath.Clamp(step[0], stageRange[7]);
            output[1] = Av1Transform1dMath.Clamp(step[1], stageRange[7]);
            output[2] = Av1Transform1dMath.Clamp(step[0], stageRange[7]);
            output[3] = Av1Transform1dMath.Clamp(step[1], stageRange[7]);
            output[4] = Av1Transform1dMath.Clamp(step[4], stageRange[7]);
            output[5] = Av1Transform1dMath.Clamp(step[5], stageRange[7]);
            output[6] = Av1Transform1dMath.Clamp(step[4], stageRange[7]);
            output[7] = Av1Transform1dMath.Clamp(step[5], stageRange[7]);
            output[8] = Av1Transform1dMath.Clamp(step[8], stageRange[7]);
            output[9] = Av1Transform1dMath.Clamp(step[9], stageRange[7]);
            output[10] = Av1Transform1dMath.Clamp(step[8], stageRange[7]);
            output[11] = Av1Transform1dMath.Clamp(step[9], stageRange[7]);
            output[12] = Av1Transform1dMath.Clamp(step[12], stageRange[7]);
            output[13] = Av1Transform1dMath.Clamp(step[13], stageRange[7]);
            output[14] = Av1Transform1dMath.Clamp(step[12], stageRange[7]);
            output[15] = Av1Transform1dMath.Clamp(step[13], stageRange[7]);

            // Stage 8 reverses the pi/4 rotations for the middle pairs.
            step[0] = output[0];
            step[1] = output[1];
            step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
            step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
            step[4] = output[4];
            step[5] = output[5];
            step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
            step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);
            step[8] = output[8];
            step[9] = output[9];
            step[10] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[11], cosBit);
            step[11] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], -cospi[32], output[11], cosBit);
            step[12] = output[12];
            step[13] = output[13];
            step[14] = Av1Transform1dMath.HalfButterfly(cospi[32], output[14], cospi[32], output[15], cosBit);
            step[15] = Av1Transform1dMath.HalfButterfly(cospi[32], output[14], -cospi[32], output[15], cosBit);

            // Stage 9 applies the AV1 signs and permutation that restore spatial sample order.
            output[0] = step[0];
            output[1] = -step[8];
            output[2] = step[12];
            output[3] = -step[4];
            output[4] = step[6];
            output[5] = -step[14];
            output[6] = step[10];
            output[7] = -step[2];
            output[8] = step[3];
            output[9] = -step[11];
            output[10] = step[15];
            output[11] = -step[7];
            output[12] = step[5];
            output[13] = -step[13];
            output[14] = step[9];
            output[15] = -step[1];
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
            output.V1 = input.V0;

            // Stage 2 applies the terminal odd-angle rotations in reverse.
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V1, cospi[62], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V1, -cospi[2], cosBit);

            // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);
            output.V8 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V9 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);

            // Stage 4 reverses the pi/16 rotations in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V8 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V8, cospi[56], output.V9, cosBit);
            step.V9 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V8, -cospi[8], output.V9, cosBit);

            // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V4 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8, stageRange[5]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9, stageRange[5]);
            output.V12 = Av1Transform1dMath.Clamp(step.V8, stageRange[5]);
            output.V13 = Av1Transform1dMath.Clamp(step.V9, stageRange[5]);

            // Stage 6 reverses the pi/8 and 3pi/8 rotations.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V12, cospi[48], output.V13, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V12, -cospi[16], output.V13, cosBit);

            // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V0, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V1, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V4, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V5, stageRange[7]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8, stageRange[7]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9, stageRange[7]);
            output.V10 = Av1Transform1dMath.Clamp(step.V8, stageRange[7]);
            output.V11 = Av1Transform1dMath.Clamp(step.V9, stageRange[7]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12, stageRange[7]);
            output.V13 = Av1Transform1dMath.Clamp(step.V13, stageRange[7]);
            output.V14 = Av1Transform1dMath.Clamp(step.V12, stageRange[7]);
            output.V15 = Av1Transform1dMath.Clamp(step.V13, stageRange[7]);

            // Stage 8 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V11, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, -cospi[32], output.V11, cosBit);
            step.V12 = output.V12;
            step.V13 = output.V13;
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V14, cospi[32], output.V15, cosBit);
            step.V15 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V14, -cospi[32], output.V15, cosBit);

            // Stage 9 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = -step.V8;
            output.V2 = step.V12;
            output.V3 = -step.V4;
            output.V4 = step.V6;
            output.V5 = -step.V14;
            output.V6 = step.V10;
            output.V7 = -step.V2;
            output.V8 = step.V3;
            output.V9 = -step.V11;
            output.V10 = step.V15;
            output.V11 = -step.V7;
            output.V12 = step.V5;
            output.V13 = -step.V13;
            output.V14 = step.V9;
            output.V15 = -step.V1;
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
            output.V1 = input.V0;

            // Stage 2 applies the terminal odd-angle rotations in reverse.
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V1, cospi[62], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V1, -cospi[2], cosBit);

            // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);
            output.V8 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V9 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);

            // Stage 4 reverses the pi/16 rotations in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V8 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V8, cospi[56], output.V9, cosBit);
            step.V9 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V8, -cospi[8], output.V9, cosBit);

            // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V4 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8, stageRange[5]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9, stageRange[5]);
            output.V12 = Av1Transform1dMath.Clamp(step.V8, stageRange[5]);
            output.V13 = Av1Transform1dMath.Clamp(step.V9, stageRange[5]);

            // Stage 6 reverses the pi/8 and 3pi/8 rotations.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V12 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V12, cospi[48], output.V13, cosBit);
            step.V13 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V12, -cospi[16], output.V13, cosBit);

            // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V0, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V1, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V4, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V5, stageRange[7]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8, stageRange[7]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9, stageRange[7]);
            output.V10 = Av1Transform1dMath.Clamp(step.V8, stageRange[7]);
            output.V11 = Av1Transform1dMath.Clamp(step.V9, stageRange[7]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12, stageRange[7]);
            output.V13 = Av1Transform1dMath.Clamp(step.V13, stageRange[7]);
            output.V14 = Av1Transform1dMath.Clamp(step.V12, stageRange[7]);
            output.V15 = Av1Transform1dMath.Clamp(step.V13, stageRange[7]);

            // Stage 8 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);
            step.V8 = output.V8;
            step.V9 = output.V9;
            step.V10 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, cospi[32], output.V11, cosBit);
            step.V11 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V10, -cospi[32], output.V11, cosBit);
            step.V12 = output.V12;
            step.V13 = output.V13;
            step.V14 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V14, cospi[32], output.V15, cosBit);
            step.V15 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V14, -cospi[32], output.V15, cosBit);

            // Stage 9 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = -step.V8;
            output.V2 = step.V12;
            output.V3 = -step.V4;
            output.V4 = step.V6;
            output.V5 = -step.V14;
            output.V6 = step.V10;
            output.V7 = -step.V2;
            output.V8 = step.V3;
            output.V9 = -step.V11;
            output.V10 = step.V15;
            output.V11 = -step.V7;
            output.V12 = step.V5;
            output.V13 = -step.V13;
            output.V14 = step.V9;
            output.V15 = -step.V1;
        }
    }
}
