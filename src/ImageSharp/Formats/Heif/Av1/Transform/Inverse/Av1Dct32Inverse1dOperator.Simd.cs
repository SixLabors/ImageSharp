// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <content>
/// Provides the SIMD kernels for the thirty-two-point inverse DCT operator.
/// </content>
internal readonly partial struct Av1Dct32Inverse1dOperator
{
    /// <summary>
    /// Applies the transform to eight independent axes in parallel.
    /// </summary>
    /// <param name="input">The source values for the parallel transform axes.</param>
    /// <param name="output">The destination values for the parallel transform axes.</param>
    /// <param name="step">The fixed stage storage for the parallel transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(
        ref Av1TransformVector<Vector256<int>> input,
        ref Av1TransformVector<Vector256<int>> output,
        ref Av1TransformVector<Vector256<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        int stage = 0;

        // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
        stage++;
        output[0] = input[0];
        output[1] = input[16];
        output[2] = input[8];
        output[3] = input[24];
        output[4] = input[4];
        output[5] = input[20];
        output[6] = input[12];
        output[7] = input[28];
        output[8] = input[2];
        output[9] = input[18];
        output[10] = input[10];
        output[11] = input[26];
        output[12] = input[6];
        output[13] = input[22];
        output[14] = input[14];
        output[15] = input[30];
        output[16] = input[1];
        output[17] = input[17];
        output[18] = input[9];
        output[19] = input[25];
        output[20] = input[5];
        output[21] = input[21];
        output[22] = input[13];
        output[23] = input[29];
        output[24] = input[3];
        output[25] = input[19];
        output[26] = input[11];
        output[27] = input[27];
        output[28] = input[7];
        output[29] = input[23];
        output[30] = input[15];
        output[31] = input[31];

        // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = output[12];
        step[13] = output[13];
        step[14] = output[14];
        step[15] = output[15];
        step[16] = Av1Transform1dMath.HalfButterfly(cospi[62], output[16], -cospi[2], output[31], cosBit);
        step[17] = Av1Transform1dMath.HalfButterfly(cospi[30], output[17], -cospi[34], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(cospi[46], output[18], -cospi[18], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(cospi[14], output[19], -cospi[50], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(cospi[54], output[20], -cospi[10], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(cospi[22], output[21], -cospi[42], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(cospi[38], output[22], -cospi[26], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(cospi[6], output[23], -cospi[58], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[58], output[23], cospi[6], output[24], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[26], output[22], cospi[38], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[42], output[21], cospi[22], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[10], output[20], cospi[54], output[27], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[50], output[19], cospi[14], output[28], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[18], output[18], cospi[46], output[29], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[34], output[17], cospi[30], output[30], cosBit);
        step[31] = Av1Transform1dMath.HalfButterfly(cospi[2], output[16], cospi[62], output[31], cosBit);

        // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = Av1Transform1dMath.HalfButterfly(cospi[60], step[8], -cospi[4], step[15], cosBit);
        output[9] = Av1Transform1dMath.HalfButterfly(cospi[28], step[9], -cospi[36], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(cospi[44], step[10], -cospi[20], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(cospi[12], step[11], -cospi[52], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[52], step[11], cospi[12], step[12], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[20], step[10], cospi[44], step[13], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[36], step[9], cospi[28], step[14], cosBit);
        output[15] = Av1Transform1dMath.HalfButterfly(cospi[4], step[8], cospi[60], step[15], cosBit);
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[17], range);
        output[17] = Av1Transform1dMath.Clamp(step[16] - step[17], range);
        output[18] = Av1Transform1dMath.Clamp(-step[18] + step[19], range);
        output[19] = Av1Transform1dMath.Clamp(step[18] + step[19], range);
        output[20] = Av1Transform1dMath.Clamp(step[20] + step[21], range);
        output[21] = Av1Transform1dMath.Clamp(step[20] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(-step[22] + step[23], range);
        output[23] = Av1Transform1dMath.Clamp(step[22] + step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[24] + step[25], range);
        output[25] = Av1Transform1dMath.Clamp(step[24] - step[25], range);
        output[26] = Av1Transform1dMath.Clamp(-step[26] + step[27], range);
        output[27] = Av1Transform1dMath.Clamp(step[26] + step[27], range);
        output[28] = Av1Transform1dMath.Clamp(step[28] + step[29], range);
        output[29] = Av1Transform1dMath.Clamp(step[28] - step[29], range);
        output[30] = Av1Transform1dMath.Clamp(-step[30] + step[31], range);
        output[31] = Av1Transform1dMath.Clamp(step[30] + step[31], range);

        // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
        stage++;
        range = stageRange[stage];
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], step[7], cosBit);
        step[8] = Av1Transform1dMath.Clamp(output[8] + output[9], range);
        step[9] = Av1Transform1dMath.Clamp(output[8] - output[9], range);
        step[10] = Av1Transform1dMath.Clamp(-output[10] + output[11], range);
        step[11] = Av1Transform1dMath.Clamp(output[10] + output[11], range);
        step[12] = Av1Transform1dMath.Clamp(output[12] + output[13], range);
        step[13] = Av1Transform1dMath.Clamp(output[12] - output[13], range);
        step[14] = Av1Transform1dMath.Clamp(-output[14] + output[15], range);
        step[15] = Av1Transform1dMath.Clamp(output[14] + output[15], range);
        step[16] = output[16];
        step[17] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[17], cospi[56], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[18], -cospi[8], output[29], cosBit);
        step[19] = output[19];
        step[20] = output[20];
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[21], cospi[24], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[22], -cospi[40], output[25], cosBit);
        step[23] = output[23];
        step[24] = output[24];
        step[25] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[22], cospi[24], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[24], output[21], cospi[40], output[26], cosBit);
        step[27] = output[27];
        step[28] = output[28];
        step[29] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[18], cospi[56], output[29], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[56], output[17], cospi[8], output[30], cosBit);
        step[31] = output[31];

        // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
        output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(-step[6] + step[7], range);
        output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);
        output[8] = step[8];
        output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
        output[11] = step[11];
        output[12] = step[12];
        output[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[10], cospi[48], step[13], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[48], step[9], cospi[16], step[14], cosBit);
        output[15] = step[15];
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[19], range);
        output[17] = Av1Transform1dMath.Clamp(step[17] + step[18], range);
        output[18] = Av1Transform1dMath.Clamp(step[17] - step[18], range);
        output[19] = Av1Transform1dMath.Clamp(step[16] - step[19], range);
        output[20] = Av1Transform1dMath.Clamp(-step[20] + step[23], range);
        output[21] = Av1Transform1dMath.Clamp(-step[21] + step[22], range);
        output[22] = Av1Transform1dMath.Clamp(step[21] + step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[20] + step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[24] + step[27], range);
        output[25] = Av1Transform1dMath.Clamp(step[25] + step[26], range);
        output[26] = Av1Transform1dMath.Clamp(step[25] - step[26], range);
        output[27] = Av1Transform1dMath.Clamp(step[24] - step[27], range);
        output[28] = Av1Transform1dMath.Clamp(-step[28] + step[31], range);
        output[29] = Av1Transform1dMath.Clamp(-step[29] + step[30], range);
        output[30] = Av1Transform1dMath.Clamp(step[29] + step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[28] + step[31], range);

        // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
        step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
        step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
        step[7] = output[7];
        step[8] = Av1Transform1dMath.Clamp(output[8] + output[11], range);
        step[9] = Av1Transform1dMath.Clamp(output[9] + output[10], range);
        step[10] = Av1Transform1dMath.Clamp(output[9] - output[10], range);
        step[11] = Av1Transform1dMath.Clamp(output[8] - output[11], range);
        step[12] = Av1Transform1dMath.Clamp(-output[12] + output[15], range);
        step[13] = Av1Transform1dMath.Clamp(-output[13] + output[14], range);
        step[14] = Av1Transform1dMath.Clamp(output[13] + output[14], range);
        step[15] = Av1Transform1dMath.Clamp(output[12] + output[15], range);
        step[16] = output[16];
        step[17] = output[17];
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[18], cospi[48], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[19], cospi[48], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[20], -cospi[16], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[21], -cospi[16], output[26], cosBit);
        step[22] = output[22];
        step[23] = output[23];
        step[24] = output[24];
        step[25] = output[25];
        step[26] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[21], cospi[48], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[20], cospi[48], output[27], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[48], output[19], cospi[16], output[28], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[48], output[18], cospi[16], output[29], cosBit);
        step[30] = output[30];
        step[31] = output[31];

        // Stage 7 widens the reconstructed groups through their next butterfly level.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[7], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[6], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[5], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[4], range);
        output[4] = Av1Transform1dMath.Clamp(step[3] - step[4], range);
        output[5] = Av1Transform1dMath.Clamp(step[2] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(step[1] - step[6], range);
        output[7] = Av1Transform1dMath.Clamp(step[0] - step[7], range);
        output[8] = step[8];
        output[9] = step[9];
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[11], cospi[32], step[12], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[10], cospi[32], step[13], cosBit);
        output[14] = step[14];
        output[15] = step[15];
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[23], range);
        output[17] = Av1Transform1dMath.Clamp(step[17] + step[22], range);
        output[18] = Av1Transform1dMath.Clamp(step[18] + step[21], range);
        output[19] = Av1Transform1dMath.Clamp(step[19] + step[20], range);
        output[20] = Av1Transform1dMath.Clamp(step[19] - step[20], range);
        output[21] = Av1Transform1dMath.Clamp(step[18] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(step[17] - step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[16] - step[23], range);
        output[24] = Av1Transform1dMath.Clamp(-step[24] + step[31], range);
        output[25] = Av1Transform1dMath.Clamp(-step[25] + step[30], range);
        output[26] = Av1Transform1dMath.Clamp(-step[26] + step[29], range);
        output[27] = Av1Transform1dMath.Clamp(-step[27] + step[28], range);
        output[28] = Av1Transform1dMath.Clamp(step[27] + step[28], range);
        output[29] = Av1Transform1dMath.Clamp(step[26] + step[29], range);
        output[30] = Av1Transform1dMath.Clamp(step[25] + step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[24] + step[31], range);

        // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[15], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[14], range);
        step[2] = Av1Transform1dMath.Clamp(output[2] + output[13], range);
        step[3] = Av1Transform1dMath.Clamp(output[3] + output[12], range);
        step[4] = Av1Transform1dMath.Clamp(output[4] + output[11], range);
        step[5] = Av1Transform1dMath.Clamp(output[5] + output[10], range);
        step[6] = Av1Transform1dMath.Clamp(output[6] + output[9], range);
        step[7] = Av1Transform1dMath.Clamp(output[7] + output[8], range);
        step[8] = Av1Transform1dMath.Clamp(output[7] - output[8], range);
        step[9] = Av1Transform1dMath.Clamp(output[6] - output[9], range);
        step[10] = Av1Transform1dMath.Clamp(output[5] - output[10], range);
        step[11] = Av1Transform1dMath.Clamp(output[4] - output[11], range);
        step[12] = Av1Transform1dMath.Clamp(output[3] - output[12], range);
        step[13] = Av1Transform1dMath.Clamp(output[2] - output[13], range);
        step[14] = Av1Transform1dMath.Clamp(output[1] - output[14], range);
        step[15] = Av1Transform1dMath.Clamp(output[0] - output[15], range);
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[20], cospi[32], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[21], cospi[32], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[22], cospi[32], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[23], cospi[32], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[32], output[23], cospi[32], output[24], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[32], output[22], cospi[32], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[32], output[21], cospi[32], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[32], output[20], cospi[32], output[27], cosBit);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 9 merges the even and odd halves into spatial order and clamps every result.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[31], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[30], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[29], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[28], range);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[27], range);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[26], range);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[25], range);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[24], range);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[23], range);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[22], range);
        output[10] = Av1Transform1dMath.Clamp(step[10] + step[21], range);
        output[11] = Av1Transform1dMath.Clamp(step[11] + step[20], range);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[19], range);
        output[13] = Av1Transform1dMath.Clamp(step[13] + step[18], range);
        output[14] = Av1Transform1dMath.Clamp(step[14] + step[17], range);
        output[15] = Av1Transform1dMath.Clamp(step[15] + step[16], range);
        output[16] = Av1Transform1dMath.Clamp(step[15] - step[16], range);
        output[17] = Av1Transform1dMath.Clamp(step[14] - step[17], range);
        output[18] = Av1Transform1dMath.Clamp(step[13] - step[18], range);
        output[19] = Av1Transform1dMath.Clamp(step[12] - step[19], range);
        output[20] = Av1Transform1dMath.Clamp(step[11] - step[20], range);
        output[21] = Av1Transform1dMath.Clamp(step[10] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(step[9] - step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[8] - step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[7] - step[24], range);
        output[25] = Av1Transform1dMath.Clamp(step[6] - step[25], range);
        output[26] = Av1Transform1dMath.Clamp(step[5] - step[26], range);
        output[27] = Av1Transform1dMath.Clamp(step[4] - step[27], range);
        output[28] = Av1Transform1dMath.Clamp(step[3] - step[28], range);
        output[29] = Av1Transform1dMath.Clamp(step[2] - step[29], range);
        output[30] = Av1Transform1dMath.Clamp(step[1] - step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[0] - step[31], range);
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

        // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
        stage++;
        output[0] = input[0];
        output[1] = input[16];
        output[2] = input[8];
        output[3] = input[24];
        output[4] = input[4];
        output[5] = input[20];
        output[6] = input[12];
        output[7] = input[28];
        output[8] = input[2];
        output[9] = input[18];
        output[10] = input[10];
        output[11] = input[26];
        output[12] = input[6];
        output[13] = input[22];
        output[14] = input[14];
        output[15] = input[30];
        output[16] = input[1];
        output[17] = input[17];
        output[18] = input[9];
        output[19] = input[25];
        output[20] = input[5];
        output[21] = input[21];
        output[22] = input[13];
        output[23] = input[29];
        output[24] = input[3];
        output[25] = input[19];
        output[26] = input[11];
        output[27] = input[27];
        output[28] = input[7];
        output[29] = input[23];
        output[30] = input[15];
        output[31] = input[31];

        // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = output[12];
        step[13] = output[13];
        step[14] = output[14];
        step[15] = output[15];
        step[16] = Av1Transform1dMath.HalfButterfly(cospi[62], output[16], -cospi[2], output[31], cosBit);
        step[17] = Av1Transform1dMath.HalfButterfly(cospi[30], output[17], -cospi[34], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(cospi[46], output[18], -cospi[18], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(cospi[14], output[19], -cospi[50], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(cospi[54], output[20], -cospi[10], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(cospi[22], output[21], -cospi[42], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(cospi[38], output[22], -cospi[26], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(cospi[6], output[23], -cospi[58], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[58], output[23], cospi[6], output[24], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[26], output[22], cospi[38], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[42], output[21], cospi[22], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[10], output[20], cospi[54], output[27], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[50], output[19], cospi[14], output[28], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[18], output[18], cospi[46], output[29], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[34], output[17], cospi[30], output[30], cosBit);
        step[31] = Av1Transform1dMath.HalfButterfly(cospi[2], output[16], cospi[62], output[31], cosBit);

        // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = Av1Transform1dMath.HalfButterfly(cospi[60], step[8], -cospi[4], step[15], cosBit);
        output[9] = Av1Transform1dMath.HalfButterfly(cospi[28], step[9], -cospi[36], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(cospi[44], step[10], -cospi[20], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(cospi[12], step[11], -cospi[52], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[52], step[11], cospi[12], step[12], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[20], step[10], cospi[44], step[13], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[36], step[9], cospi[28], step[14], cosBit);
        output[15] = Av1Transform1dMath.HalfButterfly(cospi[4], step[8], cospi[60], step[15], cosBit);
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[17], range);
        output[17] = Av1Transform1dMath.Clamp(step[16] - step[17], range);
        output[18] = Av1Transform1dMath.Clamp(-step[18] + step[19], range);
        output[19] = Av1Transform1dMath.Clamp(step[18] + step[19], range);
        output[20] = Av1Transform1dMath.Clamp(step[20] + step[21], range);
        output[21] = Av1Transform1dMath.Clamp(step[20] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(-step[22] + step[23], range);
        output[23] = Av1Transform1dMath.Clamp(step[22] + step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[24] + step[25], range);
        output[25] = Av1Transform1dMath.Clamp(step[24] - step[25], range);
        output[26] = Av1Transform1dMath.Clamp(-step[26] + step[27], range);
        output[27] = Av1Transform1dMath.Clamp(step[26] + step[27], range);
        output[28] = Av1Transform1dMath.Clamp(step[28] + step[29], range);
        output[29] = Av1Transform1dMath.Clamp(step[28] - step[29], range);
        output[30] = Av1Transform1dMath.Clamp(-step[30] + step[31], range);
        output[31] = Av1Transform1dMath.Clamp(step[30] + step[31], range);

        // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
        stage++;
        range = stageRange[stage];
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], step[7], cosBit);
        step[8] = Av1Transform1dMath.Clamp(output[8] + output[9], range);
        step[9] = Av1Transform1dMath.Clamp(output[8] - output[9], range);
        step[10] = Av1Transform1dMath.Clamp(-output[10] + output[11], range);
        step[11] = Av1Transform1dMath.Clamp(output[10] + output[11], range);
        step[12] = Av1Transform1dMath.Clamp(output[12] + output[13], range);
        step[13] = Av1Transform1dMath.Clamp(output[12] - output[13], range);
        step[14] = Av1Transform1dMath.Clamp(-output[14] + output[15], range);
        step[15] = Av1Transform1dMath.Clamp(output[14] + output[15], range);
        step[16] = output[16];
        step[17] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[17], cospi[56], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[18], -cospi[8], output[29], cosBit);
        step[19] = output[19];
        step[20] = output[20];
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[21], cospi[24], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[22], -cospi[40], output[25], cosBit);
        step[23] = output[23];
        step[24] = output[24];
        step[25] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[22], cospi[24], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[24], output[21], cospi[40], output[26], cosBit);
        step[27] = output[27];
        step[28] = output[28];
        step[29] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[18], cospi[56], output[29], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[56], output[17], cospi[8], output[30], cosBit);
        step[31] = output[31];

        // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
        output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(-step[6] + step[7], range);
        output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);
        output[8] = step[8];
        output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
        output[11] = step[11];
        output[12] = step[12];
        output[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[10], cospi[48], step[13], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[48], step[9], cospi[16], step[14], cosBit);
        output[15] = step[15];
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[19], range);
        output[17] = Av1Transform1dMath.Clamp(step[17] + step[18], range);
        output[18] = Av1Transform1dMath.Clamp(step[17] - step[18], range);
        output[19] = Av1Transform1dMath.Clamp(step[16] - step[19], range);
        output[20] = Av1Transform1dMath.Clamp(-step[20] + step[23], range);
        output[21] = Av1Transform1dMath.Clamp(-step[21] + step[22], range);
        output[22] = Av1Transform1dMath.Clamp(step[21] + step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[20] + step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[24] + step[27], range);
        output[25] = Av1Transform1dMath.Clamp(step[25] + step[26], range);
        output[26] = Av1Transform1dMath.Clamp(step[25] - step[26], range);
        output[27] = Av1Transform1dMath.Clamp(step[24] - step[27], range);
        output[28] = Av1Transform1dMath.Clamp(-step[28] + step[31], range);
        output[29] = Av1Transform1dMath.Clamp(-step[29] + step[30], range);
        output[30] = Av1Transform1dMath.Clamp(step[29] + step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[28] + step[31], range);

        // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
        step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
        step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
        step[7] = output[7];
        step[8] = Av1Transform1dMath.Clamp(output[8] + output[11], range);
        step[9] = Av1Transform1dMath.Clamp(output[9] + output[10], range);
        step[10] = Av1Transform1dMath.Clamp(output[9] - output[10], range);
        step[11] = Av1Transform1dMath.Clamp(output[8] - output[11], range);
        step[12] = Av1Transform1dMath.Clamp(-output[12] + output[15], range);
        step[13] = Av1Transform1dMath.Clamp(-output[13] + output[14], range);
        step[14] = Av1Transform1dMath.Clamp(output[13] + output[14], range);
        step[15] = Av1Transform1dMath.Clamp(output[12] + output[15], range);
        step[16] = output[16];
        step[17] = output[17];
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[18], cospi[48], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[19], cospi[48], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[20], -cospi[16], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[21], -cospi[16], output[26], cosBit);
        step[22] = output[22];
        step[23] = output[23];
        step[24] = output[24];
        step[25] = output[25];
        step[26] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[21], cospi[48], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[20], cospi[48], output[27], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[48], output[19], cospi[16], output[28], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[48], output[18], cospi[16], output[29], cosBit);
        step[30] = output[30];
        step[31] = output[31];

        // Stage 7 widens the reconstructed groups through their next butterfly level.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[7], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[6], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[5], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[4], range);
        output[4] = Av1Transform1dMath.Clamp(step[3] - step[4], range);
        output[5] = Av1Transform1dMath.Clamp(step[2] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(step[1] - step[6], range);
        output[7] = Av1Transform1dMath.Clamp(step[0] - step[7], range);
        output[8] = step[8];
        output[9] = step[9];
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[11], cospi[32], step[12], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[10], cospi[32], step[13], cosBit);
        output[14] = step[14];
        output[15] = step[15];
        output[16] = Av1Transform1dMath.Clamp(step[16] + step[23], range);
        output[17] = Av1Transform1dMath.Clamp(step[17] + step[22], range);
        output[18] = Av1Transform1dMath.Clamp(step[18] + step[21], range);
        output[19] = Av1Transform1dMath.Clamp(step[19] + step[20], range);
        output[20] = Av1Transform1dMath.Clamp(step[19] - step[20], range);
        output[21] = Av1Transform1dMath.Clamp(step[18] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(step[17] - step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[16] - step[23], range);
        output[24] = Av1Transform1dMath.Clamp(-step[24] + step[31], range);
        output[25] = Av1Transform1dMath.Clamp(-step[25] + step[30], range);
        output[26] = Av1Transform1dMath.Clamp(-step[26] + step[29], range);
        output[27] = Av1Transform1dMath.Clamp(-step[27] + step[28], range);
        output[28] = Av1Transform1dMath.Clamp(step[27] + step[28], range);
        output[29] = Av1Transform1dMath.Clamp(step[26] + step[29], range);
        output[30] = Av1Transform1dMath.Clamp(step[25] + step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[24] + step[31], range);

        // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[15], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[14], range);
        step[2] = Av1Transform1dMath.Clamp(output[2] + output[13], range);
        step[3] = Av1Transform1dMath.Clamp(output[3] + output[12], range);
        step[4] = Av1Transform1dMath.Clamp(output[4] + output[11], range);
        step[5] = Av1Transform1dMath.Clamp(output[5] + output[10], range);
        step[6] = Av1Transform1dMath.Clamp(output[6] + output[9], range);
        step[7] = Av1Transform1dMath.Clamp(output[7] + output[8], range);
        step[8] = Av1Transform1dMath.Clamp(output[7] - output[8], range);
        step[9] = Av1Transform1dMath.Clamp(output[6] - output[9], range);
        step[10] = Av1Transform1dMath.Clamp(output[5] - output[10], range);
        step[11] = Av1Transform1dMath.Clamp(output[4] - output[11], range);
        step[12] = Av1Transform1dMath.Clamp(output[3] - output[12], range);
        step[13] = Av1Transform1dMath.Clamp(output[2] - output[13], range);
        step[14] = Av1Transform1dMath.Clamp(output[1] - output[14], range);
        step[15] = Av1Transform1dMath.Clamp(output[0] - output[15], range);
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[20], cospi[32], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[21], cospi[32], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[22], cospi[32], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[23], cospi[32], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[32], output[23], cospi[32], output[24], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[32], output[22], cospi[32], output[25], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[32], output[21], cospi[32], output[26], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[32], output[20], cospi[32], output[27], cosBit);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 9 merges the even and odd halves into spatial order and clamps every result.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[31], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[30], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[29], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[28], range);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[27], range);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[26], range);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[25], range);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[24], range);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[23], range);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[22], range);
        output[10] = Av1Transform1dMath.Clamp(step[10] + step[21], range);
        output[11] = Av1Transform1dMath.Clamp(step[11] + step[20], range);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[19], range);
        output[13] = Av1Transform1dMath.Clamp(step[13] + step[18], range);
        output[14] = Av1Transform1dMath.Clamp(step[14] + step[17], range);
        output[15] = Av1Transform1dMath.Clamp(step[15] + step[16], range);
        output[16] = Av1Transform1dMath.Clamp(step[15] - step[16], range);
        output[17] = Av1Transform1dMath.Clamp(step[14] - step[17], range);
        output[18] = Av1Transform1dMath.Clamp(step[13] - step[18], range);
        output[19] = Av1Transform1dMath.Clamp(step[12] - step[19], range);
        output[20] = Av1Transform1dMath.Clamp(step[11] - step[20], range);
        output[21] = Av1Transform1dMath.Clamp(step[10] - step[21], range);
        output[22] = Av1Transform1dMath.Clamp(step[9] - step[22], range);
        output[23] = Av1Transform1dMath.Clamp(step[8] - step[23], range);
        output[24] = Av1Transform1dMath.Clamp(step[7] - step[24], range);
        output[25] = Av1Transform1dMath.Clamp(step[6] - step[25], range);
        output[26] = Av1Transform1dMath.Clamp(step[5] - step[26], range);
        output[27] = Av1Transform1dMath.Clamp(step[4] - step[27], range);
        output[28] = Av1Transform1dMath.Clamp(step[3] - step[28], range);
        output[29] = Av1Transform1dMath.Clamp(step[2] - step[29], range);
        output[30] = Av1Transform1dMath.Clamp(step[1] - step[30], range);
        output[31] = Av1Transform1dMath.Clamp(step[0] - step[31], range);
    }
}
