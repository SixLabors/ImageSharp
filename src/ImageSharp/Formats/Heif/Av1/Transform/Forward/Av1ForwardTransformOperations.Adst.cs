// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the forward asymmetric discrete sine transform stage networks.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Gets the fixed eight-point ADST coefficient permutation.
    /// </summary>
    private static ReadOnlySpan<byte> Adst8OutputOrder => [1, 6, 3, 4, 5, 2, 7, 0];

    /// <summary>
    /// Gets the first cosine index for each final sixteen-point ADST rotation.
    /// </summary>
    private static ReadOnlySpan<byte> Adst16FinalWeights => [2, 10, 18, 26, 34, 42, 50, 58];

    /// <summary>
    /// Gets the fixed sixteen-point ADST coefficient permutation.
    /// </summary>
    private static ReadOnlySpan<byte> Adst16OutputOrder => [1, 14, 3, 12, 5, 10, 7, 8, 9, 6, 11, 4, 13, 2, 15, 0];

    /// <summary>
    /// Applies the four-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The unused first transform-stage buffer.</param>
    /// <param name="buffer1">The unused second transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    public static void Adst4<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
    {
        _ = buffer0;
        _ = buffer1;

        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        TValue input0 = Load<TValue>(ref values, inputStride, 0);
        TValue input1 = Load<TValue>(ref values, inputStride, 1);
        TValue input2 = Load<TValue>(ref values, inputStride, 2);
        TValue input3 = Load<TValue>(ref values, inputStride, 3);
        TValue input01 = Av1ForwardTransformArithmetic<TValue>.Add(input0, input1);

        // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
        TValue output0 = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

        TValue output1 = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

        TValue output2 = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
            sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

        // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
        TValue output3 = Av1ForwardTransformArithmetic<TValue>.MultiplyAddRound(
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

        Store(ref values, outputStride, 0, output0);
        Store(ref values, outputStride, 1, output1);
        Store(ref values, outputStride, 2, output2);
        Store(ref values, outputStride, 3, output3);
    }

    /// <summary>
    /// Applies the eight-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Adst8<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);

        // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
        buffer0[0] = Load<TValue>(ref values, inputStride, 0);
        buffer0[1] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 7));
        buffer0[2] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 3));
        buffer0[3] = Load<TValue>(ref values, inputStride, 4);
        buffer0[4] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 1));
        buffer0[5] = Load<TValue>(ref values, inputStride, 6);
        buffer0[6] = Load<TValue>(ref values, inputStride, 2);
        buffer0[7] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 5));

        // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
        buffer1[0] = buffer0[0];
        buffer1[1] = buffer0[1];
        Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
        buffer1[4] = buffer0[4];
        buffer1[5] = buffer0[5];
        Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

        // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
        for (int group = 0; group < 8; group += 4)
        {
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    buffer1[group + i],
                    buffer1[group + i + 2],
                    out buffer0[group + i],
                    out buffer0[group + i + 2]);
            }
        }

        // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
        for (int i = 0; i < 4; i++)
        {
            buffer1[i] = buffer0[i];
        }

        buffer1[4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
        buffer1[5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
        buffer1[6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
        buffer1[7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

        // Stage 5 creates the four final butterfly pairs spanning the two groups.
        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
        }

        // Stage 6 applies the remaining odd-angle rotations.
        buffer1[0] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
        buffer1[1] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
        buffer1[2] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
        buffer1[3] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
        buffer1[4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
        buffer1[5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
        buffer1[6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
        buffer1[7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

        ReadOnlySpan<byte> outputOrder = Adst8OutputOrder;

        // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
        for (int i = 0; i < 8; i++)
        {
            Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
        }
    }

    /// <summary>
    /// Applies the sixteen-point forward asymmetric discrete sine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Adst16<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
    {
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);

        // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
        buffer0[0] = Load<TValue>(ref values, inputStride, 0);
        buffer0[1] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 15));
        buffer0[2] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 7));
        buffer0[3] = Load<TValue>(ref values, inputStride, 8);
        buffer0[4] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 3));
        buffer0[5] = Load<TValue>(ref values, inputStride, 12);
        buffer0[6] = Load<TValue>(ref values, inputStride, 4);
        buffer0[7] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 11));
        buffer0[8] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 1));
        buffer0[9] = Load<TValue>(ref values, inputStride, 14);
        buffer0[10] = Load<TValue>(ref values, inputStride, 6);
        buffer0[11] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 9));
        buffer0[12] = Load<TValue>(ref values, inputStride, 2);
        buffer0[13] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 13));
        buffer0[14] = Av1ForwardTransformArithmetic<TValue>.Negate(Load<TValue>(ref values, inputStride, 5));
        buffer0[15] = Load<TValue>(ref values, inputStride, 10);

        // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
        for (int group = 0; group < 16; group += 4)
        {
            buffer1[group] = buffer0[group];
            buffer1[group + 1] = buffer0[group + 1];
            Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
        }

        // Stage 3 combines adjacent pairs within each group of four.
        for (int group = 0; group < 16; group += 4)
        {
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    buffer1[group + i],
                    buffer1[group + i + 2],
                    out buffer0[group + i],
                    out buffer0[group + i + 2]);
            }
        }

        // Stage 4 rotates the upper pair of each eight-value group by pi/8.
        for (int group = 0; group < 16; group += 8)
        {
            for (int i = 0; i < 4; i++)
            {
                buffer1[group + i] = buffer0[group + i];
            }

            buffer1[group + 4] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

            buffer1[group + 5] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

            buffer1[group + 6] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

            buffer1[group + 7] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
        }

        // Stage 5 combines the lower and upper quartets within each eight-value group.
        for (int group = 0; group < 16; group += 8)
        {
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    buffer1[group + i],
                    buffer1[group + i + 4],
                    out buffer0[group + i],
                    out buffer0[group + i + 4]);
            }
        }

        // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
        for (int i = 0; i < 8; i++)
        {
            buffer1[i] = buffer0[i];
        }

        buffer1[8] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
        buffer1[9] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
        buffer1[10] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
        buffer1[11] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
        buffer1[12] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
        buffer1[13] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
        buffer1[14] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
        buffer1[15] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

        // Stage 7 creates the eight final butterfly pairs spanning both octets.
        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
        }

        ReadOnlySpan<byte> finalWeights = Adst16FinalWeights;

        // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
        // without allocating a per-call array or duplicating the complementary cosine-index calculation.
        for (int pair = 0; pair < 8; pair++)
        {
            int first = finalWeights[pair];
            int second = 64 - first;
            int index = pair * 2;
            buffer1[index] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

            buffer1[index + 1] = Av1ForwardTransformArithmetic<TValue>.HalfButterfly(
                cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
        }

        ReadOnlySpan<byte> outputOrder = Adst16OutputOrder;

        // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
        for (int i = 0; i < 16; i++)
        {
            Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
        }
    }
}
