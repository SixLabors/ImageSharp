// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Defines the eight-point AV1 inverse asymmetric discrete sine transform operator.
/// </summary>
internal readonly partial struct Av1Adst8Inverse1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative eight-point AV1 inverse asymmetric discrete sine transform.
    /// </summary>
    /// <param name="input">The eight frequency-domain coefficients.</param>
    /// <param name="output">The eight spatial-domain residual values.</param>
    /// <param name="step">The eight-element stage buffer owned by the containing two-dimensional transform.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;

        // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
        stage++;
        output[0] = input[7];
        output[1] = input[0];
        output[2] = input[5];
        output[3] = input[2];
        output[4] = input[3];
        output[5] = input[4];
        output[6] = input[1];
        output[7] = input[6];

        // Stage 2 applies the terminal odd-angle rotations in reverse.
        stage++;
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[4], output[0], cospi[60], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[60], output[0], -cospi[4], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[20], output[2], cospi[44], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[44], output[2], -cospi[20], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[36], output[4], cospi[28], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[28], output[4], -cospi[36], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[52], output[6], cospi[12], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[12], output[6], -cospi[52], output[7], cosBit);

        // Stage 3 separates the complete butterfly into two four-sample halves and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[4], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[5], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[6], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[7], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[0] - step[4], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[1] - step[5], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[2] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[3] - step[7], stageRange[stage]);

        // Stage 4 reverses the pi/8 and 3pi/8 rotations in the upper half.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);

        // Stage 5 separates the four-sample halves into adjacent coefficient pairs and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[2], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[3], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[0] - step[2], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[1] - step[3], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[6], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[7], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[4] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[5] - step[7], stageRange[stage]);

        // Stage 6 reverses the pi/4 rotations for the middle pairs.
        stage++;
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
}
