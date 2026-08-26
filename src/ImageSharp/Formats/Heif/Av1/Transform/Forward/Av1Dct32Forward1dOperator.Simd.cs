// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the thirty-two-point forward DCT operator.
/// </content>
internal readonly partial struct Av1Dct32Forward1dOperator
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
        // libaom uses this table only when coefficient-range checking is enabled. The production transform relies on
        // the ranges already established from the coded bit depth and the normative two-dimensional shifts.
        _ = stageRange;

        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        output[0] = input[0] + input[31];
        output[1] = input[1] + input[30];
        output[2] = input[2] + input[29];
        output[3] = input[3] + input[28];
        output[4] = input[4] + input[27];
        output[5] = input[5] + input[26];
        output[6] = input[6] + input[25];
        output[7] = input[7] + input[24];
        output[8] = input[8] + input[23];
        output[9] = input[9] + input[22];
        output[10] = input[10] + input[21];
        output[11] = input[11] + input[20];
        output[12] = input[12] + input[19];
        output[13] = input[13] + input[18];
        output[14] = input[14] + input[17];
        output[15] = input[15] + input[16];
        output[16] = -input[16] + input[15];
        output[17] = -input[17] + input[14];
        output[18] = -input[18] + input[13];
        output[19] = -input[19] + input[12];
        output[20] = -input[20] + input[11];
        output[21] = -input[21] + input[10];
        output[22] = -input[22] + input[9];
        output[23] = -input[23] + input[8];
        output[24] = -input[24] + input[7];
        output[25] = -input[25] + input[6];
        output[26] = -input[26] + input[5];
        output[27] = -input[27] + input[4];
        output[28] = -input[28] + input[3];
        output[29] = -input[29] + input[2];
        output[30] = -input[30] + input[1];
        output[31] = -input[31] + input[0];

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd pairs.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[15];
        step[1] = output[1] + output[14];
        step[2] = output[2] + output[13];
        step[3] = output[3] + output[12];
        step[4] = output[4] + output[11];
        step[5] = output[5] + output[10];
        step[6] = output[6] + output[9];
        step[7] = output[7] + output[8];
        step[8] = -output[8] + output[7];
        step[9] = -output[9] + output[6];
        step[10] = -output[10] + output[5];
        step[11] = -output[11] + output[4];
        step[12] = -output[12] + output[3];
        step[13] = -output[13] + output[2];
        step[14] = -output[14] + output[1];
        step[15] = -output[15] + output[0];
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[20], cospi[32], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[21], cospi[32], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[22], cospi[32], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[23], cospi[32], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[32], output[24], cospi[32], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[32], output[25], cospi[32], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[32], output[26], cospi[32], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[32], output[27], cospi[32], output[20], cosBit);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 3 reduces the even half and folds the next odd-frequency groups into butterflies.
        output[0] = step[0] + step[7];
        output[1] = step[1] + step[6];
        output[2] = step[2] + step[5];
        output[3] = step[3] + step[4];
        output[4] = -step[4] + step[3];
        output[5] = -step[5] + step[2];
        output[6] = -step[6] + step[1];
        output[7] = -step[7] + step[0];
        output[8] = step[8];
        output[9] = step[9];
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[12], cospi[32], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[13], cospi[32], step[10], cosBit);
        output[14] = step[14];
        output[15] = step[15];
        output[16] = step[16] + step[23];
        output[17] = step[17] + step[22];
        output[18] = step[18] + step[21];
        output[19] = step[19] + step[20];
        output[20] = -step[20] + step[19];
        output[21] = -step[21] + step[18];
        output[22] = -step[22] + step[17];
        output[23] = -step[23] + step[16];
        output[24] = -step[24] + step[31];
        output[25] = -step[25] + step[30];
        output[26] = -step[26] + step[29];
        output[27] = -step[27] + step[28];
        output[28] = step[28] + step[27];
        output[29] = step[29] + step[26];
        output[30] = step[30] + step[25];
        output[31] = step[31] + step[24];

        // Stage 4 continues the factorization as independent eight-sample groups.
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];
        step[8] = output[8] + output[11];
        step[9] = output[9] + output[10];
        step[10] = -output[10] + output[9];
        step[11] = -output[11] + output[8];
        step[12] = -output[12] + output[15];
        step[13] = -output[13] + output[14];
        step[14] = output[14] + output[13];
        step[15] = output[15] + output[12];
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
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[48], output[26], -cospi[16], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[48], output[27], -cospi[16], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[16], output[28], cospi[48], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[16], output[29], cospi[48], output[18], cosBit);
        step[30] = output[30];
        step[31] = output[31];

        // Stage 5 completes the low-frequency DCT and rotates the first separated odd groups.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];
        output[8] = step[8];
        output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
        output[11] = step[11];
        output[12] = step[12];
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[48], step[13], -cospi[16], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[16], step[14], cospi[48], step[9], cosBit);
        output[15] = step[15];
        output[16] = step[16] + step[19];
        output[17] = step[17] + step[18];
        output[18] = -step[18] + step[17];
        output[19] = -step[19] + step[16];
        output[20] = -step[20] + step[23];
        output[21] = -step[21] + step[22];
        output[22] = step[22] + step[21];
        output[23] = step[23] + step[20];
        output[24] = step[24] + step[27];
        output[25] = step[25] + step[26];
        output[26] = -step[26] + step[25];
        output[27] = -step[27] + step[24];
        output[28] = -step[28] + step[31];
        output[29] = -step[29] + step[30];
        output[30] = step[30] + step[29];
        output[31] = step[31] + step[28];

        // Stage 6 merges adjacent odd-frequency terms with the required AV1 sign pattern.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);
        step[8] = output[8] + output[9];
        step[9] = -output[9] + output[8];
        step[10] = -output[10] + output[11];
        step[11] = output[11] + output[10];
        step[12] = output[12] + output[13];
        step[13] = -output[13] + output[12];
        step[14] = -output[14] + output[15];
        step[15] = output[15] + output[14];
        step[16] = output[16];
        step[17] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[17], cospi[56], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[18], -cospi[8], output[29], cosBit);
        step[19] = output[19];
        step[20] = output[20];
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[21], cospi[24], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[22], -cospi[40], output[25], cosBit);
        step[23] = output[23];
        step[24] = output[24];
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[24], output[25], -cospi[40], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[40], output[26], cospi[24], output[21], cosBit);
        step[27] = output[27];
        step[28] = output[28];
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[56], output[29], -cospi[8], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[8], output[30], cospi[56], output[17], cosBit);
        step[31] = output[31];

        // Stage 7 applies the pi/32 rotations to the next odd-frequency level.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = Av1Transform1dMath.HalfButterfly(cospi[60], step[8], cospi[4], step[15], cosBit);
        output[9] = Av1Transform1dMath.HalfButterfly(cospi[28], step[9], cospi[36], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(cospi[44], step[10], cospi[20], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(cospi[12], step[11], cospi[52], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[12], step[12], -cospi[52], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[44], step[13], -cospi[20], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[28], step[14], -cospi[36], step[9], cosBit);
        output[15] = Av1Transform1dMath.HalfButterfly(cospi[60], step[15], -cospi[4], step[8], cosBit);
        output[16] = step[16] + step[17];
        output[17] = -step[17] + step[16];
        output[18] = -step[18] + step[19];
        output[19] = step[19] + step[18];
        output[20] = step[20] + step[21];
        output[21] = -step[21] + step[20];
        output[22] = -step[22] + step[23];
        output[23] = step[23] + step[22];
        output[24] = step[24] + step[25];
        output[25] = -step[25] + step[24];
        output[26] = -step[26] + step[27];
        output[27] = step[27] + step[26];
        output[28] = step[28] + step[29];
        output[29] = -step[29] + step[28];
        output[30] = -step[30] + step[31];
        output[31] = step[31] + step[30];

        // Stage 8 merges the final odd-frequency pairs before their terminal rotations.
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
        step[16] = Av1Transform1dMath.HalfButterfly(cospi[62], output[16], cospi[2], output[31], cosBit);
        step[17] = Av1Transform1dMath.HalfButterfly(cospi[30], output[17], cospi[34], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(cospi[46], output[18], cospi[18], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(cospi[14], output[19], cospi[50], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(cospi[54], output[20], cospi[10], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(cospi[22], output[21], cospi[42], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(cospi[38], output[22], cospi[26], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(cospi[6], output[23], cospi[58], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[6], output[24], -cospi[58], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[38], output[25], -cospi[26], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[22], output[26], -cospi[42], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[54], output[27], -cospi[10], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[14], output[28], -cospi[50], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[46], output[29], -cospi[18], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[30], output[30], -cospi[34], output[17], cosBit);
        step[31] = Av1Transform1dMath.HalfButterfly(cospi[62], output[31], -cospi[2], output[16], cosBit);

        // Stage 9 applies the terminal pi/64 rotations and produces the staged coefficient values.
        output[0] = step[0];
        output[1] = step[16];
        output[2] = step[8];
        output[3] = step[24];
        output[4] = step[4];
        output[5] = step[20];
        output[6] = step[12];
        output[7] = step[28];
        output[8] = step[2];
        output[9] = step[18];
        output[10] = step[10];
        output[11] = step[26];
        output[12] = step[6];
        output[13] = step[22];
        output[14] = step[14];
        output[15] = step[30];
        output[16] = step[1];
        output[17] = step[17];
        output[18] = step[9];
        output[19] = step[25];
        output[20] = step[5];
        output[21] = step[21];
        output[22] = step[13];
        output[23] = step[29];
        output[24] = step[3];
        output[25] = step[19];
        output[26] = step[11];
        output[27] = step[27];
        output[28] = step[7];
        output[29] = step[23];
        output[30] = step[15];
        output[31] = step[31];
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector256<int>> input,
        ref Av1TransformVector<Vector256<int>> output,
        ref Av1TransformVector<Vector256<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        // libaom uses this table only when coefficient-range checking is enabled. The production transform relies on
        // the ranges already established from the coded bit depth and the normative two-dimensional shifts.
        _ = stageRange;

        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        output[0] = input[0] + input[31];
        output[1] = input[1] + input[30];
        output[2] = input[2] + input[29];
        output[3] = input[3] + input[28];
        output[4] = input[4] + input[27];
        output[5] = input[5] + input[26];
        output[6] = input[6] + input[25];
        output[7] = input[7] + input[24];
        output[8] = input[8] + input[23];
        output[9] = input[9] + input[22];
        output[10] = input[10] + input[21];
        output[11] = input[11] + input[20];
        output[12] = input[12] + input[19];
        output[13] = input[13] + input[18];
        output[14] = input[14] + input[17];
        output[15] = input[15] + input[16];
        output[16] = -input[16] + input[15];
        output[17] = -input[17] + input[14];
        output[18] = -input[18] + input[13];
        output[19] = -input[19] + input[12];
        output[20] = -input[20] + input[11];
        output[21] = -input[21] + input[10];
        output[22] = -input[22] + input[9];
        output[23] = -input[23] + input[8];
        output[24] = -input[24] + input[7];
        output[25] = -input[25] + input[6];
        output[26] = -input[26] + input[5];
        output[27] = -input[27] + input[4];
        output[28] = -input[28] + input[3];
        output[29] = -input[29] + input[2];
        output[30] = -input[30] + input[1];
        output[31] = -input[31] + input[0];

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd pairs.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[15];
        step[1] = output[1] + output[14];
        step[2] = output[2] + output[13];
        step[3] = output[3] + output[12];
        step[4] = output[4] + output[11];
        step[5] = output[5] + output[10];
        step[6] = output[6] + output[9];
        step[7] = output[7] + output[8];
        step[8] = -output[8] + output[7];
        step[9] = -output[9] + output[6];
        step[10] = -output[10] + output[5];
        step[11] = -output[11] + output[4];
        step[12] = -output[12] + output[3];
        step[13] = -output[13] + output[2];
        step[14] = -output[14] + output[1];
        step[15] = -output[15] + output[0];
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[20], cospi[32], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[21], cospi[32], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[22], cospi[32], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[23], cospi[32], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[32], output[24], cospi[32], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[32], output[25], cospi[32], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[32], output[26], cospi[32], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[32], output[27], cospi[32], output[20], cosBit);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 3 reduces the even half and folds the next odd-frequency groups into butterflies.
        output[0] = step[0] + step[7];
        output[1] = step[1] + step[6];
        output[2] = step[2] + step[5];
        output[3] = step[3] + step[4];
        output[4] = -step[4] + step[3];
        output[5] = -step[5] + step[2];
        output[6] = -step[6] + step[1];
        output[7] = -step[7] + step[0];
        output[8] = step[8];
        output[9] = step[9];
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[12], cospi[32], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[13], cospi[32], step[10], cosBit);
        output[14] = step[14];
        output[15] = step[15];
        output[16] = step[16] + step[23];
        output[17] = step[17] + step[22];
        output[18] = step[18] + step[21];
        output[19] = step[19] + step[20];
        output[20] = -step[20] + step[19];
        output[21] = -step[21] + step[18];
        output[22] = -step[22] + step[17];
        output[23] = -step[23] + step[16];
        output[24] = -step[24] + step[31];
        output[25] = -step[25] + step[30];
        output[26] = -step[26] + step[29];
        output[27] = -step[27] + step[28];
        output[28] = step[28] + step[27];
        output[29] = step[29] + step[26];
        output[30] = step[30] + step[25];
        output[31] = step[31] + step[24];

        // Stage 4 continues the factorization as independent eight-sample groups.
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];
        step[8] = output[8] + output[11];
        step[9] = output[9] + output[10];
        step[10] = -output[10] + output[9];
        step[11] = -output[11] + output[8];
        step[12] = -output[12] + output[15];
        step[13] = -output[13] + output[14];
        step[14] = output[14] + output[13];
        step[15] = output[15] + output[12];
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
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[48], output[26], -cospi[16], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[48], output[27], -cospi[16], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[16], output[28], cospi[48], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[16], output[29], cospi[48], output[18], cosBit);
        step[30] = output[30];
        step[31] = output[31];

        // Stage 5 completes the low-frequency DCT and rotates the first separated odd groups.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];
        output[8] = step[8];
        output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
        output[11] = step[11];
        output[12] = step[12];
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[48], step[13], -cospi[16], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[16], step[14], cospi[48], step[9], cosBit);
        output[15] = step[15];
        output[16] = step[16] + step[19];
        output[17] = step[17] + step[18];
        output[18] = -step[18] + step[17];
        output[19] = -step[19] + step[16];
        output[20] = -step[20] + step[23];
        output[21] = -step[21] + step[22];
        output[22] = step[22] + step[21];
        output[23] = step[23] + step[20];
        output[24] = step[24] + step[27];
        output[25] = step[25] + step[26];
        output[26] = -step[26] + step[25];
        output[27] = -step[27] + step[24];
        output[28] = -step[28] + step[31];
        output[29] = -step[29] + step[30];
        output[30] = step[30] + step[29];
        output[31] = step[31] + step[28];

        // Stage 6 merges adjacent odd-frequency terms with the required AV1 sign pattern.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);
        step[8] = output[8] + output[9];
        step[9] = -output[9] + output[8];
        step[10] = -output[10] + output[11];
        step[11] = output[11] + output[10];
        step[12] = output[12] + output[13];
        step[13] = -output[13] + output[12];
        step[14] = -output[14] + output[15];
        step[15] = output[15] + output[14];
        step[16] = output[16];
        step[17] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[17], cospi[56], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[18], -cospi[8], output[29], cosBit);
        step[19] = output[19];
        step[20] = output[20];
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[21], cospi[24], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[22], -cospi[40], output[25], cosBit);
        step[23] = output[23];
        step[24] = output[24];
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[24], output[25], -cospi[40], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[40], output[26], cospi[24], output[21], cosBit);
        step[27] = output[27];
        step[28] = output[28];
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[56], output[29], -cospi[8], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[8], output[30], cospi[56], output[17], cosBit);
        step[31] = output[31];

        // Stage 7 applies the pi/32 rotations to the next odd-frequency level.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = Av1Transform1dMath.HalfButterfly(cospi[60], step[8], cospi[4], step[15], cosBit);
        output[9] = Av1Transform1dMath.HalfButterfly(cospi[28], step[9], cospi[36], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(cospi[44], step[10], cospi[20], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(cospi[12], step[11], cospi[52], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[12], step[12], -cospi[52], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[44], step[13], -cospi[20], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[28], step[14], -cospi[36], step[9], cosBit);
        output[15] = Av1Transform1dMath.HalfButterfly(cospi[60], step[15], -cospi[4], step[8], cosBit);
        output[16] = step[16] + step[17];
        output[17] = -step[17] + step[16];
        output[18] = -step[18] + step[19];
        output[19] = step[19] + step[18];
        output[20] = step[20] + step[21];
        output[21] = -step[21] + step[20];
        output[22] = -step[22] + step[23];
        output[23] = step[23] + step[22];
        output[24] = step[24] + step[25];
        output[25] = -step[25] + step[24];
        output[26] = -step[26] + step[27];
        output[27] = step[27] + step[26];
        output[28] = step[28] + step[29];
        output[29] = -step[29] + step[28];
        output[30] = -step[30] + step[31];
        output[31] = step[31] + step[30];

        // Stage 8 merges the final odd-frequency pairs before their terminal rotations.
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
        step[16] = Av1Transform1dMath.HalfButterfly(cospi[62], output[16], cospi[2], output[31], cosBit);
        step[17] = Av1Transform1dMath.HalfButterfly(cospi[30], output[17], cospi[34], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(cospi[46], output[18], cospi[18], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(cospi[14], output[19], cospi[50], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(cospi[54], output[20], cospi[10], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(cospi[22], output[21], cospi[42], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(cospi[38], output[22], cospi[26], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(cospi[6], output[23], cospi[58], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[6], output[24], -cospi[58], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[38], output[25], -cospi[26], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[22], output[26], -cospi[42], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[54], output[27], -cospi[10], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[14], output[28], -cospi[50], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[46], output[29], -cospi[18], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[30], output[30], -cospi[34], output[17], cosBit);
        step[31] = Av1Transform1dMath.HalfButterfly(cospi[62], output[31], -cospi[2], output[16], cosBit);

        // Stage 9 applies the terminal pi/64 rotations and produces the staged coefficient values.
        output[0] = step[0];
        output[1] = step[16];
        output[2] = step[8];
        output[3] = step[24];
        output[4] = step[4];
        output[5] = step[20];
        output[6] = step[12];
        output[7] = step[28];
        output[8] = step[2];
        output[9] = step[18];
        output[10] = step[10];
        output[11] = step[26];
        output[12] = step[6];
        output[13] = step[22];
        output[14] = step[14];
        output[15] = step[30];
        output[16] = step[1];
        output[17] = step[17];
        output[18] = step[9];
        output[19] = step[25];
        output[20] = step[5];
        output[21] = step[21];
        output[22] = step[13];
        output[23] = step[29];
        output[24] = step[3];
        output[25] = step[19];
        output[26] = step[11];
        output[27] = step[27];
        output[28] = step[7];
        output[29] = step[23];
        output[30] = step[15];
        output[31] = step[31];
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
        // libaom uses this table only when coefficient-range checking is enabled. The production transform relies on
        // the ranges already established from the coded bit depth and the normative two-dimensional shifts.
        _ = stageRange;

        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        output[0] = input[0] + input[31];
        output[1] = input[1] + input[30];
        output[2] = input[2] + input[29];
        output[3] = input[3] + input[28];
        output[4] = input[4] + input[27];
        output[5] = input[5] + input[26];
        output[6] = input[6] + input[25];
        output[7] = input[7] + input[24];
        output[8] = input[8] + input[23];
        output[9] = input[9] + input[22];
        output[10] = input[10] + input[21];
        output[11] = input[11] + input[20];
        output[12] = input[12] + input[19];
        output[13] = input[13] + input[18];
        output[14] = input[14] + input[17];
        output[15] = input[15] + input[16];
        output[16] = -input[16] + input[15];
        output[17] = -input[17] + input[14];
        output[18] = -input[18] + input[13];
        output[19] = -input[19] + input[12];
        output[20] = -input[20] + input[11];
        output[21] = -input[21] + input[10];
        output[22] = -input[22] + input[9];
        output[23] = -input[23] + input[8];
        output[24] = -input[24] + input[7];
        output[25] = -input[25] + input[6];
        output[26] = -input[26] + input[5];
        output[27] = -input[27] + input[4];
        output[28] = -input[28] + input[3];
        output[29] = -input[29] + input[2];
        output[30] = -input[30] + input[1];
        output[31] = -input[31] + input[0];

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd pairs.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[15];
        step[1] = output[1] + output[14];
        step[2] = output[2] + output[13];
        step[3] = output[3] + output[12];
        step[4] = output[4] + output[11];
        step[5] = output[5] + output[10];
        step[6] = output[6] + output[9];
        step[7] = output[7] + output[8];
        step[8] = -output[8] + output[7];
        step[9] = -output[9] + output[6];
        step[10] = -output[10] + output[5];
        step[11] = -output[11] + output[4];
        step[12] = -output[12] + output[3];
        step[13] = -output[13] + output[2];
        step[14] = -output[14] + output[1];
        step[15] = -output[15] + output[0];
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[20], cospi[32], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[21], cospi[32], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[22], cospi[32], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[23], cospi[32], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[32], output[24], cospi[32], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[32], output[25], cospi[32], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[32], output[26], cospi[32], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[32], output[27], cospi[32], output[20], cosBit);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 3 reduces the even half and folds the next odd-frequency groups into butterflies.
        output[0] = step[0] + step[7];
        output[1] = step[1] + step[6];
        output[2] = step[2] + step[5];
        output[3] = step[3] + step[4];
        output[4] = -step[4] + step[3];
        output[5] = -step[5] + step[2];
        output[6] = -step[6] + step[1];
        output[7] = -step[7] + step[0];
        output[8] = step[8];
        output[9] = step[9];
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[10], cospi[32], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[11], cospi[32], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[32], step[12], cospi[32], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[32], step[13], cospi[32], step[10], cosBit);
        output[14] = step[14];
        output[15] = step[15];
        output[16] = step[16] + step[23];
        output[17] = step[17] + step[22];
        output[18] = step[18] + step[21];
        output[19] = step[19] + step[20];
        output[20] = -step[20] + step[19];
        output[21] = -step[21] + step[18];
        output[22] = -step[22] + step[17];
        output[23] = -step[23] + step[16];
        output[24] = -step[24] + step[31];
        output[25] = -step[25] + step[30];
        output[26] = -step[26] + step[29];
        output[27] = -step[27] + step[28];
        output[28] = step[28] + step[27];
        output[29] = step[29] + step[26];
        output[30] = step[30] + step[25];
        output[31] = step[31] + step[24];

        // Stage 4 continues the factorization as independent eight-sample groups.
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];
        step[8] = output[8] + output[11];
        step[9] = output[9] + output[10];
        step[10] = -output[10] + output[9];
        step[11] = -output[11] + output[8];
        step[12] = -output[12] + output[15];
        step[13] = -output[13] + output[14];
        step[14] = output[14] + output[13];
        step[15] = output[15] + output[12];
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
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[48], output[26], -cospi[16], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[48], output[27], -cospi[16], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[16], output[28], cospi[48], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[16], output[29], cospi[48], output[18], cosBit);
        step[30] = output[30];
        step[31] = output[31];

        // Stage 5 completes the low-frequency DCT and rotates the first separated odd groups.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];
        output[8] = step[8];
        output[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], step[9], cospi[48], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], step[10], -cospi[16], step[13], cosBit);
        output[11] = step[11];
        output[12] = step[12];
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[48], step[13], -cospi[16], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[16], step[14], cospi[48], step[9], cosBit);
        output[15] = step[15];
        output[16] = step[16] + step[19];
        output[17] = step[17] + step[18];
        output[18] = -step[18] + step[17];
        output[19] = -step[19] + step[16];
        output[20] = -step[20] + step[23];
        output[21] = -step[21] + step[22];
        output[22] = step[22] + step[21];
        output[23] = step[23] + step[20];
        output[24] = step[24] + step[27];
        output[25] = step[25] + step[26];
        output[26] = -step[26] + step[25];
        output[27] = -step[27] + step[24];
        output[28] = -step[28] + step[31];
        output[29] = -step[29] + step[30];
        output[30] = step[30] + step[29];
        output[31] = step[31] + step[28];

        // Stage 6 merges adjacent odd-frequency terms with the required AV1 sign pattern.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);
        step[8] = output[8] + output[9];
        step[9] = -output[9] + output[8];
        step[10] = -output[10] + output[11];
        step[11] = output[11] + output[10];
        step[12] = output[12] + output[13];
        step[13] = -output[13] + output[12];
        step[14] = -output[14] + output[15];
        step[15] = output[15] + output[14];
        step[16] = output[16];
        step[17] = Av1Transform1dMath.HalfButterfly(-cospi[8], output[17], cospi[56], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[18], -cospi[8], output[29], cosBit);
        step[19] = output[19];
        step[20] = output[20];
        step[21] = Av1Transform1dMath.HalfButterfly(-cospi[40], output[21], cospi[24], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[22], -cospi[40], output[25], cosBit);
        step[23] = output[23];
        step[24] = output[24];
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[24], output[25], -cospi[40], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[40], output[26], cospi[24], output[21], cosBit);
        step[27] = output[27];
        step[28] = output[28];
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[56], output[29], -cospi[8], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[8], output[30], cospi[56], output[17], cosBit);
        step[31] = output[31];

        // Stage 7 applies the pi/32 rotations to the next odd-frequency level.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = Av1Transform1dMath.HalfButterfly(cospi[60], step[8], cospi[4], step[15], cosBit);
        output[9] = Av1Transform1dMath.HalfButterfly(cospi[28], step[9], cospi[36], step[14], cosBit);
        output[10] = Av1Transform1dMath.HalfButterfly(cospi[44], step[10], cospi[20], step[13], cosBit);
        output[11] = Av1Transform1dMath.HalfButterfly(cospi[12], step[11], cospi[52], step[12], cosBit);
        output[12] = Av1Transform1dMath.HalfButterfly(cospi[12], step[12], -cospi[52], step[11], cosBit);
        output[13] = Av1Transform1dMath.HalfButterfly(cospi[44], step[13], -cospi[20], step[10], cosBit);
        output[14] = Av1Transform1dMath.HalfButterfly(cospi[28], step[14], -cospi[36], step[9], cosBit);
        output[15] = Av1Transform1dMath.HalfButterfly(cospi[60], step[15], -cospi[4], step[8], cosBit);
        output[16] = step[16] + step[17];
        output[17] = -step[17] + step[16];
        output[18] = -step[18] + step[19];
        output[19] = step[19] + step[18];
        output[20] = step[20] + step[21];
        output[21] = -step[21] + step[20];
        output[22] = -step[22] + step[23];
        output[23] = step[23] + step[22];
        output[24] = step[24] + step[25];
        output[25] = -step[25] + step[24];
        output[26] = -step[26] + step[27];
        output[27] = step[27] + step[26];
        output[28] = step[28] + step[29];
        output[29] = -step[29] + step[28];
        output[30] = -step[30] + step[31];
        output[31] = step[31] + step[30];

        // Stage 8 merges the final odd-frequency pairs before their terminal rotations.
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
        step[16] = Av1Transform1dMath.HalfButterfly(cospi[62], output[16], cospi[2], output[31], cosBit);
        step[17] = Av1Transform1dMath.HalfButterfly(cospi[30], output[17], cospi[34], output[30], cosBit);
        step[18] = Av1Transform1dMath.HalfButterfly(cospi[46], output[18], cospi[18], output[29], cosBit);
        step[19] = Av1Transform1dMath.HalfButterfly(cospi[14], output[19], cospi[50], output[28], cosBit);
        step[20] = Av1Transform1dMath.HalfButterfly(cospi[54], output[20], cospi[10], output[27], cosBit);
        step[21] = Av1Transform1dMath.HalfButterfly(cospi[22], output[21], cospi[42], output[26], cosBit);
        step[22] = Av1Transform1dMath.HalfButterfly(cospi[38], output[22], cospi[26], output[25], cosBit);
        step[23] = Av1Transform1dMath.HalfButterfly(cospi[6], output[23], cospi[58], output[24], cosBit);
        step[24] = Av1Transform1dMath.HalfButterfly(cospi[6], output[24], -cospi[58], output[23], cosBit);
        step[25] = Av1Transform1dMath.HalfButterfly(cospi[38], output[25], -cospi[26], output[22], cosBit);
        step[26] = Av1Transform1dMath.HalfButterfly(cospi[22], output[26], -cospi[42], output[21], cosBit);
        step[27] = Av1Transform1dMath.HalfButterfly(cospi[54], output[27], -cospi[10], output[20], cosBit);
        step[28] = Av1Transform1dMath.HalfButterfly(cospi[14], output[28], -cospi[50], output[19], cosBit);
        step[29] = Av1Transform1dMath.HalfButterfly(cospi[46], output[29], -cospi[18], output[18], cosBit);
        step[30] = Av1Transform1dMath.HalfButterfly(cospi[30], output[30], -cospi[34], output[17], cosBit);
        step[31] = Av1Transform1dMath.HalfButterfly(cospi[62], output[31], -cospi[2], output[16], cosBit);

        // Stage 9 applies the terminal pi/64 rotations and produces the staged coefficient values.
        output[0] = step[0];
        output[1] = step[16];
        output[2] = step[8];
        output[3] = step[24];
        output[4] = step[4];
        output[5] = step[20];
        output[6] = step[12];
        output[7] = step[28];
        output[8] = step[2];
        output[9] = step[18];
        output[10] = step[10];
        output[11] = step[26];
        output[12] = step[6];
        output[13] = step[22];
        output[14] = step[14];
        output[15] = step[30];
        output[16] = step[1];
        output[17] = step[17];
        output[18] = step[9];
        output[19] = step[25];
        output[20] = step[5];
        output[21] = step[21];
        output[22] = step[13];
        output[23] = step[29];
        output[24] = step[3];
        output[25] = step[19];
        output[26] = step[11];
        output[27] = step[27];
        output[28] = step[7];
        output[29] = step[23];
        output[30] = step[15];
        output[31] = step[31];
    }
}
