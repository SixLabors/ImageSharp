// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the thirty-two-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the thirty-two-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct32<TValue>(
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

        // Stage 1 consumes the source block completely before any final coefficient is stored back into it.
        for (int i = 0; i < 16; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                Load<TValue>(ref values, inputStride, i),
                Load<TValue>(ref values, inputStride, 31 - i),
                out buffer1[i],
                out buffer1[31 - i]);
        }

        // Stage 2 starts the recursive radix-2 factorization and rotates the central odd-frequency pairs.
        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
        }

        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer1[20 + i],
                buffer1[27 - i],
                out buffer0[20 + i],
                out buffer0[27 - i],
                cosBit,
                in rounding);
        }

        // Stage 3 reduces the even half and folds the next odd-frequency groups into paired sums and differences.
        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
        }

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[32],
            cospi[32],
            buffer0[10],
            buffer0[13],
            out buffer1[10],
            out buffer1[13],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[32],
            cospi[32],
            buffer0[11],
            buffer0[12],
            out buffer1[11],
            out buffer1[12],
            cosBit,
            in rounding);

        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
        }

        // Stage 4 continues the factorization as independent eight-value groups.
        for (int i = 0; i < 2; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
        }

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[32],
            cospi[32],
            buffer1[5],
            buffer1[6],
            out buffer0[5],
            out buffer0[6],
            cosBit,
            in rounding);

        for (int i = 0; i < 2; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
        }

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[16],
            cospi[48],
            buffer1[18],
            buffer1[29],
            out buffer0[18],
            out buffer0[29],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[16],
            cospi[48],
            buffer1[19],
            buffer1[28],
            out buffer0[19],
            out buffer0[28],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[48],
            -cospi[16],
            buffer1[20],
            buffer1[27],
            out buffer0[20],
            out buffer0[27],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[48],
            -cospi[16],
            buffer1[21],
            buffer1[26],
            out buffer0[21],
            out buffer0[26],
            cosBit,
            in rounding);

        // Stage 5 completes the low-frequency DCT and rotates the first separated odd groups. Final coefficients are
        // retired directly to the block instead of being copied through a third workspace.
        ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
        ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[16],
            cospi[48],
            buffer0[9],
            buffer0[14],
            out buffer1[9],
            out buffer1[14],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[48],
            -cospi[16],
            buffer0[10],
            buffer0[13],
            out buffer1[10],
            out buffer1[13],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

        // Stage 6 merges adjacent odd-frequency terms with the sign pattern required by the next rotations.
        ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
        ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[8],
            cospi[56],
            buffer1[17],
            buffer1[30],
            out buffer0[17],
            out buffer0[30],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[56],
            -cospi[8],
            buffer1[18],
            buffer1[29],
            out buffer0[18],
            out buffer0[29],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[40],
            cospi[24],
            buffer1[21],
            buffer1[26],
            out buffer0[21],
            out buffer0[26],
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[24],
            -cospi[40],
            buffer1[22],
            buffer1[25],
            out buffer0[22],
            out buffer0[25],
            cosBit,
            in rounding);

        // Stage 7 applies the pi/32 rotations to the next odd-frequency level.
        ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
        ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
        ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
        ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

        // Stages 8 and 9 fuse the terminal pi/64 rotations with the output permutation because none of their results
        // are consumed by another arithmetic stage.
        ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
        ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
        ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
        ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
        ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
        ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
        ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
        ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
    }
}
