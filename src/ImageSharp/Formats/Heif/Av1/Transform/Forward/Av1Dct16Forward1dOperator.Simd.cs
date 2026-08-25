// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the sixteen-point forward DCT operator.
/// </content>
internal readonly partial struct Av1Dct16Forward1dOperator
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
        // libaom uses this table only when coefficient-range checking is enabled. The production transform relies on
        // the ranges already established from the coded bit depth and the normative two-dimensional shifts.
        _ = stageRange;

        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        output[0] = input[0] + input[15];
        output[1] = input[1] + input[14];
        output[2] = input[2] + input[13];
        output[3] = input[3] + input[12];
        output[4] = input[4] + input[11];
        output[5] = input[5] + input[10];
        output[6] = input[6] + input[9];
        output[7] = input[7] + input[8];
        output[8] = -input[8] + input[7];
        output[9] = -input[9] + input[6];
        output[10] = -input[10] + input[5];
        output[11] = -input[11] + input[4];
        output[12] = -input[12] + input[3];
        output[13] = -input[13] + input[2];
        output[14] = -input[14] + input[1];
        output[15] = -input[15] + input[0];

        // Stage 2 factorizes the even half and rotates the central odd pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[7];
        step[1] = output[1] + output[6];
        step[2] = output[2] + output[5];
        step[3] = output[3] + output[4];
        step[4] = -output[4] + output[3];
        step[5] = -output[5] + output[2];
        step[6] = -output[6] + output[1];
        step[7] = -output[7] + output[0];
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[12], cospi[32], output[11], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[13], cospi[32], output[10], cosBit);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 3 recursively factorizes both eight-sample groups into four-sample butterflies.
        output[0] = step[0] + step[3];
        output[1] = step[1] + step[2];
        output[2] = -step[2] + step[1];
        output[3] = -step[3] + step[0];
        output[4] = step[4];
        output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[6], cospi[32], step[5], cosBit);
        output[7] = step[7];
        output[8] = step[8] + step[11];
        output[9] = step[9] + step[10];
        output[10] = -step[10] + step[9];
        output[11] = -step[11] + step[8];
        output[12] = -step[12] + step[15];
        output[13] = -step[13] + step[14];
        output[14] = step[14] + step[13];
        output[15] = step[15] + step[12];

        // Stage 4 completes the low-frequency four-point DCT and rotates the first odd-frequency pairs.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[1], cospi[32], output[0], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[48], output[3], -cospi[16], output[2], cosBit);
        step[4] = output[4] + output[5];
        step[5] = -output[5] + output[4];
        step[6] = -output[6] + output[7];
        step[7] = output[7] + output[6];
        step[8] = output[8];
        step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
        step[11] = output[11];
        step[12] = output[12];
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[13], -cospi[16], output[10], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[9], cosBit);
        step[15] = output[15];

        // Stage 5 combines the remaining odd terms into the sign pattern required by the next rotations.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], cospi[8], step[7], cosBit);
        output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], cospi[40], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[24], step[6], -cospi[40], step[5], cosBit);
        output[7] = Av1Transform1dMath.HalfButterfly(cospi[56], step[7], -cospi[8], step[4], cosBit);
        output[8] = step[8] + step[9];
        output[9] = -step[9] + step[8];
        output[10] = -step[10] + step[11];
        output[11] = step[11] + step[10];
        output[12] = step[12] + step[13];
        output[13] = -step[13] + step[12];
        output[14] = -step[14] + step[15];
        output[15] = step[15] + step[14];

        // Stage 6 applies the final pi/32 odd-frequency rotations.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], cospi[4], output[15], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], cospi[36], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], cospi[20], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], cospi[52], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[12], output[12], -cospi[52], output[11], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[44], output[13], -cospi[20], output[10], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[28], output[14], -cospi[36], output[9], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[60], output[15], -cospi[4], output[8], cosBit);

        // Stage 7 permutes the staged values into ascending AV1 coefficient order.
        output[0] = step[0];
        output[1] = step[8];
        output[2] = step[4];
        output[3] = step[12];
        output[4] = step[2];
        output[5] = step[10];
        output[6] = step[6];
        output[7] = step[14];
        output[8] = step[1];
        output[9] = step[9];
        output[10] = step[5];
        output[11] = step[13];
        output[12] = step[3];
        output[13] = step[11];
        output[14] = step[7];
        output[15] = step[15];
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
        output[0] = input[0] + input[15];
        output[1] = input[1] + input[14];
        output[2] = input[2] + input[13];
        output[3] = input[3] + input[12];
        output[4] = input[4] + input[11];
        output[5] = input[5] + input[10];
        output[6] = input[6] + input[9];
        output[7] = input[7] + input[8];
        output[8] = -input[8] + input[7];
        output[9] = -input[9] + input[6];
        output[10] = -input[10] + input[5];
        output[11] = -input[11] + input[4];
        output[12] = -input[12] + input[3];
        output[13] = -input[13] + input[2];
        output[14] = -input[14] + input[1];
        output[15] = -input[15] + input[0];

        // Stage 2 factorizes the even half and rotates the central odd pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0] + output[7];
        step[1] = output[1] + output[6];
        step[2] = output[2] + output[5];
        step[3] = output[3] + output[4];
        step[4] = -output[4] + output[3];
        step[5] = -output[5] + output[2];
        step[6] = -output[6] + output[1];
        step[7] = -output[7] + output[0];
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[10], cospi[32], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[11], cospi[32], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[32], output[12], cospi[32], output[11], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[32], output[13], cospi[32], output[10], cosBit);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 3 recursively factorizes both eight-sample groups into four-sample butterflies.
        output[0] = step[0] + step[3];
        output[1] = step[1] + step[2];
        output[2] = -step[2] + step[1];
        output[3] = -step[3] + step[0];
        output[4] = step[4];
        output[5] = Av1Transform1dMath.HalfButterfly(-cospi[32], step[5], cospi[32], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[32], step[6], cospi[32], step[5], cosBit);
        output[7] = step[7];
        output[8] = step[8] + step[11];
        output[9] = step[9] + step[10];
        output[10] = -step[10] + step[9];
        output[11] = -step[11] + step[8];
        output[12] = -step[12] + step[15];
        output[13] = -step[13] + step[14];
        output[14] = step[14] + step[13];
        output[15] = step[15] + step[12];

        // Stage 4 completes the low-frequency four-point DCT and rotates the first odd-frequency pairs.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[1], cospi[32], output[0], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[48], output[3], -cospi[16], output[2], cosBit);
        step[4] = output[4] + output[5];
        step[5] = -output[5] + output[4];
        step[6] = -output[6] + output[7];
        step[7] = output[7] + output[6];
        step[8] = output[8];
        step[9] = Av1Transform1dMath.HalfButterfly(-cospi[16], output[9], cospi[48], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[10], -cospi[16], output[13], cosBit);
        step[11] = output[11];
        step[12] = output[12];
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[13], -cospi[16], output[10], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[9], cosBit);
        step[15] = output[15];

        // Stage 5 combines the remaining odd terms into the sign pattern required by the next rotations.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = Av1Transform1dMath.HalfButterfly(cospi[56], step[4], cospi[8], step[7], cosBit);
        output[5] = Av1Transform1dMath.HalfButterfly(cospi[24], step[5], cospi[40], step[6], cosBit);
        output[6] = Av1Transform1dMath.HalfButterfly(cospi[24], step[6], -cospi[40], step[5], cosBit);
        output[7] = Av1Transform1dMath.HalfButterfly(cospi[56], step[7], -cospi[8], step[4], cosBit);
        output[8] = step[8] + step[9];
        output[9] = -step[9] + step[8];
        output[10] = -step[10] + step[11];
        output[11] = step[11] + step[10];
        output[12] = step[12] + step[13];
        output[13] = -step[13] + step[12];
        output[14] = -step[14] + step[15];
        output[15] = step[15] + step[14];

        // Stage 6 applies the final pi/32 odd-frequency rotations.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[60], output[8], cospi[4], output[15], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[28], output[9], cospi[36], output[14], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[44], output[10], cospi[20], output[13], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[12], output[11], cospi[52], output[12], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[12], output[12], -cospi[52], output[11], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[44], output[13], -cospi[20], output[10], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[28], output[14], -cospi[36], output[9], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[60], output[15], -cospi[4], output[8], cosBit);

        // Stage 7 permutes the staged values into ascending AV1 coefficient order.
        output[0] = step[0];
        output[1] = step[8];
        output[2] = step[4];
        output[3] = step[12];
        output[4] = step[2];
        output[5] = step[10];
        output[6] = step[6];
        output[7] = step[14];
        output[8] = step[1];
        output[9] = step[9];
        output[10] = step[5];
        output[11] = step[13];
        output[12] = step[3];
        output[13] = step[11];
        output[14] = step[7];
        output[15] = step[15];
    }
}
