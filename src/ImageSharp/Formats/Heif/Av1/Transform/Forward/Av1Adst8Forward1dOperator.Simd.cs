// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the eight-point forward ADST operator.
/// </content>
internal readonly partial struct Av1Adst8Forward1dOperator
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

        // Stage 1 reorders and signs the inputs so the ADST can be expressed as symmetric butterflies.
        output[0] = input[0];
        output[1] = -input[7];
        output[2] = -input[3];
        output[3] = input[4];
        output[4] = -input[1];
        output[5] = input[6];
        output[6] = input[2];
        output[7] = -input[5];

        // Stage 2 rotates the middle pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0];
        step[1] = output[1];
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
        step[4] = output[4];
        step[5] = output[5];
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);

        // Stage 3 combines adjacent rotated pairs into four-sample butterflies.
        output[0] = step[0] + step[2];
        output[1] = step[1] + step[3];
        output[2] = step[0] - step[2];
        output[3] = step[1] - step[3];
        output[4] = step[4] + step[6];
        output[5] = step[5] + step[7];
        output[6] = step[4] - step[6];
        output[7] = step[5] - step[7];

        // Stage 4 rotates the upper half by pi/8 and 3pi/8.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);

        // Stage 5 merges both four-sample halves into the complete eight-sample butterfly.
        output[0] = step[0] + step[4];
        output[1] = step[1] + step[5];
        output[2] = step[2] + step[6];
        output[3] = step[3] + step[7];
        output[4] = step[0] - step[4];
        output[5] = step[1] - step[5];
        output[6] = step[2] - step[6];
        output[7] = step[3] - step[7];

        // Stage 6 applies the terminal odd-frequency rotations that define the ADST basis vectors.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[4], output[0], cospi[60], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[60], output[0], -cospi[4], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[20], output[2], cospi[44], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[44], output[2], -cospi[20], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[36], output[4], cospi[28], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[28], output[4], -cospi[36], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[52], output[6], cospi[12], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[12], output[6], -cospi[52], output[7], cosBit);

        // Stage 7 permutes the rotated values into AV1 coefficient order.
        output[0] = step[1];
        output[1] = step[6];
        output[2] = step[3];
        output[3] = step[4];
        output[4] = step[5];
        output[5] = step[2];
        output[6] = step[7];
        output[7] = step[0];
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

        // Stage 1 reorders and signs the inputs so the ADST can be expressed as symmetric butterflies.
        output[0] = input[0];
        output[1] = -input[7];
        output[2] = -input[3];
        output[3] = input[4];
        output[4] = -input[1];
        output[5] = input[6];
        output[6] = input[2];
        output[7] = -input[5];

        // Stage 2 rotates the middle pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0];
        step[1] = output[1];
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
        step[4] = output[4];
        step[5] = output[5];
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);

        // Stage 3 combines adjacent rotated pairs into four-sample butterflies.
        output[0] = step[0] + step[2];
        output[1] = step[1] + step[3];
        output[2] = step[0] - step[2];
        output[3] = step[1] - step[3];
        output[4] = step[4] + step[6];
        output[5] = step[5] + step[7];
        output[6] = step[4] - step[6];
        output[7] = step[5] - step[7];

        // Stage 4 rotates the upper half by pi/8 and 3pi/8.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);

        // Stage 5 merges both four-sample halves into the complete eight-sample butterfly.
        output[0] = step[0] + step[4];
        output[1] = step[1] + step[5];
        output[2] = step[2] + step[6];
        output[3] = step[3] + step[7];
        output[4] = step[0] - step[4];
        output[5] = step[1] - step[5];
        output[6] = step[2] - step[6];
        output[7] = step[3] - step[7];

        // Stage 6 applies the terminal odd-frequency rotations that define the ADST basis vectors.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[4], output[0], cospi[60], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[60], output[0], -cospi[4], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[20], output[2], cospi[44], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[44], output[2], -cospi[20], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[36], output[4], cospi[28], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[28], output[4], -cospi[36], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[52], output[6], cospi[12], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[12], output[6], -cospi[52], output[7], cosBit);

        // Stage 7 permutes the rotated values into AV1 coefficient order.
        output[0] = step[1];
        output[1] = step[6];
        output[2] = step[3];
        output[3] = step[4];
        output[4] = step[5];
        output[5] = step[2];
        output[6] = step[7];
        output[7] = step[0];
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

        // Stage 1 reorders and signs the inputs so the ADST can be expressed as symmetric butterflies.
        output[0] = input[0];
        output[1] = -input[7];
        output[2] = -input[3];
        output[3] = input[4];
        output[4] = -input[1];
        output[5] = input[6];
        output[6] = input[2];
        output[7] = -input[5];

        // Stage 2 rotates the middle pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0];
        step[1] = output[1];
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
        step[4] = output[4];
        step[5] = output[5];
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);

        // Stage 3 combines adjacent rotated pairs into four-sample butterflies.
        output[0] = step[0] + step[2];
        output[1] = step[1] + step[3];
        output[2] = step[0] - step[2];
        output[3] = step[1] - step[3];
        output[4] = step[4] + step[6];
        output[5] = step[5] + step[7];
        output[6] = step[4] - step[6];
        output[7] = step[5] - step[7];

        // Stage 4 rotates the upper half by pi/8 and 3pi/8.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);

        // Stage 5 merges both four-sample halves into the complete eight-sample butterfly.
        output[0] = step[0] + step[4];
        output[1] = step[1] + step[5];
        output[2] = step[2] + step[6];
        output[3] = step[3] + step[7];
        output[4] = step[0] - step[4];
        output[5] = step[1] - step[5];
        output[6] = step[2] - step[6];
        output[7] = step[3] - step[7];

        // Stage 6 applies the terminal odd-frequency rotations that define the ADST basis vectors.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[4], output[0], cospi[60], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[60], output[0], -cospi[4], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[20], output[2], cospi[44], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[44], output[2], -cospi[20], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[36], output[4], cospi[28], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[28], output[4], -cospi[36], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[52], output[6], cospi[12], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[12], output[6], -cospi[52], output[7], cosBit);

        // Stage 7 permutes the rotated values into AV1 coefficient order.
        output[0] = step[1];
        output[1] = step[6];
        output[2] = step[3];
        output[3] = step[4];
        output[4] = step[5];
        output[5] = step[2];
        output[6] = step[7];
        output[7] = step[0];
    }
}
