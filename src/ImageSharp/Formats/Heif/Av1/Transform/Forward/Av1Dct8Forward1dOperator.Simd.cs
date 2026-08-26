// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the eight-point forward DCT operator.
/// </content>
internal readonly partial struct Av1Dct8Forward1dOperator
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
        output[0] = input[0] + input[7];
        output[1] = input[1] + input[6];
        output[2] = input[2] + input[5];
        output[3] = input[3] + input[4];
        output[4] = -input[4] + input[3];
        output[5] = -input[5] + input[2];
        output[6] = -input[6] + input[1];
        output[7] = -input[7] + input[0];

        // Stage 2 applies a four-point DCT to the even half and a pi/4 rotation to the middle odd pair.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];

        // Stage 3 completes the even transform and combines the odd terms into sum and difference pairs.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];

        // Stage 4 rotates the odd-frequency pairs by the remaining pi/16 angles.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);

        // Stage 5 permutes the staged values into ascending AV1 coefficient order.
        output[0] = step[0];
        output[1] = step[4];
        output[2] = step[2];
        output[3] = step[6];
        output[4] = step[1];
        output[5] = step[5];
        output[6] = step[3];
        output[7] = step[7];
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
        output[0] = input[0] + input[7];
        output[1] = input[1] + input[6];
        output[2] = input[2] + input[5];
        output[3] = input[3] + input[4];
        output[4] = -input[4] + input[3];
        output[5] = -input[5] + input[2];
        output[6] = -input[6] + input[1];
        output[7] = -input[7] + input[0];

        // Stage 2 applies a four-point DCT to the even half and a pi/4 rotation to the middle odd pair.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];

        // Stage 3 completes the even transform and combines the odd terms into sum and difference pairs.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];

        // Stage 4 rotates the odd-frequency pairs by the remaining pi/16 angles.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);

        // Stage 5 permutes the staged values into ascending AV1 coefficient order.
        output[0] = step[0];
        output[1] = step[4];
        output[2] = step[2];
        output[3] = step[6];
        output[4] = step[1];
        output[5] = step[5];
        output[6] = step[3];
        output[7] = step[7];
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
        output[0] = input[0] + input[7];
        output[1] = input[1] + input[6];
        output[2] = input[2] + input[5];
        output[3] = input[3] + input[4];
        output[4] = -input[4] + input[3];
        output[5] = -input[5] + input[2];
        output[6] = -input[6] + input[1];
        output[7] = -input[7] + input[0];

        // Stage 2 applies a four-point DCT to the even half and a pi/4 rotation to the middle odd pair.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[3];
        step[1] = output[1] + output[2];
        step[2] = -output[2] + output[1];
        step[3] = -output[3] + output[0];
        step[4] = output[4];
        step[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[5], cospi[32], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[5], cosBit);
        step[7] = output[7];

        // Stage 3 completes the even transform and combines the odd terms into sum and difference pairs.
        output[0] = Av1Transform1dMath.HalfButterfly(cospi[32], step[0], cospi[32], step[1], cosBit);
        output[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[1], cospi[32], step[0], cosBit);
        output[2] = Av1Transform1dMath.HalfButterfly(cospi[48], step[2], cospi[16], step[3], cosBit);
        output[3] = Av1Transform1dMath.HalfButterfly(cospi[48], step[3], -cospi[16], step[2], cosBit);
        output[4] = step[4] + step[5];
        output[5] = -step[5] + step[4];
        output[6] = -step[6] + step[7];
        output[7] = step[7] + step[6];

        // Stage 4 rotates the odd-frequency pairs by the remaining pi/16 angles.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[56], output[4], cospi[8], output[7], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[24], output[5], cospi[40], output[6], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[24], output[6], -cospi[40], output[5], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[56], output[7], -cospi[8], output[4], cosBit);

        // Stage 5 permutes the staged values into ascending AV1 coefficient order.
        output[0] = step[0];
        output[1] = step[4];
        output[2] = step[2];
        output[3] = step[6];
        output[4] = step[1];
        output[5] = step[5];
        output[6] = step[3];
        output[7] = step[7];
    }
}
