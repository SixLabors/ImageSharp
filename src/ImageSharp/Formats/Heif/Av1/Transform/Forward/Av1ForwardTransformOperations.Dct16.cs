// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the sixteen-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the sixteen-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct16<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[0], input[15], out output[0], out output[15]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[1], input[14], out output[1], out output[14]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[2], input[13], out output[2], out output[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[3], input[12], out output[3], out output[12]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[4], input[11], out output[4], out output[11]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[5], input[10], out output[5], out output[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[6], input[9], out output[6], out output[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[7], input[8], out output[7], out output[8]);

        // Stage 2 factorizes the even half and rotates the central odd pairs by pi/4.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[0], output[7], out step[0], out step[7]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[1], output[6], out step[1], out step[6]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[2], output[5], out step[2], out step[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[3], output[4], out step[3], out step[4]);

        step[8] = output[8];
        step[9] = output[9];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[10], output[13], out step[10], out step[13], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[11], output[12], out step[11], out step[12], cosBit, in rounding);
        step[14] = output[14];
        step[15] = output[15];

        // Stage 3 recursively factorizes both eight-sample groups into four-sample butterflies.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[0], step[3], out output[0], out output[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[1], step[2], out output[1], out output[2]);

        output[4] = step[4];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], step[5], step[6], out output[5], out output[6], cosBit, in rounding);
        output[7] = step[7];
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[8], step[11], out output[8], out output[11]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[9], step[10], out output[9], out output[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[14], step[13], out output[14], out output[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[15], step[12], out output[15], out output[12]);

        // Stage 4 completes the low-frequency four-point DCT and rotates the first odd-frequency pairs.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[32], cospi[32], output[0], output[1], out step[0], out step[1], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[16], cospi[48], output[3], output[2], out step[2], out step[3], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[4], output[5], out step[4], out step[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[7], output[6], out step[7], out step[6]);

        step[8] = output[8];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], output[9], output[14], out step[9], out step[14], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], output[10], output[13], out step[10], out step[13], cosBit, in rounding);
        step[11] = output[11];
        step[12] = output[12];
        step[15] = output[15];

        // Stage 5 combines the remaining odd terms into the sign pattern required by the next rotations.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[8], cospi[56], step[7], step[4], out output[4], out output[7], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[40], cospi[24], step[6], step[5], out output[5], out output[6], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[8], step[9], out output[8], out output[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[11], step[10], out output[11], out output[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[12], step[13], out output[12], out output[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[15], step[14], out output[15], out output[14]);

        // Stage 6 applies the final pi/32 odd-frequency rotations.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[4], cospi[60], output[15], output[8], out step[8], out step[15], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[36], cospi[28], output[14], output[9], out step[9], out step[14], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[20], cospi[44], output[13], output[10], out step[10], out step[13], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[52], cospi[12], output[12], output[11], out step[11], out step[12], cosBit, in rounding);

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
