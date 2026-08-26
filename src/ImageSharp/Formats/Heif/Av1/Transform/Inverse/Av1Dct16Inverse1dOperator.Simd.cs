// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <content>
/// Provides the SIMD kernels for the sixteen-point inverse DCT operator.
/// </content>
internal readonly partial struct Av1Dct16Inverse1dOperator
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

        // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
        stage++;
        output[0] = input[0];
        output[1] = input[8];
        output[2] = input[4];
        output[3] = input[12];
        output[4] = input[2];
        output[5] = input[10];
        output[6] = input[6];
        output[7] = input[14];
        output[8] = input[1];
        output[9] = input[9];
        output[10] = input[5];
        output[11] = input[13];
        output[12] = input[3];
        output[13] = input[11];
        output[14] = input[7];
        output[15] = input[15];

        // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], -cospi[4], output[15], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], -cospi[36], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], -cospi[20], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], -cospi[52], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[52], output[11], cospi[12], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[20], output[10], cospi[44], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[36], output[9], cospi[28], output[14], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[4], output[8], cospi[60], output[15], cosBit);

        // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], -cospi[8], step[7], cosBit);
        output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], -cospi[40], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[40], step[5], cospi[24], step[6], cosBit);
        output[7] = Av1Transform1dMath.HalfButterfly(cospi[8], step[4], cospi[56], step[7], cosBit);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[9], range);
        output[9] = Av1Transform1dMath.Clamp(step[8] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[11] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[10] + step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[13], range);
        output[13] = Av1Transform1dMath.Clamp(step[12] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[15] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[14] + step[15], range);

        // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);
        step[4] = Av1Transform1dMath.Clamp(output[4] + output[5], range);
        step[5] = Av1Transform1dMath.Clamp(output[4] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[7] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[6] + output[7], range);
        step[8] = output[8];
        step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
        step[11] = output[11];
        step[12] = output[12];
        step[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[10], cospi[48], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[48], output[9], cospi[16], output[14], cosBit);
        step[15] = output[15];

        // Stage 5 widens the reconstructed groups through their next butterfly level.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
        output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
        output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
        output[4] = step[4];
        output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[5], cospi[32], step[6], cosBit);
        output[7] = step[7];
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[11], range);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[10], range);
        output[10] = Av1Transform1dMath.Clamp(step[9] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[8] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[15] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[14] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[13] + step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[12] + step[15], range);

        // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[7], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[6], range);
        step[2] = Av1Transform1dMath.Clamp(output[2] + output[5], range);
        step[3] = Av1Transform1dMath.Clamp(output[3] + output[4], range);
        step[4] = Av1Transform1dMath.Clamp(output[3] - output[4], range);
        step[5] = Av1Transform1dMath.Clamp(output[2] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[1] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[0] - output[7], range);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[11], cospi[32], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[13], cosBit);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 7 merges the even and odd halves into spatial order and clamps every result.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[15], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[14], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[13], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[12], range);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[11], range);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[10], range);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[9], range);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[8], range);
        output[8] = Av1Transform1dMath.Clamp(step[7] - step[8], range);
        output[9] = Av1Transform1dMath.Clamp(step[6] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[5] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[4] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[3] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[2] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[1] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[0] - step[15], range);
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

        // Stage 1 permutes frequency-ordered coefficients into the recursive DCT factorization order.
        stage++;
        output[0] = input[0];
        output[1] = input[8];
        output[2] = input[4];
        output[3] = input[12];
        output[4] = input[2];
        output[5] = input[10];
        output[6] = input[6];
        output[7] = input[14];
        output[8] = input[1];
        output[9] = input[9];
        output[10] = input[5];
        output[11] = input[13];
        output[12] = input[3];
        output[13] = input[11];
        output[14] = input[7];
        output[15] = input[15];

        // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], -cospi[4], output[15], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], -cospi[36], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], -cospi[20], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], -cospi[52], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[52], output[11], cospi[12], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[20], output[10], cospi[44], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[36], output[9], cospi[28], output[14], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[4], output[8], cospi[60], output[15], cosBit);

        // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], -cospi[8], step[7], cosBit);
        output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], -cospi[40], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[40], step[5], cospi[24], step[6], cosBit);
        output[7] = Av1Transform1dMath.HalfButterfly(cospi[8], step[4], cospi[56], step[7], cosBit);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[9], range);
        output[9] = Av1Transform1dMath.Clamp(step[8] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[11] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[10] + step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[13], range);
        output[13] = Av1Transform1dMath.Clamp(step[12] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[15] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[14] + step[15], range);

        // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);
        step[4] = Av1Transform1dMath.Clamp(output[4] + output[5], range);
        step[5] = Av1Transform1dMath.Clamp(output[4] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[7] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[6] + output[7], range);
        step[8] = output[8];
        step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
        step[11] = output[11];
        step[12] = output[12];
        step[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[10], cospi[48], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[48], output[9], cospi[16], output[14], cosBit);
        step[15] = output[15];

        // Stage 5 widens the reconstructed groups through their next butterfly level.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
        output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
        output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
        output[4] = step[4];
        output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[5], cospi[32], step[6], cosBit);
        output[7] = step[7];
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[11], range);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[10], range);
        output[10] = Av1Transform1dMath.Clamp(step[9] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[8] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[15] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[14] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[13] + step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[12] + step[15], range);

        // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[7], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[6], range);
        step[2] = Av1Transform1dMath.Clamp(output[2] + output[5], range);
        step[3] = Av1Transform1dMath.Clamp(output[3] + output[4], range);
        step[4] = Av1Transform1dMath.Clamp(output[3] - output[4], range);
        step[5] = Av1Transform1dMath.Clamp(output[2] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[1] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[0] - output[7], range);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[11], cospi[32], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[13], cosBit);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 7 merges the even and odd halves into spatial order and clamps every result.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[15], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[14], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[13], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[12], range);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[11], range);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[10], range);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[9], range);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[8], range);
        output[8] = Av1Transform1dMath.Clamp(step[7] - step[8], range);
        output[9] = Av1Transform1dMath.Clamp(step[6] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[5] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[4] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[3] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[2] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[1] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[0] - step[15], range);
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
        output[1] = input[8];
        output[2] = input[4];
        output[3] = input[12];
        output[4] = input[2];
        output[5] = input[10];
        output[6] = input[6];
        output[7] = input[14];
        output[8] = input[1];
        output[9] = input[9];
        output[10] = input[5];
        output[11] = input[13];
        output[12] = input[3];
        output[13] = input[11];
        output[14] = input[7];
        output[15] = input[15];

        // Stage 2 rotates the highest odd-frequency coefficient pairs by their pi/32 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], -cospi[4], output[15], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], -cospi[36], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], -cospi[20], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], -cospi[52], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[52], output[11], cospi[12], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[20], output[10], cospi[44], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[36], output[9], cospi[28], output[14], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[4], output[8], cospi[60], output[15], cosBit);

        // Stage 3 reconstructs the embedded eight-point groups and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], -cospi[8], step[7], cosBit);
        output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], -cospi[40], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[40], step[5], cospi[24], step[6], cosBit);
        output[7] = Av1Transform1dMath.HalfButterfly(cospi[8], step[4], cospi[56], step[7], cosBit);
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[9], range);
        output[9] = Av1Transform1dMath.Clamp(step[8] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[11] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[10] + step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[12] + step[13], range);
        output[13] = Av1Transform1dMath.Clamp(step[12] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[15] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[14] + step[15], range);

        // Stage 4 completes the low-frequency four-point DCT and rotates the next odd-frequency pairs.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], -cospi[32], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], -cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[16], output[2], cospi[48], output[3], cosBit);
        step[4] = Av1Transform1dMath.Clamp(output[4] + output[5], range);
        step[5] = Av1Transform1dMath.Clamp(output[4] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[7] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[6] + output[7], range);
        step[8] = output[8];
        step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
        step[11] = output[11];
        step[12] = output[12];
        step[13] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[10], cospi[48], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[48], output[9], cospi[16], output[14], cosBit);
        step[15] = output[15];

        // Stage 5 widens the reconstructed groups through their next butterfly level.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[3], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[2], range);
        output[2] = Av1Transform1dMath.Clamp(step[1] - step[2], range);
        output[3] = Av1Transform1dMath.Clamp(step[0] - step[3], range);
        output[4] = step[4];
        output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[5], cospi[32], step[6], cosBit);
        output[7] = step[7];
        output[8] = Av1Transform1dMath.Clamp(step[8] + step[11], range);
        output[9] = Av1Transform1dMath.Clamp(step[9] + step[10], range);
        output[10] = Av1Transform1dMath.Clamp(step[9] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[8] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[15] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[14] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[13] + step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[12] + step[15], range);

        // Stage 6 applies the remaining pi/4 rotations before the terminal spatial merge.
        stage++;
        range = stageRange[stage];
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[7], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[6], range);
        step[2] = Av1Transform1dMath.Clamp(output[2] + output[5], range);
        step[3] = Av1Transform1dMath.Clamp(output[3] + output[4], range);
        step[4] = Av1Transform1dMath.Clamp(output[3] - output[4], range);
        step[5] = Av1Transform1dMath.Clamp(output[2] - output[5], range);
        step[6] = Av1Transform1dMath.Clamp(output[1] - output[6], range);
        step[7] = Av1Transform1dMath.Clamp(output[0] - output[7], range);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[11], cospi[32], output[12], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[13], cosBit);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 7 merges the even and odd halves into spatial order and clamps every result.
        stage++;
        range = stageRange[stage];
        output[0] = Av1Transform1dMath.Clamp(step[0] + step[15], range);
        output[1] = Av1Transform1dMath.Clamp(step[1] + step[14], range);
        output[2] = Av1Transform1dMath.Clamp(step[2] + step[13], range);
        output[3] = Av1Transform1dMath.Clamp(step[3] + step[12], range);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[11], range);
        output[5] = Av1Transform1dMath.Clamp(step[5] + step[10], range);
        output[6] = Av1Transform1dMath.Clamp(step[6] + step[9], range);
        output[7] = Av1Transform1dMath.Clamp(step[7] + step[8], range);
        output[8] = Av1Transform1dMath.Clamp(step[7] - step[8], range);
        output[9] = Av1Transform1dMath.Clamp(step[6] - step[9], range);
        output[10] = Av1Transform1dMath.Clamp(step[5] - step[10], range);
        output[11] = Av1Transform1dMath.Clamp(step[4] - step[11], range);
        output[12] = Av1Transform1dMath.Clamp(step[3] - step[12], range);
        output[13] = Av1Transform1dMath.Clamp(step[2] - step[13], range);
        output[14] = Av1Transform1dMath.Clamp(step[1] - step[14], range);
        output[15] = Av1Transform1dMath.Clamp(step[0] - step[15], range);
    }
}
