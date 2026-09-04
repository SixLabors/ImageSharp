// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines the 32-point AV1 inverse discrete cosine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. The SIMD overloads apply
/// the same staged butterflies, fixed-point rounding, and range clamps as the scalar overload without mixing axes.
/// </remarks>
internal static partial class Av1Inverse2dTransformer
{
    internal readonly struct Dct32Operator : IAv1Transform1dOperator
    {
        /// <summary>
        /// Applies the normative 32-point AV1 inverse discrete cosine transform.
        /// </summary>
        /// <param name="input">The 32 frequency-domain coefficients.</param>
        /// <param name="output">The 32 spatial-domain residual values.</param>
        /// <param name="step">The 32-element stage buffer owned by the containing two-dimensional transform.</param>
        /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
        /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
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

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
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

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
            stage++;
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
            step.V16 = Av1Transform1dMath.HalfButterfly(cospi[62], output.V16, -cospi[2], output.V31, cosBit);
            step.V17 = Av1Transform1dMath.HalfButterfly(cospi[30], output.V17, -cospi[34], output.V30, cosBit);
            step.V18 = Av1Transform1dMath.HalfButterfly(cospi[46], output.V18, -cospi[18], output.V29, cosBit);
            step.V19 = Av1Transform1dMath.HalfButterfly(cospi[14], output.V19, -cospi[50], output.V28, cosBit);
            step.V20 = Av1Transform1dMath.HalfButterfly(cospi[54], output.V20, -cospi[10], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(cospi[22], output.V21, -cospi[42], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(cospi[38], output.V22, -cospi[26], output.V25, cosBit);
            step.V23 = Av1Transform1dMath.HalfButterfly(cospi[6], output.V23, -cospi[58], output.V24, cosBit);
            step.V24 = Av1Transform1dMath.HalfButterfly(cospi[58], output.V23, cospi[6], output.V24, cosBit);
            step.V25 = Av1Transform1dMath.HalfButterfly(cospi[26], output.V22, cospi[38], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[42], output.V21, cospi[22], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(cospi[10], output.V20, cospi[54], output.V27, cosBit);
            step.V28 = Av1Transform1dMath.HalfButterfly(cospi[50], output.V19, cospi[14], output.V28, cosBit);
            step.V29 = Av1Transform1dMath.HalfButterfly(cospi[18], output.V18, cospi[46], output.V29, cosBit);
            step.V30 = Av1Transform1dMath.HalfButterfly(cospi[34], output.V17, cospi[30], output.V30, cosBit);
            step.V31 = Av1Transform1dMath.HalfButterfly(cospi[2], output.V16, cospi[62], output.V31, cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = step.V4;
            output.V5 = step.V5;
            output.V6 = step.V6;
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.HalfButterfly(cospi[60], step.V8, -cospi[4], step.V15, cosBit);
            output.V9 = Av1Transform1dMath.HalfButterfly(cospi[28], step.V9, -cospi[36], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(cospi[44], step.V10, -cospi[20], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(cospi[12], step.V11, -cospi[52], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[52], step.V11, cospi[12], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[20], step.V10, cospi[44], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[36], step.V9, cospi[28], step.V14, cosBit);
            output.V15 = Av1Transform1dMath.HalfButterfly(cospi[4], step.V8, cospi[60], step.V15, cosBit);
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V17, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V16 - step.V17, range);
            output.V18 = Av1Transform1dMath.Clamp(-step.V18 + step.V19, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V18 + step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V20 + step.V21, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V20 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(-step.V22 + step.V23, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V22 + step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V25, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V24 - step.V25, range);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V27, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V26 + step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V28 + step.V29, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V28 - step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(-step.V30 + step.V31, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V30 + step.V31, range);

            // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
            stage++;
            range = stageRange[stage];
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V4, cospi[56], step.V7, cosBit);
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V9, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V8 - output.V9, range);
            step.V10 = Av1Transform1dMath.Clamp(-output.V10 + output.V11, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V10 + output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(output.V12 + output.V13, range);
            step.V13 = Av1Transform1dMath.Clamp(output.V12 - output.V13, range);
            step.V14 = Av1Transform1dMath.Clamp(-output.V14 + output.V15, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V14 + output.V15, range);
            step.V16 = output.V16;
            step.V17 = Av1Transform1dMath.HalfButterfly(-cospi[8], output.V17, cospi[56], output.V30, cosBit);
            step.V18 = Av1Transform1dMath.HalfButterfly(-cospi[56], output.V18, -cospi[8], output.V29, cosBit);
            step.V19 = output.V19;
            step.V20 = output.V20;
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[40], output.V21, cospi[24], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(-cospi[24], output.V22, -cospi[40], output.V25, cosBit);
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = Av1Transform1dMath.HalfButterfly(-cospi[40], output.V22, cospi[24], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V21, cospi[40], output.V26, cosBit);
            step.V27 = output.V27;
            step.V28 = output.V28;
            step.V29 = Av1Transform1dMath.HalfButterfly(-cospi[8], output.V18, cospi[56], output.V29, cosBit);
            step.V30 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V17, cospi[8], output.V30, cosBit);
            step.V31 = output.V31;

            // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(-step.V6 + step.V7, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, range);
            output.V8 = step.V8;
            output.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V19, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V18, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V17 - step.V18, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V16 - step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(-step.V20 + step.V23, range);
            output.V21 = Av1Transform1dMath.Clamp(-step.V21 + step.V22, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V21 + step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V20 + step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V27, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V25 + step.V26, range);
            output.V26 = Av1Transform1dMath.Clamp(step.V25 - step.V26, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V24 - step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(-step.V28 + step.V31, range);
            output.V29 = Av1Transform1dMath.Clamp(-step.V29 + step.V30, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V29 + step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V28 + step.V31, range);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, range);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V11, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V9 + output.V10, range);
            step.V10 = Av1Transform1dMath.Clamp(output.V9 - output.V10, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V8 - output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(-output.V12 + output.V15, range);
            step.V13 = Av1Transform1dMath.Clamp(-output.V13 + output.V14, range);
            step.V14 = Av1Transform1dMath.Clamp(output.V13 + output.V14, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V12 + output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V18, cospi[48], output.V29, cosBit);
            step.V19 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V19, cospi[48], output.V28, cosBit);
            step.V20 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V20, -cospi[16], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V21, -cospi[16], output.V26, cosBit);
            step.V22 = output.V22;
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = output.V25;
            step.V26 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V21, cospi[48], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V20, cospi[48], output.V27, cosBit);
            step.V28 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V19, cospi[16], output.V28, cosBit);
            step.V29 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V18, cospi[16], output.V29, cosBit);
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Stage 7 widens the reconstructed groups through their next butterfly level.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, range);
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V23, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V22, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V18 + step.V21, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V19 + step.V20, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V19 - step.V20, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V18 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V17 - step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V16 - step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(-step.V24 + step.V31, range);
            output.V25 = Av1Transform1dMath.Clamp(-step.V25 + step.V30, range);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V29, range);
            output.V27 = Av1Transform1dMath.Clamp(-step.V27 + step.V28, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V27 + step.V28, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V26 + step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V25 + step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V24 + step.V31, range);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V15, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V14, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V13, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V12, range);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V11, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V5 + output.V10, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V6 + output.V9, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V7 + output.V8, range);
            step.V8 = Av1Transform1dMath.Clamp(output.V7 - output.V8, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V6 - output.V9, range);
            step.V10 = Av1Transform1dMath.Clamp(output.V5 - output.V10, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V4 - output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(output.V3 - output.V12, range);
            step.V13 = Av1Transform1dMath.Clamp(output.V2 - output.V13, range);
            step.V14 = Av1Transform1dMath.Clamp(output.V1 - output.V14, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V0 - output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = output.V18;
            step.V19 = output.V19;
            step.V20 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V23 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V24 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V25 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V28 = output.V28;
            step.V29 = output.V29;
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Stage 9 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V31, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V30, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V29, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V28, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V27, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V26, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V25, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V24, range);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V23, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V22, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V10 + step.V21, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V11 + step.V20, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V19, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V13 + step.V18, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V14 + step.V17, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V15 + step.V16, range);
            output.V16 = Av1Transform1dMath.Clamp(step.V15 - step.V16, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V14 - step.V17, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V13 - step.V18, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V12 - step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V11 - step.V20, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V10 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V9 - step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V8 - step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V7 - step.V24, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V6 - step.V25, range);
            output.V26 = Av1Transform1dMath.Clamp(step.V5 - step.V26, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V4 - step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V3 - step.V28, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V2 - step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V1 - step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V0 - step.V31, range);
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
            InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            int stage = 0;

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            stage++;
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

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
            stage++;
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
            step.V16 = Av1Transform1dMath.HalfButterfly(cospi[62], output.V16, -cospi[2], output.V31, cosBit);
            step.V17 = Av1Transform1dMath.HalfButterfly(cospi[30], output.V17, -cospi[34], output.V30, cosBit);
            step.V18 = Av1Transform1dMath.HalfButterfly(cospi[46], output.V18, -cospi[18], output.V29, cosBit);
            step.V19 = Av1Transform1dMath.HalfButterfly(cospi[14], output.V19, -cospi[50], output.V28, cosBit);
            step.V20 = Av1Transform1dMath.HalfButterfly(cospi[54], output.V20, -cospi[10], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(cospi[22], output.V21, -cospi[42], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(cospi[38], output.V22, -cospi[26], output.V25, cosBit);
            step.V23 = Av1Transform1dMath.HalfButterfly(cospi[6], output.V23, -cospi[58], output.V24, cosBit);
            step.V24 = Av1Transform1dMath.HalfButterfly(cospi[58], output.V23, cospi[6], output.V24, cosBit);
            step.V25 = Av1Transform1dMath.HalfButterfly(cospi[26], output.V22, cospi[38], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[42], output.V21, cospi[22], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(cospi[10], output.V20, cospi[54], output.V27, cosBit);
            step.V28 = Av1Transform1dMath.HalfButterfly(cospi[50], output.V19, cospi[14], output.V28, cosBit);
            step.V29 = Av1Transform1dMath.HalfButterfly(cospi[18], output.V18, cospi[46], output.V29, cosBit);
            step.V30 = Av1Transform1dMath.HalfButterfly(cospi[34], output.V17, cospi[30], output.V30, cosBit);
            step.V31 = Av1Transform1dMath.HalfButterfly(cospi[2], output.V16, cospi[62], output.V31, cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            stage++;
            byte range = stageRange[stage];
            output.V0 = step.V0;
            output.V1 = step.V1;
            output.V2 = step.V2;
            output.V3 = step.V3;
            output.V4 = step.V4;
            output.V5 = step.V5;
            output.V6 = step.V6;
            output.V7 = step.V7;
            output.V8 = Av1Transform1dMath.HalfButterfly(cospi[60], step.V8, -cospi[4], step.V15, cosBit);
            output.V9 = Av1Transform1dMath.HalfButterfly(cospi[28], step.V9, -cospi[36], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(cospi[44], step.V10, -cospi[20], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(cospi[12], step.V11, -cospi[52], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[52], step.V11, cospi[12], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[20], step.V10, cospi[44], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[36], step.V9, cospi[28], step.V14, cosBit);
            output.V15 = Av1Transform1dMath.HalfButterfly(cospi[4], step.V8, cospi[60], step.V15, cosBit);
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V17, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V16 - step.V17, range);
            output.V18 = Av1Transform1dMath.Clamp(-step.V18 + step.V19, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V18 + step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V20 + step.V21, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V20 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(-step.V22 + step.V23, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V22 + step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V25, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V24 - step.V25, range);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V27, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V26 + step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V28 + step.V29, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V28 - step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(-step.V30 + step.V31, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V30 + step.V31, range);

            // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
            stage++;
            range = stageRange[stage];
            step.V0 = output.V0;
            step.V1 = output.V1;
            step.V2 = output.V2;
            step.V3 = output.V3;
            step.V4 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V4, -cospi[8], output.V7, cosBit);
            step.V5 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V5, -cospi[40], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[40], output.V5, cospi[24], output.V6, cosBit);
            step.V7 = Av1Transform1dMath.HalfButterfly(cospi[8], output.V4, cospi[56], step.V7, cosBit);
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V9, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V8 - output.V9, range);
            step.V10 = Av1Transform1dMath.Clamp(-output.V10 + output.V11, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V10 + output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(output.V12 + output.V13, range);
            step.V13 = Av1Transform1dMath.Clamp(output.V12 - output.V13, range);
            step.V14 = Av1Transform1dMath.Clamp(-output.V14 + output.V15, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V14 + output.V15, range);
            step.V16 = output.V16;
            step.V17 = Av1Transform1dMath.HalfButterfly(-cospi[8], output.V17, cospi[56], output.V30, cosBit);
            step.V18 = Av1Transform1dMath.HalfButterfly(-cospi[56], output.V18, -cospi[8], output.V29, cosBit);
            step.V19 = output.V19;
            step.V20 = output.V20;
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[40], output.V21, cospi[24], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(-cospi[24], output.V22, -cospi[40], output.V25, cosBit);
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = Av1Transform1dMath.HalfButterfly(-cospi[40], output.V22, cospi[24], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[24], output.V21, cospi[40], output.V26, cosBit);
            step.V27 = output.V27;
            step.V28 = output.V28;
            step.V29 = Av1Transform1dMath.HalfButterfly(-cospi[8], output.V18, cospi[56], output.V29, cosBit);
            step.V30 = Av1Transform1dMath.HalfButterfly(cospi[56], output.V17, cospi[8], output.V30, cosBit);
            step.V31 = output.V31;

            // Stage 5 reconstructs the embedded eight-point groups and combines adjacent odd terms.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, cospi[32], step.V1, cosBit);
            output.V1 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V0, -cospi[32], step.V1, cosBit);
            output.V2 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V2, -cospi[16], step.V3, cosBit);
            output.V3 = Av1Transform1dMath.HalfButterfly(cospi[16], step.V2, cospi[48], step.V3, cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(-step.V6 + step.V7, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, range);
            output.V8 = step.V8;
            output.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V19, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V18, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V17 - step.V18, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V16 - step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(-step.V20 + step.V23, range);
            output.V21 = Av1Transform1dMath.Clamp(-step.V21 + step.V22, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V21 + step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V20 + step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V27, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V25 + step.V26, range);
            output.V26 = Av1Transform1dMath.Clamp(step.V25 - step.V26, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V24 - step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(-step.V28 + step.V31, range);
            output.V29 = Av1Transform1dMath.Clamp(-step.V29 + step.V30, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V29 + step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V28 + step.V31, range);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, range);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V11, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V9 + output.V10, range);
            step.V10 = Av1Transform1dMath.Clamp(output.V9 - output.V10, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V8 - output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(-output.V12 + output.V15, range);
            step.V13 = Av1Transform1dMath.Clamp(-output.V13 + output.V14, range);
            step.V14 = Av1Transform1dMath.Clamp(output.V13 + output.V14, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V12 + output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V18, cospi[48], output.V29, cosBit);
            step.V19 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V19, cospi[48], output.V28, cosBit);
            step.V20 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V20, -cospi[16], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[48], output.V21, -cospi[16], output.V26, cosBit);
            step.V22 = output.V22;
            step.V23 = output.V23;
            step.V24 = output.V24;
            step.V25 = output.V25;
            step.V26 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V21, cospi[48], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(-cospi[16], output.V20, cospi[48], output.V27, cosBit);
            step.V28 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V19, cospi[16], output.V28, cosBit);
            step.V29 = Av1Transform1dMath.HalfButterfly(cospi[48], output.V18, cospi[16], output.V29, cosBit);
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Stage 7 widens the reconstructed groups through their next butterfly level.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, range);
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V23, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V22, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V18 + step.V21, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V19 + step.V20, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V19 - step.V20, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V18 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V17 - step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V16 - step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(-step.V24 + step.V31, range);
            output.V25 = Av1Transform1dMath.Clamp(-step.V25 + step.V30, range);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V29, range);
            output.V27 = Av1Transform1dMath.Clamp(-step.V27 + step.V28, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V27 + step.V28, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V26 + step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V25 + step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V24 + step.V31, range);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            stage++;
            range = stageRange[stage];
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V15, range);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V14, range);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V13, range);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V12, range);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V11, range);
            step.V5 = Av1Transform1dMath.Clamp(output.V5 + output.V10, range);
            step.V6 = Av1Transform1dMath.Clamp(output.V6 + output.V9, range);
            step.V7 = Av1Transform1dMath.Clamp(output.V7 + output.V8, range);
            step.V8 = Av1Transform1dMath.Clamp(output.V7 - output.V8, range);
            step.V9 = Av1Transform1dMath.Clamp(output.V6 - output.V9, range);
            step.V10 = Av1Transform1dMath.Clamp(output.V5 - output.V10, range);
            step.V11 = Av1Transform1dMath.Clamp(output.V4 - output.V11, range);
            step.V12 = Av1Transform1dMath.Clamp(output.V3 - output.V12, range);
            step.V13 = Av1Transform1dMath.Clamp(output.V2 - output.V13, range);
            step.V14 = Av1Transform1dMath.Clamp(output.V1 - output.V14, range);
            step.V15 = Av1Transform1dMath.Clamp(output.V0 - output.V15, range);
            step.V16 = output.V16;
            step.V17 = output.V17;
            step.V18 = output.V18;
            step.V19 = output.V19;
            step.V20 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V21 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V22 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V23 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V24 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V23, cospi[32], output.V24, cosBit);
            step.V25 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V22, cospi[32], output.V25, cosBit);
            step.V26 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V21, cospi[32], output.V26, cosBit);
            step.V27 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V20, cospi[32], output.V27, cosBit);
            step.V28 = output.V28;
            step.V29 = output.V29;
            step.V30 = output.V30;
            step.V31 = output.V31;

            // Stage 9 merges the even and odd halves into spatial order and clamps every result.
            stage++;
            range = stageRange[stage];
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V31, range);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V30, range);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V29, range);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V28, range);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V27, range);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V26, range);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V25, range);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V24, range);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V23, range);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V22, range);
            output.V10 = Av1Transform1dMath.Clamp(step.V10 + step.V21, range);
            output.V11 = Av1Transform1dMath.Clamp(step.V11 + step.V20, range);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V19, range);
            output.V13 = Av1Transform1dMath.Clamp(step.V13 + step.V18, range);
            output.V14 = Av1Transform1dMath.Clamp(step.V14 + step.V17, range);
            output.V15 = Av1Transform1dMath.Clamp(step.V15 + step.V16, range);
            output.V16 = Av1Transform1dMath.Clamp(step.V15 - step.V16, range);
            output.V17 = Av1Transform1dMath.Clamp(step.V14 - step.V17, range);
            output.V18 = Av1Transform1dMath.Clamp(step.V13 - step.V18, range);
            output.V19 = Av1Transform1dMath.Clamp(step.V12 - step.V19, range);
            output.V20 = Av1Transform1dMath.Clamp(step.V11 - step.V20, range);
            output.V21 = Av1Transform1dMath.Clamp(step.V10 - step.V21, range);
            output.V22 = Av1Transform1dMath.Clamp(step.V9 - step.V22, range);
            output.V23 = Av1Transform1dMath.Clamp(step.V8 - step.V23, range);
            output.V24 = Av1Transform1dMath.Clamp(step.V7 - step.V24, range);
            output.V25 = Av1Transform1dMath.Clamp(step.V6 - step.V25, range);
            output.V26 = Av1Transform1dMath.Clamp(step.V5 - step.V26, range);
            output.V27 = Av1Transform1dMath.Clamp(step.V4 - step.V27, range);
            output.V28 = Av1Transform1dMath.Clamp(step.V3 - step.V28, range);
            output.V29 = Av1Transform1dMath.Clamp(step.V2 - step.V29, range);
            output.V30 = Av1Transform1dMath.Clamp(step.V1 - step.V30, range);
            output.V31 = Av1Transform1dMath.Clamp(step.V0 - step.V31, range);
        }
    }
}
