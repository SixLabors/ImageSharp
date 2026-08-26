// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the sixty-four-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Identifies coefficient positions whose final value resides in the first stage buffer.
    /// </summary>
    private const ulong Dct64Buffer0OutputMask =
        (1UL << 2) | (1UL << 6) | (1UL << 8) | (1UL << 10) | (1UL << 14) |
        (1UL << 18) | (1UL << 22) | (1UL << 24) | (1UL << 26) | (1UL << 30) |
        (1UL << 34) | (1UL << 38) | (1UL << 40) | (1UL << 42) | (1UL << 46) |
        (1UL << 50) | (1UL << 54) | (1UL << 56) | (1UL << 58) | (1UL << 62);

    /// <summary>
    /// Gets the stage-nine rotation order for the middle quarter of the sixty-four-point DCT.
    /// </summary>
    private static ReadOnlySpan<byte> Dct64Stage9RotationOrder => [2, 34, 18, 50, 10, 42, 26, 58];

    /// <summary>
    /// Gets the stage-ten rotation order for the upper half of the sixty-four-point DCT.
    /// </summary>
    private static ReadOnlySpan<byte> Dct64Stage10RotationOrder => [1, 33, 17, 49, 9, 41, 25, 57, 5, 37, 21, 53, 13, 45, 29, 61];

    /// <summary>
    /// Gets the mapping from coefficient order to the final staged value.
    /// </summary>
    private static ReadOnlySpan<byte> Dct64OutputOrder =>
    [
        0, 32, 16, 48, 8, 40, 24, 56, 4, 36, 20, 52, 12, 44, 28, 60,
        2, 34, 18, 50, 10, 42, 26, 58, 6, 38, 22, 54, 14, 46, 30, 62,
        1, 33, 17, 49, 9, 41, 25, 57, 5, 37, 21, 53, 13, 45, 29, 61,
        3, 35, 19, 51, 11, 43, 27, 59, 7, 39, 23, 55, 15, 47, 31, 63,
    ];

    /// <summary>
    /// Applies the sixty-four-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct64<TValue>(
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

        // Stage 1 consumes every spatial value before the strided block becomes available for final coefficients.
        for (int i = 0; i < 32; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                Load<TValue>(ref values, inputStride, i),
                Load<TValue>(ref values, inputStride, 63 - i),
                out buffer0[i],
                out buffer0[63 - i]);
        }

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd-frequency pairs.
        for (int i = 0; i < 16; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[i], buffer0[31 - i], out buffer1[i], out buffer1[31 - i]);
        }

        for (int i = 0; i < 8; i++)
        {
            Butterfly(-cospi[32], cospi[32], buffer0[40 + i], buffer0[55 - i], ref buffer1, 40 + i, 55 - i, cosBit, in rounding);
        }

        // Stage 3 reduces the even half and folds the next odd-frequency groups into paired sums and differences.
        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
        }

        for (int i = 0; i < 4; i++)
        {
            Butterfly(-cospi[32], cospi[32], buffer1[20 + i], buffer1[27 - i], ref buffer0, 20 + i, 27 - i, cosBit, in rounding);
        }

        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[32 + i], buffer1[47 - i], out buffer0[32 + i], out buffer0[47 - i]);
        }

        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[63 - i], buffer1[48 + i], out buffer0[63 - i], out buffer0[48 + i]);
        }

        // Stage 4 continues the factorization as independent sixteen-value groups.
        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
        }

        for (int i = 0; i < 2; i++)
        {
            Butterfly(-cospi[32], cospi[32], buffer0[10 + i], buffer0[13 - i], ref buffer1, 10 + i, 13 - i, cosBit, in rounding);
        }

        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            Butterfly(-cospi[16], cospi[48], buffer0[36 + i], buffer0[59 - i], ref buffer1, 36 + i, 59 - i, cosBit, in rounding);
        }

        for (int i = 4; i < 8; i++)
        {
            Butterfly(-cospi[48], -cospi[16], buffer0[36 + i], buffer0[59 - i], ref buffer1, 36 + i, 59 - i, cosBit, in rounding);
        }

        // Stage 5 reduces the sixteen-value groups into the eight-value DCT and ADST building blocks.
        for (int i = 0; i < 2; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
        }

        Butterfly(-cospi[32], cospi[32], buffer1[5], buffer1[6], ref buffer0, 5, 6, cosBit, in rounding);

        for (int i = 0; i < 2; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            Butterfly(-cospi[16], cospi[48], buffer1[18 + i], buffer1[29 - i], ref buffer0, 18 + i, 29 - i, cosBit, in rounding);
        }

        for (int i = 2; i < 4; i++)
        {
            Butterfly(-cospi[48], -cospi[16], buffer1[18 + i], buffer1[29 - i], ref buffer0, 18 + i, 29 - i, cosBit, in rounding);
        }

        for (int i = 0; i < 4; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[32 + i], buffer1[39 - i], out buffer0[32 + i], out buffer0[39 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[47 - i], buffer1[40 + i], out buffer0[47 - i], out buffer0[40 + i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[48 + i], buffer1[55 - i], out buffer0[48 + i], out buffer0[55 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[63 - i], buffer1[56 + i], out buffer0[63 - i], out buffer0[56 + i]);
        }

        // Stage 6 completes the low-frequency DCT and rotates the first separated odd-frequency groups.
        Butterfly(cospi[32], cospi[32], buffer0[0], buffer0[1], ref buffer1, 0, 1, cosBit, in rounding);
        Butterfly(cospi[16], cospi[48], buffer0[3], buffer0[2], ref buffer1, 2, 3, cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
        Butterfly(-cospi[16], cospi[48], buffer0[9], buffer0[14], ref buffer1, 9, 14, cosBit, in rounding);
        Butterfly(-cospi[48], -cospi[16], buffer0[10], buffer0[13], ref buffer1, 10, 13, cosBit, in rounding);

        for (int i = 0; i < 2; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16 + i], buffer0[19 - i], out buffer1[16 + i], out buffer1[19 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[23 - i], buffer0[20 + i], out buffer1[23 - i], out buffer1[20 + i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[24 + i], buffer0[27 - i], out buffer1[24 + i], out buffer1[27 - i]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31 - i], buffer0[28 + i], out buffer1[31 - i], out buffer1[28 + i]);
            Butterfly(-cospi[8], cospi[56], buffer0[34 + i], buffer0[61 - i], ref buffer1, 34 + i, 61 - i, cosBit, in rounding);
            Butterfly(-cospi[40], cospi[24], buffer0[42 + i], buffer0[53 - i], ref buffer1, 42 + i, 53 - i, cosBit, in rounding);
        }

        for (int i = 2; i < 4; i++)
        {
            Butterfly(-cospi[56], -cospi[8], buffer0[34 + i], buffer0[61 - i], ref buffer1, 34 + i, 61 - i, cosBit, in rounding);
            Butterfly(-cospi[24], -cospi[40], buffer0[42 + i], buffer0[53 - i], ref buffer1, 42 + i, 53 - i, cosBit, in rounding);
        }

        // Stage 7 merges adjacent odd-frequency terms with the sign pattern required by the next rotations.
        Butterfly(cospi[8], cospi[56], buffer1[7], buffer1[4], ref buffer0, 4, 7, cosBit, in rounding);
        Butterfly(cospi[40], cospi[24], buffer1[6], buffer1[5], ref buffer0, 5, 6, cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
        Butterfly(-cospi[8], cospi[56], buffer1[17], buffer1[30], ref buffer0, 17, 30, cosBit, in rounding);
        Butterfly(-cospi[56], -cospi[8], buffer1[18], buffer1[29], ref buffer0, 18, 29, cosBit, in rounding);
        Butterfly(-cospi[40], cospi[24], buffer1[21], buffer1[26], ref buffer0, 21, 26, cosBit, in rounding);
        Butterfly(-cospi[24], -cospi[40], buffer1[22], buffer1[25], ref buffer0, 22, 25, cosBit, in rounding);

        for (int group = 0; group < 4; group++)
        {
            int offset = group * 8;

            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    buffer0[32 + offset + i],
                    buffer1[35 + offset - i],
                    out buffer0[32 + offset + i],
                    out buffer0[35 + offset - i]);

                Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                    buffer0[39 + offset - i],
                    buffer1[36 + offset + i],
                    out buffer0[39 + offset - i],
                    out buffer0[36 + offset + i]);
            }
        }

        // Stage 8 applies the next level of odd-frequency rotations.
        Butterfly(cospi[4], cospi[60], buffer0[15], buffer0[8], ref buffer1, 8, 15, cosBit, in rounding);
        Butterfly(cospi[36], cospi[28], buffer0[14], buffer0[9], ref buffer1, 9, 14, cosBit, in rounding);
        Butterfly(cospi[20], cospi[44], buffer0[13], buffer0[10], ref buffer1, 10, 13, cosBit, in rounding);
        Butterfly(cospi[52], cospi[12], buffer0[12], buffer0[11], ref buffer1, 11, 12, cosBit, in rounding);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

        Butterfly(-cospi[4], cospi[60], buffer0[33], buffer0[62], ref buffer1, 33, 62, cosBit, in rounding);
        Butterfly(-cospi[60], -cospi[4], buffer0[34], buffer0[61], ref buffer1, 34, 61, cosBit, in rounding);
        Butterfly(-cospi[36], cospi[28], buffer0[37], buffer0[58], ref buffer1, 37, 58, cosBit, in rounding);
        Butterfly(-cospi[28], -cospi[36], buffer0[38], buffer0[57], ref buffer1, 38, 57, cosBit, in rounding);
        Butterfly(-cospi[20], cospi[44], buffer0[41], buffer0[54], ref buffer1, 41, 54, cosBit, in rounding);
        Butterfly(-cospi[44], -cospi[20], buffer0[42], buffer0[53], ref buffer1, 42, 53, cosBit, in rounding);
        Butterfly(-cospi[52], cospi[12], buffer0[45], buffer0[50], ref buffer1, 45, 50, cosBit, in rounding);
        Butterfly(-cospi[12], -cospi[52], buffer0[46], buffer0[49], ref buffer1, 46, 49, cosBit, in rounding);

        // Stage 9 merges the remaining odd-frequency pairs before their terminal rotations. The table preserves the
        // non-linear rotation order while keeping the constants in compile-time data.
        for (int i = 0; i < 8; i++)
        {
            int low = 16 + i;
            int high = 31 - i;
            int odd = Dct64Stage9RotationOrder[i];
            Butterfly(cospi[odd], cospi[64 - odd], buffer1[high], buffer1[low], ref buffer0, low, high, cosBit, in rounding);
        }

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[32], buffer1[33], out buffer0[32], out buffer0[33]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[35], buffer1[34], out buffer0[35], out buffer0[34]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[36], buffer1[37], out buffer0[36], out buffer0[37]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[39], buffer1[38], out buffer0[39], out buffer0[38]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[40], buffer1[41], out buffer0[40], out buffer0[41]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[43], buffer1[42], out buffer0[43], out buffer0[42]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[44], buffer1[45], out buffer0[44], out buffer0[45]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[47], buffer1[46], out buffer0[47], out buffer0[46]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[48], buffer1[49], out buffer0[48], out buffer0[49]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[51], buffer1[50], out buffer0[51], out buffer0[50]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[52], buffer1[53], out buffer0[52], out buffer0[53]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[55], buffer1[54], out buffer0[55], out buffer0[54]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[56], buffer1[57], out buffer0[56], out buffer0[57]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[59], buffer1[58], out buffer0[59], out buffer0[58]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[60], buffer1[61], out buffer0[60], out buffer0[61]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[63], buffer1[62], out buffer0[63], out buffer0[62]);

        // Stage 10 applies the pi/64 rotations to the penultimate odd-frequency level.
        for (int i = 0; i < 16; i++)
        {
            int low = 32 + i;
            int high = 63 - i;
            int odd = Dct64Stage10RotationOrder[i];
            Butterfly(cospi[odd], cospi[64 - odd], buffer0[high], buffer0[low], ref buffer1, low, high, cosBit, in rounding);
        }

        // Stage 11 applies the terminal permutation. The fused stages omit pass-through copies, so sources 4-7 and
        // 16-31 remain in buffer0 at retirement,
        // while every other source resides in buffer1. The mask maps that ownership through AV1 coefficient order.
        ReadOnlySpan<byte> outputOrder = Dct64OutputOrder;

        for (int i = 0; i < 64; i++)
        {
            int sourceIndex = outputOrder[i];
            TValue value = ((Dct64Buffer0OutputMask >> i) & 1) != 0 ? buffer0[sourceIndex] : buffer1[sourceIndex];

            Store(ref values, outputStride, i, value);
        }
    }
}
