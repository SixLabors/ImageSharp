// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 32-point inverse DCT with at most 16 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct32Low16Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 16;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output[0] = input[0];
            output[2] = input[8];
            output[4] = input[4];
            output[6] = input[12];
            output[8] = input[2];
            output[10] = input[10];
            output[12] = input[6];
            output[14] = input[14];
            output[16] = input[1];
            output[18] = input[9];
            output[20] = input[5];
            output[22] = input[13];
            output[24] = input[3];
            output[26] = input[11];
            output[28] = input[7];
            output[30] = input[15];

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
            step[0] = output[0];
            step[2] = output[2];
            step[4] = output[4];
            step[6] = output[6];
            step[8] = output[8];
            step[10] = output[10];
            step[12] = output[12];
            step[14] = output[14];
            step[16] = Av1Math.RoundShift((long)output[16] * cospi[62], cosBit);
            step[17] = Av1Math.RoundShift((long)output[30] * -cospi[34], cosBit);
            step[18] = Av1Math.RoundShift((long)output[18] * cospi[46], cosBit);
            step[19] = Av1Math.RoundShift((long)output[28] * -cospi[50], cosBit);
            step[20] = Av1Math.RoundShift((long)output[20] * cospi[54], cosBit);
            step[21] = Av1Math.RoundShift((long)output[26] * -cospi[42], cosBit);
            step[22] = Av1Math.RoundShift((long)output[22] * cospi[38], cosBit);
            step[23] = Av1Math.RoundShift((long)output[24] * -cospi[58], cosBit);
            step[24] = Av1Math.RoundShift((long)output[24] * cospi[6], cosBit);
            step[25] = Av1Math.RoundShift((long)output[22] * cospi[26], cosBit);
            step[26] = Av1Math.RoundShift((long)output[26] * cospi[22], cosBit);
            step[27] = Av1Math.RoundShift((long)output[20] * cospi[10], cosBit);
            step[28] = Av1Math.RoundShift((long)output[28] * cospi[14], cosBit);
            step[29] = Av1Math.RoundShift((long)output[18] * cospi[18], cosBit);
            step[30] = Av1Math.RoundShift((long)output[30] * cospi[30], cosBit);
            step[31] = Av1Math.RoundShift((long)output[16] * cospi[2], cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            output[0] = step[0];
            output[2] = step[2];
            output[4] = step[4];
            output[6] = step[6];
            output[8] = Av1Math.RoundShift((long)step[8] * cospi[60], cosBit);
            output[9] = Av1Math.RoundShift((long)step[14] * -cospi[36], cosBit);
            output[10] = Av1Math.RoundShift((long)step[10] * cospi[44], cosBit);
            output[11] = Av1Math.RoundShift((long)step[12] * -cospi[52], cosBit);
            output[12] = Av1Math.RoundShift((long)step[12] * cospi[12], cosBit);
            output[13] = Av1Math.RoundShift((long)step[10] * cospi[20], cosBit);
            output[14] = Av1Math.RoundShift((long)step[14] * cospi[28], cosBit);
            output[15] = Av1Math.RoundShift((long)step[8] * cospi[4], cosBit);
            output[16] = Av1Transform1dMath.Clamp(step[16] + step[17], stageRange[3]);
            output[17] = Av1Transform1dMath.Clamp(step[16] - step[17], stageRange[3]);
            output[18] = Av1Transform1dMath.Clamp(-step[18] + step[19], stageRange[3]);
            output[19] = Av1Transform1dMath.Clamp(step[18] + step[19], stageRange[3]);
            output[20] = Av1Transform1dMath.Clamp(step[20] + step[21], stageRange[3]);
            output[21] = Av1Transform1dMath.Clamp(step[20] - step[21], stageRange[3]);
            output[22] = Av1Transform1dMath.Clamp(-step[22] + step[23], stageRange[3]);
            output[23] = Av1Transform1dMath.Clamp(step[22] + step[23], stageRange[3]);
            output[24] = Av1Transform1dMath.Clamp(step[24] + step[25], stageRange[3]);
            output[25] = Av1Transform1dMath.Clamp(step[24] - step[25], stageRange[3]);
            output[26] = Av1Transform1dMath.Clamp(-step[26] + step[27], stageRange[3]);
            output[27] = Av1Transform1dMath.Clamp(step[26] + step[27], stageRange[3]);
            output[28] = Av1Transform1dMath.Clamp(step[28] + step[29], stageRange[3]);
            output[29] = Av1Transform1dMath.Clamp(step[28] - step[29], stageRange[3]);
            output[30] = Av1Transform1dMath.Clamp(-step[30] + step[31], stageRange[3]);
            output[31] = Av1Transform1dMath.Clamp(step[30] + step[31], stageRange[3]);

            // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
            step[0] = output[0];
            step[2] = output[2];
            step[4] = Av1Math.RoundShift((long)output[4] * cospi[56], cosBit);
            step[5] = Av1Math.RoundShift((long)output[6] * -cospi[40], cosBit);
            step[6] = Av1Math.RoundShift((long)output[6] * cospi[24], cosBit);
            step[7] = Av1Math.RoundShift((long)output[4] * cospi[8], cosBit);
            step[8] = Av1Transform1dMath.Clamp(output[8] + output[9], stageRange[4]);
            step[9] = Av1Transform1dMath.Clamp(output[8] - output[9], stageRange[4]);
            step[10] = Av1Transform1dMath.Clamp(-output[10] + output[11], stageRange[4]);
            step[11] = Av1Transform1dMath.Clamp(output[10] + output[11], stageRange[4]);
            step[12] = Av1Transform1dMath.Clamp(output[12] + output[13], stageRange[4]);
            step[13] = Av1Transform1dMath.Clamp(output[12] - output[13], stageRange[4]);
            step[14] = Av1Transform1dMath.Clamp(-output[14] + output[15], stageRange[4]);
            step[15] = Av1Transform1dMath.Clamp(output[14] + output[15], stageRange[4]);
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
            output[0] = Av1Math.RoundShift((long)step[0] * cospi[32], cosBit);
            output[1] = Av1Math.RoundShift((long)step[0] * cospi[32], cosBit);
            output[2] = Av1Math.RoundShift((long)step[2] * cospi[48], cosBit);
            output[3] = Av1Math.RoundShift((long)step[2] * cospi[16], cosBit);
            output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], stageRange[5]);
            output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], stageRange[5]);
            output[6] = Av1Transform1dMath.Clamp(-step[6] + step[7], stageRange[5]);
            output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], stageRange[5]);
            output[8] = step[8];
            output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
            output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
            output[11] = step[11];
            output[12] = step[12];
            output[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[10], cospi[48], step[13], cosBit);
            output[14] = Av1Transform1dMath.HalfButterfly(cospi[48], step[9], cospi[16], step[14], cosBit);
            output[15] = step[15];
            output[16] = Av1Transform1dMath.Clamp(step[16] + step[19], stageRange[5]);
            output[17] = Av1Transform1dMath.Clamp(step[17] + step[18], stageRange[5]);
            output[18] = Av1Transform1dMath.Clamp(step[17] - step[18], stageRange[5]);
            output[19] = Av1Transform1dMath.Clamp(step[16] - step[19], stageRange[5]);
            output[20] = Av1Transform1dMath.Clamp(-step[20] + step[23], stageRange[5]);
            output[21] = Av1Transform1dMath.Clamp(-step[21] + step[22], stageRange[5]);
            output[22] = Av1Transform1dMath.Clamp(step[21] + step[22], stageRange[5]);
            output[23] = Av1Transform1dMath.Clamp(step[20] + step[23], stageRange[5]);
            output[24] = Av1Transform1dMath.Clamp(step[24] + step[27], stageRange[5]);
            output[25] = Av1Transform1dMath.Clamp(step[25] + step[26], stageRange[5]);
            output[26] = Av1Transform1dMath.Clamp(step[25] - step[26], stageRange[5]);
            output[27] = Av1Transform1dMath.Clamp(step[24] - step[27], stageRange[5]);
            output[28] = Av1Transform1dMath.Clamp(-step[28] + step[31], stageRange[5]);
            output[29] = Av1Transform1dMath.Clamp(-step[29] + step[30], stageRange[5]);
            output[30] = Av1Transform1dMath.Clamp(step[29] + step[30], stageRange[5]);
            output[31] = Av1Transform1dMath.Clamp(step[28] + step[31], stageRange[5]);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], stageRange[6]);
            step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], stageRange[6]);
            step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], stageRange[6]);
            step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], stageRange[6]);
            step[4] = output[4];
            step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
            step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
            step[7] = output[7];
            step[8] = Av1Transform1dMath.Clamp(output[8] + output[11], stageRange[6]);
            step[9] = Av1Transform1dMath.Clamp(output[9] + output[10], stageRange[6]);
            step[10] = Av1Transform1dMath.Clamp(output[9] - output[10], stageRange[6]);
            step[11] = Av1Transform1dMath.Clamp(output[8] - output[11], stageRange[6]);
            step[12] = Av1Transform1dMath.Clamp(-output[12] + output[15], stageRange[6]);
            step[13] = Av1Transform1dMath.Clamp(-output[13] + output[14], stageRange[6]);
            step[14] = Av1Transform1dMath.Clamp(output[13] + output[14], stageRange[6]);
            step[15] = Av1Transform1dMath.Clamp(output[12] + output[15], stageRange[6]);
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
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[7], stageRange[7]);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[6], stageRange[7]);
            output[2] = Av1Transform1dMath.Clamp(step[2] + step[5], stageRange[7]);
            output[3] = Av1Transform1dMath.Clamp(step[3] + step[4], stageRange[7]);
            output[4] = Av1Transform1dMath.Clamp(step[3] - step[4], stageRange[7]);
            output[5] = Av1Transform1dMath.Clamp(step[2] - step[5], stageRange[7]);
            output[6] = Av1Transform1dMath.Clamp(step[1] - step[6], stageRange[7]);
            output[7] = Av1Transform1dMath.Clamp(step[0] - step[7], stageRange[7]);
            output[8] = step[8];
            output[9] = step[9];
            output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
            output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
            output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[11], cospi[32], step[12], cosBit);
            output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[10], cospi[32], step[13], cosBit);
            output[14] = step[14];
            output[15] = step[15];
            output[16] = Av1Transform1dMath.Clamp(step[16] + step[23], stageRange[7]);
            output[17] = Av1Transform1dMath.Clamp(step[17] + step[22], stageRange[7]);
            output[18] = Av1Transform1dMath.Clamp(step[18] + step[21], stageRange[7]);
            output[19] = Av1Transform1dMath.Clamp(step[19] + step[20], stageRange[7]);
            output[20] = Av1Transform1dMath.Clamp(step[19] - step[20], stageRange[7]);
            output[21] = Av1Transform1dMath.Clamp(step[18] - step[21], stageRange[7]);
            output[22] = Av1Transform1dMath.Clamp(step[17] - step[22], stageRange[7]);
            output[23] = Av1Transform1dMath.Clamp(step[16] - step[23], stageRange[7]);
            output[24] = Av1Transform1dMath.Clamp(-step[24] + step[31], stageRange[7]);
            output[25] = Av1Transform1dMath.Clamp(-step[25] + step[30], stageRange[7]);
            output[26] = Av1Transform1dMath.Clamp(-step[26] + step[29], stageRange[7]);
            output[27] = Av1Transform1dMath.Clamp(-step[27] + step[28], stageRange[7]);
            output[28] = Av1Transform1dMath.Clamp(step[27] + step[28], stageRange[7]);
            output[29] = Av1Transform1dMath.Clamp(step[26] + step[29], stageRange[7]);
            output[30] = Av1Transform1dMath.Clamp(step[25] + step[30], stageRange[7]);
            output[31] = Av1Transform1dMath.Clamp(step[24] + step[31], stageRange[7]);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            step[0] = Av1Transform1dMath.Clamp(output[0] + output[15], stageRange[8]);
            step[1] = Av1Transform1dMath.Clamp(output[1] + output[14], stageRange[8]);
            step[2] = Av1Transform1dMath.Clamp(output[2] + output[13], stageRange[8]);
            step[3] = Av1Transform1dMath.Clamp(output[3] + output[12], stageRange[8]);
            step[4] = Av1Transform1dMath.Clamp(output[4] + output[11], stageRange[8]);
            step[5] = Av1Transform1dMath.Clamp(output[5] + output[10], stageRange[8]);
            step[6] = Av1Transform1dMath.Clamp(output[6] + output[9], stageRange[8]);
            step[7] = Av1Transform1dMath.Clamp(output[7] + output[8], stageRange[8]);
            step[8] = Av1Transform1dMath.Clamp(output[7] - output[8], stageRange[8]);
            step[9] = Av1Transform1dMath.Clamp(output[6] - output[9], stageRange[8]);
            step[10] = Av1Transform1dMath.Clamp(output[5] - output[10], stageRange[8]);
            step[11] = Av1Transform1dMath.Clamp(output[4] - output[11], stageRange[8]);
            step[12] = Av1Transform1dMath.Clamp(output[3] - output[12], stageRange[8]);
            step[13] = Av1Transform1dMath.Clamp(output[2] - output[13], stageRange[8]);
            step[14] = Av1Transform1dMath.Clamp(output[1] - output[14], stageRange[8]);
            step[15] = Av1Transform1dMath.Clamp(output[0] - output[15], stageRange[8]);
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
            output[0] = Av1Transform1dMath.Clamp(step[0] + step[31], stageRange[9]);
            output[1] = Av1Transform1dMath.Clamp(step[1] + step[30], stageRange[9]);
            output[2] = Av1Transform1dMath.Clamp(step[2] + step[29], stageRange[9]);
            output[3] = Av1Transform1dMath.Clamp(step[3] + step[28], stageRange[9]);
            output[4] = Av1Transform1dMath.Clamp(step[4] + step[27], stageRange[9]);
            output[5] = Av1Transform1dMath.Clamp(step[5] + step[26], stageRange[9]);
            output[6] = Av1Transform1dMath.Clamp(step[6] + step[25], stageRange[9]);
            output[7] = Av1Transform1dMath.Clamp(step[7] + step[24], stageRange[9]);
            output[8] = Av1Transform1dMath.Clamp(step[8] + step[23], stageRange[9]);
            output[9] = Av1Transform1dMath.Clamp(step[9] + step[22], stageRange[9]);
            output[10] = Av1Transform1dMath.Clamp(step[10] + step[21], stageRange[9]);
            output[11] = Av1Transform1dMath.Clamp(step[11] + step[20], stageRange[9]);
            output[12] = Av1Transform1dMath.Clamp(step[12] + step[19], stageRange[9]);
            output[13] = Av1Transform1dMath.Clamp(step[13] + step[18], stageRange[9]);
            output[14] = Av1Transform1dMath.Clamp(step[14] + step[17], stageRange[9]);
            output[15] = Av1Transform1dMath.Clamp(step[15] + step[16], stageRange[9]);
            output[16] = Av1Transform1dMath.Clamp(step[15] - step[16], stageRange[9]);
            output[17] = Av1Transform1dMath.Clamp(step[14] - step[17], stageRange[9]);
            output[18] = Av1Transform1dMath.Clamp(step[13] - step[18], stageRange[9]);
            output[19] = Av1Transform1dMath.Clamp(step[12] - step[19], stageRange[9]);
            output[20] = Av1Transform1dMath.Clamp(step[11] - step[20], stageRange[9]);
            output[21] = Av1Transform1dMath.Clamp(step[10] - step[21], stageRange[9]);
            output[22] = Av1Transform1dMath.Clamp(step[9] - step[22], stageRange[9]);
            output[23] = Av1Transform1dMath.Clamp(step[8] - step[23], stageRange[9]);
            output[24] = Av1Transform1dMath.Clamp(step[7] - step[24], stageRange[9]);
            output[25] = Av1Transform1dMath.Clamp(step[6] - step[25], stageRange[9]);
            output[26] = Av1Transform1dMath.Clamp(step[5] - step[26], stageRange[9]);
            output[27] = Av1Transform1dMath.Clamp(step[4] - step[27], stageRange[9]);
            output[28] = Av1Transform1dMath.Clamp(step[3] - step[28], stageRange[9]);
            output[29] = Av1Transform1dMath.Clamp(step[2] - step[29], stageRange[9]);
            output[30] = Av1Transform1dMath.Clamp(step[1] - step[30], stageRange[9]);
            output[31] = Av1Transform1dMath.Clamp(step[0] - step[31], stageRange[9]);
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

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output.V0 = input.V0;
            output.V2 = input.V8;
            output.V4 = input.V4;
            output.V6 = input.V12;
            output.V8 = input.V2;
            output.V10 = input.V10;
            output.V12 = input.V6;
            output.V14 = input.V14;
            output.V16 = input.V1;
            output.V18 = input.V9;
            output.V20 = input.V5;
            output.V22 = input.V13;
            output.V24 = input.V3;
            output.V26 = input.V11;
            output.V28 = input.V7;
            output.V30 = input.V15;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = output.V8;
            step.V10 = output.V10;
            step.V12 = output.V12;
            step.V14 = output.V14;
            step.V16 = Av1Transform1dMath.MultiplyRound(output.V16, cospi[62], cosBit);
            step.V17 = Av1Transform1dMath.MultiplyRound(output.V30, -cospi[34], cosBit);
            step.V18 = Av1Transform1dMath.MultiplyRound(output.V18, cospi[46], cosBit);
            step.V19 = Av1Transform1dMath.MultiplyRound(output.V28, -cospi[50], cosBit);
            step.V20 = Av1Transform1dMath.MultiplyRound(output.V20, cospi[54], cosBit);
            step.V21 = Av1Transform1dMath.MultiplyRound(output.V26, -cospi[42], cosBit);
            step.V22 = Av1Transform1dMath.MultiplyRound(output.V22, cospi[38], cosBit);
            step.V23 = Av1Transform1dMath.MultiplyRound(output.V24, -cospi[58], cosBit);
            step.V24 = Av1Transform1dMath.MultiplyRound(output.V24, cospi[6], cosBit);
            step.V25 = Av1Transform1dMath.MultiplyRound(output.V22, cospi[26], cosBit);
            step.V26 = Av1Transform1dMath.MultiplyRound(output.V26, cospi[22], cosBit);
            step.V27 = Av1Transform1dMath.MultiplyRound(output.V20, cospi[10], cosBit);
            step.V28 = Av1Transform1dMath.MultiplyRound(output.V28, cospi[14], cosBit);
            step.V29 = Av1Transform1dMath.MultiplyRound(output.V18, cospi[18], cosBit);
            step.V30 = Av1Transform1dMath.MultiplyRound(output.V30, cospi[30], cosBit);
            step.V31 = Av1Transform1dMath.MultiplyRound(output.V16, cospi[2], cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = step.V4;
            output.V6 = step.V6;
            output.V8 = Av1Transform1dMath.MultiplyRound(step.V8, cospi[60], cosBit);
            output.V9 = Av1Transform1dMath.MultiplyRound(step.V14, -cospi[36], cosBit);
            output.V10 = Av1Transform1dMath.MultiplyRound(step.V10, cospi[44], cosBit);
            output.V11 = Av1Transform1dMath.MultiplyRound(step.V12, -cospi[52], cosBit);
            output.V12 = Av1Transform1dMath.MultiplyRound(step.V12, cospi[12], cosBit);
            output.V13 = Av1Transform1dMath.MultiplyRound(step.V10, cospi[20], cosBit);
            output.V14 = Av1Transform1dMath.MultiplyRound(step.V14, cospi[28], cosBit);
            output.V15 = Av1Transform1dMath.MultiplyRound(step.V8, cospi[4], cosBit);
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V17, stageRange[3]);
            output.V17 = Av1Transform1dMath.Clamp(step.V16 - step.V17, stageRange[3]);
            output.V18 = Av1Transform1dMath.Clamp(-step.V18 + step.V19, stageRange[3]);
            output.V19 = Av1Transform1dMath.Clamp(step.V18 + step.V19, stageRange[3]);
            output.V20 = Av1Transform1dMath.Clamp(step.V20 + step.V21, stageRange[3]);
            output.V21 = Av1Transform1dMath.Clamp(step.V20 - step.V21, stageRange[3]);
            output.V22 = Av1Transform1dMath.Clamp(-step.V22 + step.V23, stageRange[3]);
            output.V23 = Av1Transform1dMath.Clamp(step.V22 + step.V23, stageRange[3]);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V25, stageRange[3]);
            output.V25 = Av1Transform1dMath.Clamp(step.V24 - step.V25, stageRange[3]);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V27, stageRange[3]);
            output.V27 = Av1Transform1dMath.Clamp(step.V26 + step.V27, stageRange[3]);
            output.V28 = Av1Transform1dMath.Clamp(step.V28 + step.V29, stageRange[3]);
            output.V29 = Av1Transform1dMath.Clamp(step.V28 - step.V29, stageRange[3]);
            output.V30 = Av1Transform1dMath.Clamp(-step.V30 + step.V31, stageRange[3]);
            output.V31 = Av1Transform1dMath.Clamp(step.V30 + step.V31, stageRange[3]);

            // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = Av1Transform1dMath.MultiplyRound(output.V4, cospi[56], cosBit);
            step.V5 = Av1Transform1dMath.MultiplyRound(output.V6, -cospi[40], cosBit);
            step.V6 = Av1Transform1dMath.MultiplyRound(output.V6, cospi[24], cosBit);
            step.V7 = Av1Transform1dMath.MultiplyRound(output.V4, cospi[8], cosBit);
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V9, stageRange[4]);
            step.V9 = Av1Transform1dMath.Clamp(output.V8 - output.V9, stageRange[4]);
            step.V10 = Av1Transform1dMath.Clamp(-output.V10 + output.V11, stageRange[4]);
            step.V11 = Av1Transform1dMath.Clamp(output.V10 + output.V11, stageRange[4]);
            step.V12 = Av1Transform1dMath.Clamp(output.V12 + output.V13, stageRange[4]);
            step.V13 = Av1Transform1dMath.Clamp(output.V12 - output.V13, stageRange[4]);
            step.V14 = Av1Transform1dMath.Clamp(-output.V14 + output.V15, stageRange[4]);
            step.V15 = Av1Transform1dMath.Clamp(output.V14 + output.V15, stageRange[4]);
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
            output.V0 = Av1Transform1dMath.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V1 = Av1Transform1dMath.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V2 = Av1Transform1dMath.MultiplyRound(step.V2, cospi[48], cosBit);
            output.V3 = Av1Transform1dMath.MultiplyRound(step.V2, cospi[16], cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, stageRange[5]);
            output.V6 = Av1Transform1dMath.Clamp(-step.V6 + step.V7, stageRange[5]);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, stageRange[5]);
            output.V8 = step.V8;
            output.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V19, stageRange[5]);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V18, stageRange[5]);
            output.V18 = Av1Transform1dMath.Clamp(step.V17 - step.V18, stageRange[5]);
            output.V19 = Av1Transform1dMath.Clamp(step.V16 - step.V19, stageRange[5]);
            output.V20 = Av1Transform1dMath.Clamp(-step.V20 + step.V23, stageRange[5]);
            output.V21 = Av1Transform1dMath.Clamp(-step.V21 + step.V22, stageRange[5]);
            output.V22 = Av1Transform1dMath.Clamp(step.V21 + step.V22, stageRange[5]);
            output.V23 = Av1Transform1dMath.Clamp(step.V20 + step.V23, stageRange[5]);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V27, stageRange[5]);
            output.V25 = Av1Transform1dMath.Clamp(step.V25 + step.V26, stageRange[5]);
            output.V26 = Av1Transform1dMath.Clamp(step.V25 - step.V26, stageRange[5]);
            output.V27 = Av1Transform1dMath.Clamp(step.V24 - step.V27, stageRange[5]);
            output.V28 = Av1Transform1dMath.Clamp(-step.V28 + step.V31, stageRange[5]);
            output.V29 = Av1Transform1dMath.Clamp(-step.V29 + step.V30, stageRange[5]);
            output.V30 = Av1Transform1dMath.Clamp(step.V29 + step.V30, stageRange[5]);
            output.V31 = Av1Transform1dMath.Clamp(step.V28 + step.V31, stageRange[5]);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, stageRange[6]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, stageRange[6]);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, stageRange[6]);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, stageRange[6]);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V11, stageRange[6]);
            step.V9 = Av1Transform1dMath.Clamp(output.V9 + output.V10, stageRange[6]);
            step.V10 = Av1Transform1dMath.Clamp(output.V9 - output.V10, stageRange[6]);
            step.V11 = Av1Transform1dMath.Clamp(output.V8 - output.V11, stageRange[6]);
            step.V12 = Av1Transform1dMath.Clamp(-output.V12 + output.V15, stageRange[6]);
            step.V13 = Av1Transform1dMath.Clamp(-output.V13 + output.V14, stageRange[6]);
            step.V14 = Av1Transform1dMath.Clamp(output.V13 + output.V14, stageRange[6]);
            step.V15 = Av1Transform1dMath.Clamp(output.V12 + output.V15, stageRange[6]);
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
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, stageRange[7]);
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V23, stageRange[7]);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V22, stageRange[7]);
            output.V18 = Av1Transform1dMath.Clamp(step.V18 + step.V21, stageRange[7]);
            output.V19 = Av1Transform1dMath.Clamp(step.V19 + step.V20, stageRange[7]);
            output.V20 = Av1Transform1dMath.Clamp(step.V19 - step.V20, stageRange[7]);
            output.V21 = Av1Transform1dMath.Clamp(step.V18 - step.V21, stageRange[7]);
            output.V22 = Av1Transform1dMath.Clamp(step.V17 - step.V22, stageRange[7]);
            output.V23 = Av1Transform1dMath.Clamp(step.V16 - step.V23, stageRange[7]);
            output.V24 = Av1Transform1dMath.Clamp(-step.V24 + step.V31, stageRange[7]);
            output.V25 = Av1Transform1dMath.Clamp(-step.V25 + step.V30, stageRange[7]);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V29, stageRange[7]);
            output.V27 = Av1Transform1dMath.Clamp(-step.V27 + step.V28, stageRange[7]);
            output.V28 = Av1Transform1dMath.Clamp(step.V27 + step.V28, stageRange[7]);
            output.V29 = Av1Transform1dMath.Clamp(step.V26 + step.V29, stageRange[7]);
            output.V30 = Av1Transform1dMath.Clamp(step.V25 + step.V30, stageRange[7]);
            output.V31 = Av1Transform1dMath.Clamp(step.V24 + step.V31, stageRange[7]);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V15, stageRange[8]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V14, stageRange[8]);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V13, stageRange[8]);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V12, stageRange[8]);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V11, stageRange[8]);
            step.V5 = Av1Transform1dMath.Clamp(output.V5 + output.V10, stageRange[8]);
            step.V6 = Av1Transform1dMath.Clamp(output.V6 + output.V9, stageRange[8]);
            step.V7 = Av1Transform1dMath.Clamp(output.V7 + output.V8, stageRange[8]);
            step.V8 = Av1Transform1dMath.Clamp(output.V7 - output.V8, stageRange[8]);
            step.V9 = Av1Transform1dMath.Clamp(output.V6 - output.V9, stageRange[8]);
            step.V10 = Av1Transform1dMath.Clamp(output.V5 - output.V10, stageRange[8]);
            step.V11 = Av1Transform1dMath.Clamp(output.V4 - output.V11, stageRange[8]);
            step.V12 = Av1Transform1dMath.Clamp(output.V3 - output.V12, stageRange[8]);
            step.V13 = Av1Transform1dMath.Clamp(output.V2 - output.V13, stageRange[8]);
            step.V14 = Av1Transform1dMath.Clamp(output.V1 - output.V14, stageRange[8]);
            step.V15 = Av1Transform1dMath.Clamp(output.V0 - output.V15, stageRange[8]);
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
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V31, stageRange[9]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V30, stageRange[9]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V29, stageRange[9]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V28, stageRange[9]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V27, stageRange[9]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V26, stageRange[9]);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V25, stageRange[9]);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V24, stageRange[9]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V23, stageRange[9]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V22, stageRange[9]);
            output.V10 = Av1Transform1dMath.Clamp(step.V10 + step.V21, stageRange[9]);
            output.V11 = Av1Transform1dMath.Clamp(step.V11 + step.V20, stageRange[9]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V19, stageRange[9]);
            output.V13 = Av1Transform1dMath.Clamp(step.V13 + step.V18, stageRange[9]);
            output.V14 = Av1Transform1dMath.Clamp(step.V14 + step.V17, stageRange[9]);
            output.V15 = Av1Transform1dMath.Clamp(step.V15 + step.V16, stageRange[9]);
            output.V16 = Av1Transform1dMath.Clamp(step.V15 - step.V16, stageRange[9]);
            output.V17 = Av1Transform1dMath.Clamp(step.V14 - step.V17, stageRange[9]);
            output.V18 = Av1Transform1dMath.Clamp(step.V13 - step.V18, stageRange[9]);
            output.V19 = Av1Transform1dMath.Clamp(step.V12 - step.V19, stageRange[9]);
            output.V20 = Av1Transform1dMath.Clamp(step.V11 - step.V20, stageRange[9]);
            output.V21 = Av1Transform1dMath.Clamp(step.V10 - step.V21, stageRange[9]);
            output.V22 = Av1Transform1dMath.Clamp(step.V9 - step.V22, stageRange[9]);
            output.V23 = Av1Transform1dMath.Clamp(step.V8 - step.V23, stageRange[9]);
            output.V24 = Av1Transform1dMath.Clamp(step.V7 - step.V24, stageRange[9]);
            output.V25 = Av1Transform1dMath.Clamp(step.V6 - step.V25, stageRange[9]);
            output.V26 = Av1Transform1dMath.Clamp(step.V5 - step.V26, stageRange[9]);
            output.V27 = Av1Transform1dMath.Clamp(step.V4 - step.V27, stageRange[9]);
            output.V28 = Av1Transform1dMath.Clamp(step.V3 - step.V28, stageRange[9]);
            output.V29 = Av1Transform1dMath.Clamp(step.V2 - step.V29, stageRange[9]);
            output.V30 = Av1Transform1dMath.Clamp(step.V1 - step.V30, stageRange[9]);
            output.V31 = Av1Transform1dMath.Clamp(step.V0 - step.V31, stageRange[9]);
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

            // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
            output.V0 = input.V0;
            output.V2 = input.V8;
            output.V4 = input.V4;
            output.V6 = input.V12;
            output.V8 = input.V2;
            output.V10 = input.V10;
            output.V12 = input.V6;
            output.V14 = input.V14;
            output.V16 = input.V1;
            output.V18 = input.V9;
            output.V20 = input.V5;
            output.V22 = input.V13;
            output.V24 = input.V3;
            output.V26 = input.V11;
            output.V28 = input.V7;
            output.V30 = input.V15;

            // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/64 angles.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = output.V4;
            step.V6 = output.V6;
            step.V8 = output.V8;
            step.V10 = output.V10;
            step.V12 = output.V12;
            step.V14 = output.V14;
            step.V16 = Av1Transform1dMath.MultiplyRound(output.V16, cospi[62], cosBit);
            step.V17 = Av1Transform1dMath.MultiplyRound(output.V30, -cospi[34], cosBit);
            step.V18 = Av1Transform1dMath.MultiplyRound(output.V18, cospi[46], cosBit);
            step.V19 = Av1Transform1dMath.MultiplyRound(output.V28, -cospi[50], cosBit);
            step.V20 = Av1Transform1dMath.MultiplyRound(output.V20, cospi[54], cosBit);
            step.V21 = Av1Transform1dMath.MultiplyRound(output.V26, -cospi[42], cosBit);
            step.V22 = Av1Transform1dMath.MultiplyRound(output.V22, cospi[38], cosBit);
            step.V23 = Av1Transform1dMath.MultiplyRound(output.V24, -cospi[58], cosBit);
            step.V24 = Av1Transform1dMath.MultiplyRound(output.V24, cospi[6], cosBit);
            step.V25 = Av1Transform1dMath.MultiplyRound(output.V22, cospi[26], cosBit);
            step.V26 = Av1Transform1dMath.MultiplyRound(output.V26, cospi[22], cosBit);
            step.V27 = Av1Transform1dMath.MultiplyRound(output.V20, cospi[10], cosBit);
            step.V28 = Av1Transform1dMath.MultiplyRound(output.V28, cospi[14], cosBit);
            step.V29 = Av1Transform1dMath.MultiplyRound(output.V18, cospi[18], cosBit);
            step.V30 = Av1Transform1dMath.MultiplyRound(output.V30, cospi[30], cosBit);
            step.V31 = Av1Transform1dMath.MultiplyRound(output.V16, cospi[2], cosBit);

            // Stage 3 reconstructs the first nested groups and combines their adjacent odd terms.
            output.V0 = step.V0;
            output.V2 = step.V2;
            output.V4 = step.V4;
            output.V6 = step.V6;
            output.V8 = Av1Transform1dMath.MultiplyRound(step.V8, cospi[60], cosBit);
            output.V9 = Av1Transform1dMath.MultiplyRound(step.V14, -cospi[36], cosBit);
            output.V10 = Av1Transform1dMath.MultiplyRound(step.V10, cospi[44], cosBit);
            output.V11 = Av1Transform1dMath.MultiplyRound(step.V12, -cospi[52], cosBit);
            output.V12 = Av1Transform1dMath.MultiplyRound(step.V12, cospi[12], cosBit);
            output.V13 = Av1Transform1dMath.MultiplyRound(step.V10, cospi[20], cosBit);
            output.V14 = Av1Transform1dMath.MultiplyRound(step.V14, cospi[28], cosBit);
            output.V15 = Av1Transform1dMath.MultiplyRound(step.V8, cospi[4], cosBit);
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V17, stageRange[3]);
            output.V17 = Av1Transform1dMath.Clamp(step.V16 - step.V17, stageRange[3]);
            output.V18 = Av1Transform1dMath.Clamp(-step.V18 + step.V19, stageRange[3]);
            output.V19 = Av1Transform1dMath.Clamp(step.V18 + step.V19, stageRange[3]);
            output.V20 = Av1Transform1dMath.Clamp(step.V20 + step.V21, stageRange[3]);
            output.V21 = Av1Transform1dMath.Clamp(step.V20 - step.V21, stageRange[3]);
            output.V22 = Av1Transform1dMath.Clamp(-step.V22 + step.V23, stageRange[3]);
            output.V23 = Av1Transform1dMath.Clamp(step.V22 + step.V23, stageRange[3]);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V25, stageRange[3]);
            output.V25 = Av1Transform1dMath.Clamp(step.V24 - step.V25, stageRange[3]);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V27, stageRange[3]);
            output.V27 = Av1Transform1dMath.Clamp(step.V26 + step.V27, stageRange[3]);
            output.V28 = Av1Transform1dMath.Clamp(step.V28 + step.V29, stageRange[3]);
            output.V29 = Av1Transform1dMath.Clamp(step.V28 - step.V29, stageRange[3]);
            output.V30 = Av1Transform1dMath.Clamp(-step.V30 + step.V31, stageRange[3]);
            output.V31 = Av1Transform1dMath.Clamp(step.V30 + step.V31, stageRange[3]);

            // Stage 4 rotates the next odd-frequency level while preserving completed low-frequency lanes.
            step.V0 = output.V0;
            step.V2 = output.V2;
            step.V4 = Av1Transform1dMath.MultiplyRound(output.V4, cospi[56], cosBit);
            step.V5 = Av1Transform1dMath.MultiplyRound(output.V6, -cospi[40], cosBit);
            step.V6 = Av1Transform1dMath.MultiplyRound(output.V6, cospi[24], cosBit);
            step.V7 = Av1Transform1dMath.MultiplyRound(output.V4, cospi[8], cosBit);
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V9, stageRange[4]);
            step.V9 = Av1Transform1dMath.Clamp(output.V8 - output.V9, stageRange[4]);
            step.V10 = Av1Transform1dMath.Clamp(-output.V10 + output.V11, stageRange[4]);
            step.V11 = Av1Transform1dMath.Clamp(output.V10 + output.V11, stageRange[4]);
            step.V12 = Av1Transform1dMath.Clamp(output.V12 + output.V13, stageRange[4]);
            step.V13 = Av1Transform1dMath.Clamp(output.V12 - output.V13, stageRange[4]);
            step.V14 = Av1Transform1dMath.Clamp(-output.V14 + output.V15, stageRange[4]);
            step.V15 = Av1Transform1dMath.Clamp(output.V14 + output.V15, stageRange[4]);
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
            output.V0 = Av1Transform1dMath.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V1 = Av1Transform1dMath.MultiplyRound(step.V0, cospi[32], cosBit);
            output.V2 = Av1Transform1dMath.MultiplyRound(step.V2, cospi[48], cosBit);
            output.V3 = Av1Transform1dMath.MultiplyRound(step.V2, cospi[16], cosBit);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V5, stageRange[5]);
            output.V5 = Av1Transform1dMath.Clamp(step.V4 - step.V5, stageRange[5]);
            output.V6 = Av1Transform1dMath.Clamp(-step.V6 + step.V7, stageRange[5]);
            output.V7 = Av1Transform1dMath.Clamp(step.V6 + step.V7, stageRange[5]);
            output.V8 = step.V8;
            output.V9 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V9, cospi[48], step.V14, cosBit);
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[48], step.V10, -cospi[16], step.V13, cosBit);
            output.V11 = step.V11;
            output.V12 = step.V12;
            output.V13 = Av1Transform1dMath.HalfButterfly(-cospi[16], step.V10, cospi[48], step.V13, cosBit);
            output.V14 = Av1Transform1dMath.HalfButterfly(cospi[48], step.V9, cospi[16], step.V14, cosBit);
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V19, stageRange[5]);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V18, stageRange[5]);
            output.V18 = Av1Transform1dMath.Clamp(step.V17 - step.V18, stageRange[5]);
            output.V19 = Av1Transform1dMath.Clamp(step.V16 - step.V19, stageRange[5]);
            output.V20 = Av1Transform1dMath.Clamp(-step.V20 + step.V23, stageRange[5]);
            output.V21 = Av1Transform1dMath.Clamp(-step.V21 + step.V22, stageRange[5]);
            output.V22 = Av1Transform1dMath.Clamp(step.V21 + step.V22, stageRange[5]);
            output.V23 = Av1Transform1dMath.Clamp(step.V20 + step.V23, stageRange[5]);
            output.V24 = Av1Transform1dMath.Clamp(step.V24 + step.V27, stageRange[5]);
            output.V25 = Av1Transform1dMath.Clamp(step.V25 + step.V26, stageRange[5]);
            output.V26 = Av1Transform1dMath.Clamp(step.V25 - step.V26, stageRange[5]);
            output.V27 = Av1Transform1dMath.Clamp(step.V24 - step.V27, stageRange[5]);
            output.V28 = Av1Transform1dMath.Clamp(-step.V28 + step.V31, stageRange[5]);
            output.V29 = Av1Transform1dMath.Clamp(-step.V29 + step.V30, stageRange[5]);
            output.V30 = Av1Transform1dMath.Clamp(step.V29 + step.V30, stageRange[5]);
            output.V31 = Av1Transform1dMath.Clamp(step.V28 + step.V31, stageRange[5]);

            // Stage 6 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V3, stageRange[6]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V2, stageRange[6]);
            step.V2 = Av1Transform1dMath.Clamp(output.V1 - output.V2, stageRange[6]);
            step.V3 = Av1Transform1dMath.Clamp(output.V0 - output.V3, stageRange[6]);
            step.V4 = output.V4;
            step.V5 = Av1Transform1dMath.HalfButterfly(-cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V6 = Av1Transform1dMath.HalfButterfly(cospi[32], output.V5, cospi[32], output.V6, cosBit);
            step.V7 = output.V7;
            step.V8 = Av1Transform1dMath.Clamp(output.V8 + output.V11, stageRange[6]);
            step.V9 = Av1Transform1dMath.Clamp(output.V9 + output.V10, stageRange[6]);
            step.V10 = Av1Transform1dMath.Clamp(output.V9 - output.V10, stageRange[6]);
            step.V11 = Av1Transform1dMath.Clamp(output.V8 - output.V11, stageRange[6]);
            step.V12 = Av1Transform1dMath.Clamp(-output.V12 + output.V15, stageRange[6]);
            step.V13 = Av1Transform1dMath.Clamp(-output.V13 + output.V14, stageRange[6]);
            step.V14 = Av1Transform1dMath.Clamp(output.V13 + output.V14, stageRange[6]);
            step.V15 = Av1Transform1dMath.Clamp(output.V12 + output.V15, stageRange[6]);
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
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V7, stageRange[7]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V6, stageRange[7]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V5, stageRange[7]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V4, stageRange[7]);
            output.V4 = Av1Transform1dMath.Clamp(step.V3 - step.V4, stageRange[7]);
            output.V5 = Av1Transform1dMath.Clamp(step.V2 - step.V5, stageRange[7]);
            output.V6 = Av1Transform1dMath.Clamp(step.V1 - step.V6, stageRange[7]);
            output.V7 = Av1Transform1dMath.Clamp(step.V0 - step.V7, stageRange[7]);
            output.V8 = step.V8;
            output.V9 = step.V9;
            output.V10 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V11 = Av1Transform1dMath.HalfButterfly(-cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V12 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V11, cospi[32], step.V12, cosBit);
            output.V13 = Av1Transform1dMath.HalfButterfly(cospi[32], step.V10, cospi[32], step.V13, cosBit);
            output.V14 = step.V14;
            output.V15 = step.V15;
            output.V16 = Av1Transform1dMath.Clamp(step.V16 + step.V23, stageRange[7]);
            output.V17 = Av1Transform1dMath.Clamp(step.V17 + step.V22, stageRange[7]);
            output.V18 = Av1Transform1dMath.Clamp(step.V18 + step.V21, stageRange[7]);
            output.V19 = Av1Transform1dMath.Clamp(step.V19 + step.V20, stageRange[7]);
            output.V20 = Av1Transform1dMath.Clamp(step.V19 - step.V20, stageRange[7]);
            output.V21 = Av1Transform1dMath.Clamp(step.V18 - step.V21, stageRange[7]);
            output.V22 = Av1Transform1dMath.Clamp(step.V17 - step.V22, stageRange[7]);
            output.V23 = Av1Transform1dMath.Clamp(step.V16 - step.V23, stageRange[7]);
            output.V24 = Av1Transform1dMath.Clamp(-step.V24 + step.V31, stageRange[7]);
            output.V25 = Av1Transform1dMath.Clamp(-step.V25 + step.V30, stageRange[7]);
            output.V26 = Av1Transform1dMath.Clamp(-step.V26 + step.V29, stageRange[7]);
            output.V27 = Av1Transform1dMath.Clamp(-step.V27 + step.V28, stageRange[7]);
            output.V28 = Av1Transform1dMath.Clamp(step.V27 + step.V28, stageRange[7]);
            output.V29 = Av1Transform1dMath.Clamp(step.V26 + step.V29, stageRange[7]);
            output.V30 = Av1Transform1dMath.Clamp(step.V25 + step.V30, stageRange[7]);
            output.V31 = Av1Transform1dMath.Clamp(step.V24 + step.V31, stageRange[7]);

            // Stage 8 applies the remaining pi/4 rotations before the terminal spatial merge.
            step.V0 = Av1Transform1dMath.Clamp(output.V0 + output.V15, stageRange[8]);
            step.V1 = Av1Transform1dMath.Clamp(output.V1 + output.V14, stageRange[8]);
            step.V2 = Av1Transform1dMath.Clamp(output.V2 + output.V13, stageRange[8]);
            step.V3 = Av1Transform1dMath.Clamp(output.V3 + output.V12, stageRange[8]);
            step.V4 = Av1Transform1dMath.Clamp(output.V4 + output.V11, stageRange[8]);
            step.V5 = Av1Transform1dMath.Clamp(output.V5 + output.V10, stageRange[8]);
            step.V6 = Av1Transform1dMath.Clamp(output.V6 + output.V9, stageRange[8]);
            step.V7 = Av1Transform1dMath.Clamp(output.V7 + output.V8, stageRange[8]);
            step.V8 = Av1Transform1dMath.Clamp(output.V7 - output.V8, stageRange[8]);
            step.V9 = Av1Transform1dMath.Clamp(output.V6 - output.V9, stageRange[8]);
            step.V10 = Av1Transform1dMath.Clamp(output.V5 - output.V10, stageRange[8]);
            step.V11 = Av1Transform1dMath.Clamp(output.V4 - output.V11, stageRange[8]);
            step.V12 = Av1Transform1dMath.Clamp(output.V3 - output.V12, stageRange[8]);
            step.V13 = Av1Transform1dMath.Clamp(output.V2 - output.V13, stageRange[8]);
            step.V14 = Av1Transform1dMath.Clamp(output.V1 - output.V14, stageRange[8]);
            step.V15 = Av1Transform1dMath.Clamp(output.V0 - output.V15, stageRange[8]);
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
            output.V0 = Av1Transform1dMath.Clamp(step.V0 + step.V31, stageRange[9]);
            output.V1 = Av1Transform1dMath.Clamp(step.V1 + step.V30, stageRange[9]);
            output.V2 = Av1Transform1dMath.Clamp(step.V2 + step.V29, stageRange[9]);
            output.V3 = Av1Transform1dMath.Clamp(step.V3 + step.V28, stageRange[9]);
            output.V4 = Av1Transform1dMath.Clamp(step.V4 + step.V27, stageRange[9]);
            output.V5 = Av1Transform1dMath.Clamp(step.V5 + step.V26, stageRange[9]);
            output.V6 = Av1Transform1dMath.Clamp(step.V6 + step.V25, stageRange[9]);
            output.V7 = Av1Transform1dMath.Clamp(step.V7 + step.V24, stageRange[9]);
            output.V8 = Av1Transform1dMath.Clamp(step.V8 + step.V23, stageRange[9]);
            output.V9 = Av1Transform1dMath.Clamp(step.V9 + step.V22, stageRange[9]);
            output.V10 = Av1Transform1dMath.Clamp(step.V10 + step.V21, stageRange[9]);
            output.V11 = Av1Transform1dMath.Clamp(step.V11 + step.V20, stageRange[9]);
            output.V12 = Av1Transform1dMath.Clamp(step.V12 + step.V19, stageRange[9]);
            output.V13 = Av1Transform1dMath.Clamp(step.V13 + step.V18, stageRange[9]);
            output.V14 = Av1Transform1dMath.Clamp(step.V14 + step.V17, stageRange[9]);
            output.V15 = Av1Transform1dMath.Clamp(step.V15 + step.V16, stageRange[9]);
            output.V16 = Av1Transform1dMath.Clamp(step.V15 - step.V16, stageRange[9]);
            output.V17 = Av1Transform1dMath.Clamp(step.V14 - step.V17, stageRange[9]);
            output.V18 = Av1Transform1dMath.Clamp(step.V13 - step.V18, stageRange[9]);
            output.V19 = Av1Transform1dMath.Clamp(step.V12 - step.V19, stageRange[9]);
            output.V20 = Av1Transform1dMath.Clamp(step.V11 - step.V20, stageRange[9]);
            output.V21 = Av1Transform1dMath.Clamp(step.V10 - step.V21, stageRange[9]);
            output.V22 = Av1Transform1dMath.Clamp(step.V9 - step.V22, stageRange[9]);
            output.V23 = Av1Transform1dMath.Clamp(step.V8 - step.V23, stageRange[9]);
            output.V24 = Av1Transform1dMath.Clamp(step.V7 - step.V24, stageRange[9]);
            output.V25 = Av1Transform1dMath.Clamp(step.V6 - step.V25, stageRange[9]);
            output.V26 = Av1Transform1dMath.Clamp(step.V5 - step.V26, stageRange[9]);
            output.V27 = Av1Transform1dMath.Clamp(step.V4 - step.V27, stageRange[9]);
            output.V28 = Av1Transform1dMath.Clamp(step.V3 - step.V28, stageRange[9]);
            output.V29 = Av1Transform1dMath.Clamp(step.V2 - step.V29, stageRange[9]);
            output.V30 = Av1Transform1dMath.Clamp(step.V1 - step.V30, stageRange[9]);
            output.V31 = Av1Transform1dMath.Clamp(step.V0 - step.V31, stageRange[9]);
        }
    }
}
