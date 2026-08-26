// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the forward asymmetric discrete sine transform stage networks.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the four-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    public static void Adst4<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        _ = step;

        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        TValue input0 = input[0];
        TValue input1 = input[1];
        TValue input2 = input[2];
        TValue input3 = input[3];
        TValue input01 = Av1ForwardTransformArithmetic<TValue>.Add(input0, input1);

        // Highway forms x0 + x1 in the native lane width before widening the products. Retaining that intermediate
        // is observable for Int16 overflow and is therefore part of the reference stage network, not an algebraic
        // simplification opportunity.
        output[0] = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);
        output[1] = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);
        output[2] = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

        // The final output is Highway's widened w2 - w0 + 3 * v5 sequence expressed with the same unrounded
        // products. All four outputs then share the single normative fixed-point rounding point.
        output[3] = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[4] - sinpi[1],
            input0,
            -sinpi[1] - sinpi[2],
            input1,
            sinpi[3],
            input2,
            sinpi[2] - sinpi[4],
            input3,
            cosBit,
            in rounding);
    }

    /// <summary>
    /// Applies the eight-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values and first transform-stage buffer.</param>
    /// <param name="step">The second transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Adst8<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);

        // Stage 1 applies the ADST input permutation and signs. The following stages can then use the same adjacent
        // butterfly layout across every scalar and SIMD instantiation.
        output[0] = input[0];
        output[1] = Av1ForwardTransformArithmetic<TValue>.Negate(input[7]);
        output[2] = Av1ForwardTransformArithmetic<TValue>.Negate(input[3]);
        output[3] = input[4];
        output[4] = Av1ForwardTransformArithmetic<TValue>.Negate(input[1]);
        output[5] = input[6];
        output[6] = input[2];
        output[7] = Av1ForwardTransformArithmetic<TValue>.Negate(input[5]);

        // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
        step[0] = output[0];
        step[1] = output[1];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[32], cospi[32], output[2], output[3], out step[2], out step[3], cosBit, in rounding);
        step[4] = output[4];
        step[5] = output[5];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[32], cospi[32], output[6], output[7], out step[6], out step[7], cosBit, in rounding);

        // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
        for (int group = 0; group < 8; group += 4)
        {
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    step[group + i],
                    step[group + i + 2],
                    out output[group + i],
                    out output[group + i + 2]);
            }
        }

        // Stage 4 rotates the upper group by pi/8 and retains the completed lower group.
        for (int i = 0; i < 4; i++)
        {
            step[i] = output[i];
        }

        step[4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[16], output[4], cospi[48], output[5], cosBit, in rounding);
        step[5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[48], output[4], -cospi[16], output[5], cosBit, in rounding);
        step[6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            -cospi[48], output[6], cospi[16], output[7], cosBit, in rounding);
        step[7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[16], output[6], cospi[48], output[7], cosBit, in rounding);

        // Stage 5 creates the four final butterfly pairs spanning the two groups.
        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[i], step[i + 4], out output[i], out output[i + 4]);
        }

        // Stage 6 applies the remaining odd angles. Each result is placed in step for the fixed ADST permutation.
        step[0] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[4], output[0], cospi[60], output[1], cosBit, in rounding);
        step[1] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[60], output[0], -cospi[4], output[1], cosBit, in rounding);
        step[2] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[20], output[2], cospi[44], output[3], cosBit, in rounding);
        step[3] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[44], output[2], -cospi[20], output[3], cosBit, in rounding);
        step[4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[36], output[4], cospi[28], output[5], cosBit, in rounding);
        step[5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[28], output[4], -cospi[36], output[5], cosBit, in rounding);
        step[6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[52], output[6], cospi[12], output[7], cosBit, in rounding);
        step[7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[12], output[6], -cospi[52], output[7], cosBit, in rounding);

        // Stage 7 is the normative ADST output permutation.
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
    /// Applies the sixteen-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values and first transform-stage buffer.</param>
    /// <param name="step">The second transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Adst16<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);

        // Stage 1 applies the bit-reversed ADST input order and its alternating signs.
        output[0] = input[0];
        output[1] = Av1ForwardTransformArithmetic<TValue>.Negate(input[15]);
        output[2] = Av1ForwardTransformArithmetic<TValue>.Negate(input[7]);
        output[3] = input[8];
        output[4] = Av1ForwardTransformArithmetic<TValue>.Negate(input[3]);
        output[5] = input[12];
        output[6] = input[4];
        output[7] = Av1ForwardTransformArithmetic<TValue>.Negate(input[11]);
        output[8] = Av1ForwardTransformArithmetic<TValue>.Negate(input[1]);
        output[9] = input[14];
        output[10] = input[6];
        output[11] = Av1ForwardTransformArithmetic<TValue>.Negate(input[9]);
        output[12] = input[2];
        output[13] = Av1ForwardTransformArithmetic<TValue>.Negate(input[13]);
        output[14] = Av1ForwardTransformArithmetic<TValue>.Negate(input[5]);
        output[15] = input[10];

        // Stage 2 rotates the second pair in each group of four and copies the first pair unchanged.
        for (int group = 0; group < 16; group += 4)
        {
            step[group] = output[group];
            step[group + 1] = output[group + 1];
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                cospi[32],
                cospi[32],
                output[group + 2],
                output[group + 3],
                out step[group + 2],
                out step[group + 3],
                cosBit,
                in rounding);
        }

        // Stage 3 combines adjacent pairs within each group of four.
        for (int group = 0; group < 16; group += 4)
        {
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    step[group + i],
                    step[group + i + 2],
                    out output[group + i],
                    out output[group + i + 2]);
            }
        }

        // Stage 4 rotates the upper pair of each eight-value group by pi/8.
        for (int group = 0; group < 16; group += 8)
        {
            for (int i = 0; i < 4; i++)
            {
                step[group + i] = output[group + i];
            }

            step[group + 4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[16], output[group + 4], cospi[48], output[group + 5], cosBit, in rounding);
            step[group + 5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[48], output[group + 4], -cospi[16], output[group + 5], cosBit, in rounding);
            step[group + 6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                -cospi[48], output[group + 6], cospi[16], output[group + 7], cosBit, in rounding);
            step[group + 7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[16], output[group + 6], cospi[48], output[group + 7], cosBit, in rounding);
        }

        // Stage 5 combines the lower and upper quartets within each eight-value group.
        for (int group = 0; group < 16; group += 8)
        {
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    step[group + i],
                    step[group + i + 4],
                    out output[group + i],
                    out output[group + i + 4]);
            }
        }

        // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
        for (int i = 0; i < 8; i++)
        {
            step[i] = output[i];
        }

        step[8] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[8], output[8], cospi[56], output[9], cosBit, in rounding);
        step[9] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[56], output[8], -cospi[8], output[9], cosBit, in rounding);
        step[10] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[40], output[10], cospi[24], output[11], cosBit, in rounding);
        step[11] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[24], output[10], -cospi[40], output[11], cosBit, in rounding);
        step[12] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            -cospi[56], output[12], cospi[8], output[13], cosBit, in rounding);
        step[13] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[8], output[12], cospi[56], output[13], cosBit, in rounding);
        step[14] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            -cospi[24], output[14], cospi[40], output[15], cosBit, in rounding);
        step[15] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
            cospi[40], output[14], cospi[24], output[15], cosBit, in rounding);

        // Stage 7 creates the eight final butterfly pairs spanning both octets.
        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[i], step[i + 8], out output[i], out output[i + 8]);
        }

        // Stage 8 applies the final odd-angle rotations before the fixed output permutation.
        ReadOnlySpan<int> firstWeights = [2, 10, 18, 26, 34, 42, 50, 58];

        for (int pair = 0; pair < 8; pair++)
        {
            int first = firstWeights[pair];
            int second = 64 - first;
            int index = pair * 2;

            step[index] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[first], output[index], cospi[second], output[index + 1], cosBit, in rounding);
            step[index + 1] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[second], output[index], -cospi[first], output[index + 1], cosBit, in rounding);
        }

        // Stage 9 maps the rotated input to ascending AV1 ADST coefficient order.
        ReadOnlySpan<byte> permutation = [1, 14, 3, 12, 5, 10, 7, 8, 9, 6, 11, 4, 13, 2, 15, 0];

        for (int i = 0; i < 16; i++)
        {
            output[i] = step[permutation[i]];
        }
    }
}
