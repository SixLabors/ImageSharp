// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 8-point inverse ADST with at most 1 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Adst8Low1Operator : IAv1Transform1dOperator
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
            step[0] = Av1Math.RoundShift((long)output[1] * cospi[60], cosBit);
            step[1] = Av1Math.RoundShift((long)output[1] * -cospi[4], cosBit);

            // Stage 3 separates the complete butterfly into two four-sample halves and clamps each lane.
            output[0] = Av1Transform1dMath.Clamp(step[0], stageRange[3]);
            output[1] = Av1Transform1dMath.Clamp(step[1], stageRange[3]);
            output[4] = Av1Transform1dMath.Clamp(step[0], stageRange[3]);
            output[5] = Av1Transform1dMath.Clamp(step[1], stageRange[3]);

            // Stage 4 reverses the pi/8 and 3pi/8 rotations in the upper half.
            step[0] = output[0];
            step[1] = output[1];
            step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
            step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);

            // Stage 5 separates the four-sample halves into adjacent coefficient pairs and clamps each lane.
            output[0] = Av1Transform1dMath.Clamp(step[0], stageRange[5]);
            output[1] = Av1Transform1dMath.Clamp(step[1], stageRange[5]);
            output[2] = Av1Transform1dMath.Clamp(step[0], stageRange[5]);
            output[3] = Av1Transform1dMath.Clamp(step[1], stageRange[5]);
            output[4] = Av1Transform1dMath.Clamp(step[4], stageRange[5]);
            output[5] = Av1Transform1dMath.Clamp(step[5], stageRange[5]);
            output[6] = Av1Transform1dMath.Clamp(step[4], stageRange[5]);
            output[7] = Av1Transform1dMath.Clamp(step[5], stageRange[5]);

            // Stage 6 reverses the pi/4 rotations for the middle pairs.
            step[0] = output[0];
            step[1] = output[1];
            step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
            step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
            step[4] = output[4];
            step[5] = output[5];
            step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
            step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);

            // Stage 7 applies the AV1 signs and permutation that restore spatial sample order.
            output[0] = step[0];
            output[1] = -step[4];
            output[2] = step[6];
            output[3] = -step[2];
            output[4] = step[3];
            output[5] = -step[7];
            output[6] = step[5];
            output[7] = -step[1];
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
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V1, cospi[60], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V1, -cospi[4], cosBit);

            // Stage 3 separates the complete butterfly into two four-sample halves and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);
            output.V4 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V5 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);

            // Stage 4 reverses the pi/8 and 3pi/8 rotations in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);

            // Stage 5 separates the four-sample halves into adjacent coefficient pairs and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V2 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V3 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5, stageRange[5]);
            output.V6 = Av1Transform1dMath.Clamp(step.V4, stageRange[5]);
            output.V7 = Av1Transform1dMath.Clamp(step.V5, stageRange[5]);

            // Stage 6 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);

            // Stage 7 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = -step.V4;
            output.V2 = step.V6;
            output.V3 = -step.V2;
            output.V4 = step.V3;
            output.V5 = -step.V7;
            output.V6 = step.V5;
            output.V7 = -step.V1;
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
            step.V0 = Av1Transform1dMath.MultiplyRound(output.V1, cospi[60], cosBit);
            step.V1 = Av1Transform1dMath.MultiplyRound(output.V1, -cospi[4], cosBit);

            // Stage 3 separates the complete butterfly into two four-sample halves and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);
            output.V4 = Av1Transform1dMath.Clamp(step.V0, stageRange[3]);
            output.V5 = Av1Transform1dMath.Clamp(step.V1, stageRange[3]);

            // Stage 4 reverses the pi/8 and 3pi/8 rotations in the upper half.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[16], output.V4, cospi[48], output.V5, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V4, -cospi[16], output.V5, cosBit);

            // Stage 5 separates the four-sample halves into adjacent coefficient pairs and clamps each lane.
            output.V0 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V2 = Av1Transform1dMath.Clamp(step.V0, stageRange[5]);
            output.V3 = Av1Transform1dMath.Clamp(step.V1, stageRange[5]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5, stageRange[5]);
            output.V6 = Av1Transform1dMath.Clamp(step.V4, stageRange[5]);
            output.V7 = Av1Transform1dMath.Clamp(step.V5, stageRange[5]);

            // Stage 6 reverses the pi/4 rotations for the middle pairs.
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, cospi[32], output.V3, cosBit);
            step.V3 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V2, -cospi[32], output.V3, cosBit);
            step.V4 = output.V4;
            step.V5 = output.V5;
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, cospi[32], output.V7, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V6, -cospi[32], output.V7, cosBit);

            // Stage 7 applies the AV1 signs and permutation that restore spatial sample order.
            output.V0 = step.V0;
            output.V1 = -step.V4;
            output.V2 = step.V6;
            output.V3 = -step.V2;
            output.V4 = step.V3;
            output.V5 = -step.V7;
            output.V6 = step.V5;
            output.V7 = -step.V1;
        }
    }
}
