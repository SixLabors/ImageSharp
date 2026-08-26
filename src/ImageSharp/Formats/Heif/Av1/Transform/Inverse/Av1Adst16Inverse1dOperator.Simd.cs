// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <content>
/// Provides the SIMD kernels for the sixteen-point inverse ADST operator.
/// </content>
internal readonly partial struct Av1Adst16Inverse1dOperator
{
    /// <summary>
    /// Applies the transform to sixteen independent axes in parallel.
    /// </summary>
    /// <param name="input">The source values for the parallel transform axes.</param>
    /// <param name="output">The destination values for the parallel transform axes.</param>
    /// <param name="step">The fixed stage storage for the parallel transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(
        ref Av1TransformVector<Vector512<int>> input,
        ref Av1TransformVector<Vector512<int>> output,
        ref Av1TransformVector<Vector512<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;

        // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
        stage++;
        output[0] = input[15];
        output[1] = input[0];
        output[2] = input[13];
        output[3] = input[2];
        output[4] = input[11];
        output[5] = input[4];
        output[6] = input[9];
        output[7] = input[6];
        output[8] = input[7];
        output[9] = input[8];
        output[10] = input[5];
        output[11] = input[10];
        output[12] = input[3];
        output[13] = input[12];
        output[14] = input[1];
        output[15] = input[14];

        // Stage 2 applies the terminal odd-angle rotations in reverse.
        stage++;
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[2], output[0], cospi[62], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[62], output[0], -cospi[2], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[10], output[2], cospi[54], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[54], output[2], -cospi[10], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[18], output[4], cospi[46], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[46], output[4], -cospi[18], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[26], output[6], cospi[38], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[38], output[6], -cospi[26], output[7], cosBit);
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[34], output[8], cospi[30], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[30], output[8], -cospi[34], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[42], output[10], cospi[22], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[22], output[10], -cospi[42], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[50], output[12], cospi[14], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[14], output[12], -cospi[50], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[58], output[14], cospi[6], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[6], output[14], -cospi[58], output[15], cosBit);

        // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[8], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[9], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[10], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[11], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[12], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[13], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[14], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[15], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[0] - step[8], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[1] - step[9], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[2] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[3] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[4] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[5] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[6] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[7] - step[15], stageRange[stage]);

        // Stage 4 reverses the pi/16 rotations in the upper half.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[8], output[8], cospi[56], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[56], output[8], -cospi[8], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[40], output[10], cospi[24], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[24], output[10], -cospi[40], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[12], cospi[8], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[8], output[12], cospi[56], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[14], cospi[40], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[40], output[14], cospi[24], output[15], cosBit);

        // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[4], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[5], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[6], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[7], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[0] - step[4], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[1] - step[5], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[2] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[3] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[12], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[13], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[10] + step[14], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[11] + step[15], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[8] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[9] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[10] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[11] - step[15], stageRange[stage]);

        // Stage 6 reverses the pi/8 and 3pi/8 rotations.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[16], output[12], cospi[48], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[12], -cospi[16], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[14], cospi[16], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[15], cosBit);

        // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[2], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[3], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[0] - step[2], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[1] - step[3], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[6], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[7], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[4] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[5] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[10], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[11], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[8] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[9] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[14], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[13] + step[15], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[12] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[13] - step[15], stageRange[stage]);

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
        Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;

        // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
        stage++;
        output[0] = input[15];
        output[1] = input[0];
        output[2] = input[13];
        output[3] = input[2];
        output[4] = input[11];
        output[5] = input[4];
        output[6] = input[9];
        output[7] = input[6];
        output[8] = input[7];
        output[9] = input[8];
        output[10] = input[5];
        output[11] = input[10];
        output[12] = input[3];
        output[13] = input[12];
        output[14] = input[1];
        output[15] = input[14];

        // Stage 2 applies the terminal odd-angle rotations in reverse.
        stage++;
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[2], output[0], cospi[62], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[62], output[0], -cospi[2], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[10], output[2], cospi[54], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[54], output[2], -cospi[10], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[18], output[4], cospi[46], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[46], output[4], -cospi[18], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[26], output[6], cospi[38], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[38], output[6], -cospi[26], output[7], cosBit);
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[34], output[8], cospi[30], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[30], output[8], -cospi[34], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[42], output[10], cospi[22], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[22], output[10], -cospi[42], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[50], output[12], cospi[14], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[14], output[12], -cospi[50], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[58], output[14], cospi[6], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[6], output[14], -cospi[58], output[15], cosBit);

        // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[8], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[9], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[10], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[11], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[12], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[13], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[14], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[15], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[0] - step[8], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[1] - step[9], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[2] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[3] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[4] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[5] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[6] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[7] - step[15], stageRange[stage]);

        // Stage 4 reverses the pi/16 rotations in the upper half.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[8], output[8], cospi[56], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[56], output[8], -cospi[8], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[40], output[10], cospi[24], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[24], output[10], -cospi[40], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[12], cospi[8], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[8], output[12], cospi[56], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[14], cospi[40], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[40], output[14], cospi[24], output[15], cosBit);

        // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[4], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[5], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[6], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[7], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[0] - step[4], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[1] - step[5], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[2] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[3] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[12], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[13], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[10] + step[14], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[11] + step[15], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[8] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[9] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[10] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[11] - step[15], stageRange[stage]);

        // Stage 6 reverses the pi/8 and 3pi/8 rotations.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[16], output[12], cospi[48], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[12], -cospi[16], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[14], cospi[16], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[15], cosBit);

        // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[2], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[3], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[0] - step[2], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[1] - step[3], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[6], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[7], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[4] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[5] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[10], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[11], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[8] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[9] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[14], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[13] + step[15], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[12] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[13] - step[15], stageRange[stage]);

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

    /// <summary>
    /// Applies the transform to four independent axes in parallel.
    /// </summary>
    /// <param name="input">The source values for the parallel transform axes.</param>
    /// <param name="output">The destination values for the parallel transform axes.</param>
    /// <param name="step">The fixed stage storage for the parallel transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;

        // Stage 1 permutes the coefficients into the signed order used by the ADST factorization.
        stage++;
        output[0] = input[15];
        output[1] = input[0];
        output[2] = input[13];
        output[3] = input[2];
        output[4] = input[11];
        output[5] = input[4];
        output[6] = input[9];
        output[7] = input[6];
        output[8] = input[7];
        output[9] = input[8];
        output[10] = input[5];
        output[11] = input[10];
        output[12] = input[3];
        output[13] = input[12];
        output[14] = input[1];
        output[15] = input[14];

        // Stage 2 applies the terminal odd-angle rotations in reverse.
        stage++;
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[2], output[0], cospi[62], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[62], output[0], -cospi[2], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[10], output[2], cospi[54], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[54], output[2], -cospi[10], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[18], output[4], cospi[46], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[46], output[4], -cospi[18], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[26], output[6], cospi[38], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[38], output[6], -cospi[26], output[7], cosBit);
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[34], output[8], cospi[30], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[30], output[8], -cospi[34], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[42], output[10], cospi[22], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[22], output[10], -cospi[42], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[50], output[12], cospi[14], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[14], output[12], -cospi[50], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[58], output[14], cospi[6], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[6], output[14], -cospi[58], output[15], cosBit);

        // Stage 3 separates the complete butterfly into two eight-sample halves and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[8], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[9], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[10], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[11], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[12], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[13], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[14], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[15], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[0] - step[8], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[1] - step[9], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[2] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[3] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[4] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[5] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[6] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[7] - step[15], stageRange[stage]);

        // Stage 4 reverses the pi/16 rotations in the upper half.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[8], output[8], cospi[56], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[56], output[8], -cospi[8], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[40], output[10], cospi[24], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[24], output[10], -cospi[40], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[12], cospi[8], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[8], output[12], cospi[56], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[14], cospi[40], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[40], output[14], cospi[24], output[15], cosBit);

        // Stage 5 separates each eight-sample half into four-sample groups and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[4], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[5], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[6], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[7], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[0] - step[4], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[1] - step[5], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[2] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[3] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[12], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[13], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[10] + step[14], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[11] + step[15], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[8] - step[12], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[9] - step[13], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[10] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[11] - step[15], stageRange[stage]);

        // Stage 6 reverses the pi/8 and 3pi/8 rotations.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[16], output[12], cospi[48], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[12], -cospi[16], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[14], cospi[16], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[15], cosBit);

        // Stage 7 separates the four-sample groups into adjacent coefficient pairs and clamps each lane.
        stage++;
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[2], stageRange[stage]);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[3], stageRange[stage]);
        output[2] = Av1Transform1dMath.Clamp(step[0] - step[2], stageRange[stage]);
        output[3] = Av1Transform1dMath.Clamp(step[1] - step[3], stageRange[stage]);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[6], stageRange[stage]);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[7], stageRange[stage]);
        output[6] = Av1Transform1dMath.Clamp(step[4] - step[6], stageRange[stage]);
        output[7] = Av1Transform1dMath.Clamp(step[5] - step[7], stageRange[stage]);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[10], stageRange[stage]);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[11], stageRange[stage]);
        output[10] = Av1Transform1dMath.Clamp(step[8] - step[10], stageRange[stage]);
        output[11] = Av1Transform1dMath.Clamp(step[9] - step[11], stageRange[stage]);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[14], stageRange[stage]);
        output[13] = Av1Transform1dMath.Clamp(step[13] + step[15], stageRange[stage]);
        output[14] = Av1Transform1dMath.Clamp(step[12] - step[14], stageRange[stage]);
        output[15] = Av1Transform1dMath.Clamp(step[13] - step[15], stageRange[stage]);

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
}
