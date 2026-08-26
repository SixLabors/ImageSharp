// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the eight-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the eight-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct8<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[0], input[7], out output[0], out output[7]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[1], input[6], out output[1], out output[6]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[2], input[5], out output[2], out output[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[3], input[4], out output[3], out output[4]);

        // Stage 2 applies a four-point DCT to the even half and a pi/4 rotation to the middle odd pair.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[0], output[3], out step[0], out step[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[1], output[2], out step[1], out step[2]);

        step[4] = output[4];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[5], output[6], out step[5], out step[6], cosBit, in rounding);
        step[7] = output[7];

        // Stage 3 completes the even transform and combines the odd terms into sum and difference pairs.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[32], cospi[32], step[0], step[1], out output[0], out output[1], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[16], cospi[48], step[3], step[2], out output[2], out output[3], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[4], step[5], out output[4], out output[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[7], step[6], out output[7], out output[6]);

        // Stage 4 rotates the odd-frequency pairs by the remaining pi/16 angles.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[8], cospi[56], output[7], output[4], out step[4], out step[7], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[40], cospi[24], output[6], output[5], out step[5], out step[6], cosBit, in rounding);

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
