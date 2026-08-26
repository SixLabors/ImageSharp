// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the four-point forward DCT operator.
/// </content>
internal readonly partial struct Av1Dct4Forward1dOperator
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
        _ = stageRange;

        // Mirror butterflies separate the even and odd spatial symmetries used by the four DCT basis vectors.
        output[0] = input[0] + input[3];
        output[1] = input[1] + input[2];
        output[2] = input[1] - input[2];
        output[3] = input[0] - input[3];

        // Each half-butterfly keeps the optimized kernels' wrapping 32-bit arithmetic before applying the
        // normative fixed-point rounding shift.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[1], cospi[32], output[0], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[48], output[3], -cospi[16], output[2], cosBit);

        // The staged order groups butterfly partners; AV1 coefficient order interleaves their frequency indices.
        output[0] = step[0];
        output[1] = step[2];
        output[2] = step[1];
        output[3] = step[3];
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector256<int>> input,
        ref Av1TransformVector<Vector256<int>> output,
        ref Av1TransformVector<Vector256<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        _ = stageRange;

        // Mirror butterflies separate the even and odd spatial symmetries used by the four DCT basis vectors.
        output[0] = input[0] + input[3];
        output[1] = input[1] + input[2];
        output[2] = input[1] - input[2];
        output[3] = input[0] - input[3];

        // Each half-butterfly keeps the optimized kernels' wrapping 32-bit arithmetic before applying the
        // normative fixed-point rounding shift.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[1], cospi[32], output[0], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[48], output[3], -cospi[16], output[2], cosBit);

        // The staged order groups butterfly partners; AV1 coefficient order interleaves their frequency indices.
        output[0] = step[0];
        output[1] = step[2];
        output[2] = step[1];
        output[3] = step[3];
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
        _ = stageRange;

        // Mirror butterflies separate the even and odd spatial symmetries used by the four DCT basis vectors.
        output[0] = input[0] + input[3];
        output[1] = input[1] + input[2];
        output[2] = input[1] - input[2];
        output[3] = input[0] - input[3];

        // Each half-butterfly keeps the optimized kernels' wrapping 32-bit arithmetic before applying the
        // normative fixed-point rounding shift.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[32], output[0], cospi[32], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(-cospi[32], output[1], cospi[32], output[0], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[48], output[2], cospi[16], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[48], output[3], -cospi[16], output[2], cosBit);

        // The staged order groups butterfly partners; AV1 coefficient order interleaves their frequency indices.
        output[0] = step[0];
        output[1] = step[2];
        output[2] = step[1];
        output[3] = step[3];
    }
}
