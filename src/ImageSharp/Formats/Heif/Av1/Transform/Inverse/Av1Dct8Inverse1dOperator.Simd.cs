// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <content>
/// Provides the SIMD kernels for the eight-point inverse DCT operator.
/// </content>
internal readonly partial struct Av1Dct8Inverse1dOperator
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
        output[1] = input[4];
        output[2] = input[2];
        output[3] = input[6];
        output[4] = input[1];
        output[5] = input[5];
        output[6] = input[3];
        output[7] = input[7];

        // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], output[7], cosBit);

        // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
        output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(step[7] - step[6], range);
        output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);

        // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
        stage++;
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
        step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
        step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
        step[7] = output[7];

        // Stage 5 merges the even and odd halves into spatial order and clamps every result.
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
        output[1] = input[4];
        output[2] = input[2];
        output[3] = input[6];
        output[4] = input[1];
        output[5] = input[5];
        output[6] = input[3];
        output[7] = input[7];

        // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], output[7], cosBit);

        // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
        output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(step[7] - step[6], range);
        output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);

        // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
        stage++;
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
        step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
        step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
        step[7] = output[7];

        // Stage 5 merges the even and odd halves into spatial order and clamps every result.
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
        output[1] = input[4];
        output[2] = input[2];
        output[3] = input[6];
        output[4] = input[1];
        output[5] = input[5];
        output[6] = input[3];
        output[7] = input[7];

        // Stage 2 rotates the odd-frequency coefficient pairs by their pi/16 angles.
        stage++;
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], -cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], -cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[40], output[5], cospi[24], output[6], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[8], output[4], cospi[56], output[7], cosBit);

        // Stage 3 reconstructs the even four-point DCT and combines adjacent odd terms.
        stage++;
        byte range = stageRange[stage];
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], -cospi[32], step[1], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], -cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[16], step[2], cospi[48], step[3], cosBit);
        output[4] = Av1Transform1dMath.Clamp(step[4] + step[5], range);
        output[5] = Av1Transform1dMath.Clamp(step[4] - step[5], range);
        output[6] = Av1Transform1dMath.Clamp(step[7] - step[6], range);
        output[7] = Av1Transform1dMath.Clamp(step[6] + step[7], range);

        // Stage 4 completes the even butterflies and applies the remaining pi/4 odd rotation.
        stage++;
        step[0] = Av1Transform1dMath.Clamp(output[0] + output[3], range);
        step[1] = Av1Transform1dMath.Clamp(output[1] + output[2], range);
        step[2] = Av1Transform1dMath.Clamp(output[1] - output[2], range);
        step[3] = Av1Transform1dMath.Clamp(output[0] - output[3], range);
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[5], cospi[32], output[6], cosBit);
        step[7] = output[7];

        // Stage 5 merges the even and odd halves into spatial order and clamps every result.
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
    }
}
