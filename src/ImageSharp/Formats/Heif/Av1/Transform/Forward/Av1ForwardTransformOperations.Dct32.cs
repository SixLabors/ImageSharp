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
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct32<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        for (int index = 0; index < 16; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[index], input[31 - index], out output[index], out output[31 - index]);
        }

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd pairs.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        for (int index = 0; index < 8; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[index], output[15 - index], out step[index], out step[15 - index]);
        }

        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[20], output[27], out step[20], out step[27], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[21], output[26], out step[21], out step[26], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[22], output[25], out step[22], out step[25], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[23], output[24], out step[23], out step[24], cosBit, in rounding);
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 3 reduces the even half and folds the next odd-frequency groups into butterflies.
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[index], step[7 - index], out output[index], out output[7 - index]);
        }

        output[8] = step[8];
        output[9] = step[9];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], step[10], step[13], out output[10], out output[13], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], step[11], step[12], out output[11], out output[12], cosBit, in rounding);
        output[14] = step[14];
        output[15] = step[15];
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[16 + index], step[23 - index], out output[16 + index], out output[23 - index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[31 - index], step[24 + index], out output[31 - index], out output[24 + index]);
        }

        // Stage 4 continues the factorization as independent eight-sample groups.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[0], output[3], out step[0], out step[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[1], output[2], out step[1], out step[2]);

        step[4] = output[4];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[5], output[6], out step[5], out step[6], cosBit, in rounding);
        step[7] = output[7];
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[8], output[11], out step[8], out step[11]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[9], output[10], out step[9], out step[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[14], output[13], out step[14], out step[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[15], output[12], out step[15], out step[12]);

        step[16] = output[16];
        step[17] = output[17];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], output[18], output[29], out step[18], out step[29], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], output[19], output[28], out step[19], out step[28], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], output[20], output[27], out step[20], out step[27], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], output[21], output[26], out step[21], out step[26], cosBit, in rounding);
        step[22] = output[22];
        step[23] = output[23];
        step[24] = output[24];
        step[25] = output[25];
        step[30] = output[30];
        step[31] = output[31];

        // Stage 5 completes the low-frequency DCT and rotates the first separated odd groups.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[32], cospi[32], step[0], step[1], out output[0], out output[1], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[16], cospi[48], step[3], step[2], out output[2], out output[3], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[4], step[5], out output[4], out output[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[7], step[6], out output[7], out output[6]);

        output[8] = step[8];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], step[9], step[14], out output[9], out output[14], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], step[10], step[13], out output[10], out output[13], cosBit, in rounding);
        output[11] = step[11];
        output[12] = step[12];
        output[15] = step[15];
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[16], step[19], out output[16], out output[19]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[17], step[18], out output[17], out output[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[23], step[20], out output[23], out output[20]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[22], step[21], out output[22], out output[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[24], step[27], out output[24], out output[27]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[25], step[26], out output[25], out output[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[31], step[28], out output[31], out output[28]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[30], step[29], out output[30], out output[29]);

        // Stage 6 merges adjacent odd-frequency terms with the required AV1 sign pattern.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[8], cospi[56], output[7], output[4], out step[4], out step[7], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[40], cospi[24], output[6], output[5], out step[5], out step[6], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[8], output[9], out step[8], out step[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[11], output[10], out step[11], out step[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[12], output[13], out step[12], out step[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[15], output[14], out step[15], out step[14]);

        step[16] = output[16];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[8], cospi[56], output[17], output[30], out step[17], out step[30], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[56], -cospi[8], output[18], output[29], out step[18], out step[29], cosBit, in rounding);
        step[19] = output[19];
        step[20] = output[20];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[40], cospi[24], output[21], output[26], out step[21], out step[26], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[24], -cospi[40], output[22], output[25], out step[22], out step[25], cosBit, in rounding);
        step[23] = output[23];
        step[24] = output[24];
        step[27] = output[27];
        step[28] = output[28];
        step[31] = output[31];

        // Stage 7 applies the pi/32 rotations to the next odd-frequency level.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[4], cospi[60], step[15], step[8], out output[8], out output[15], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[36], cospi[28], step[14], step[9], out output[9], out output[14], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[20], cospi[44], step[13], step[10], out output[10], out output[13], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[52], cospi[12], step[12], step[11], out output[11], out output[12], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[16], step[17], out output[16], out output[17]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[19], step[18], out output[19], out output[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[20], step[21], out output[20], out output[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[23], step[22], out output[23], out output[22]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[24], step[25], out output[24], out output[25]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[27], step[26], out output[27], out output[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[28], step[29], out output[28], out output[29]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[31], step[30], out output[31], out output[30]);

        // Stage 8 merges the final odd-frequency pairs before their terminal rotations.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = output[12];
        step[13] = output[13];
        step[14] = output[14];
        step[15] = output[15];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[2], cospi[62], output[31], output[16], out step[16], out step[31], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[34], cospi[30], output[30], output[17], out step[17], out step[30], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[18], cospi[46], output[29], output[18], out step[18], out step[29], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[50], cospi[14], output[28], output[19], out step[19], out step[28], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[10], cospi[54], output[27], output[20], out step[20], out step[27], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[42], cospi[22], output[26], output[21], out step[21], out step[26], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[26], cospi[38], output[25], output[22], out step[22], out step[25], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[58], cospi[6], output[24], output[23], out step[23], out step[24], cosBit, in rounding);

        // Stage 9 applies the terminal pi/64 rotations and produces the staged coefficient values.
        output[0] = step[0];
        output[1] = step[16];
        output[2] = step[8];
        output[3] = step[24];
        output[4] = step[4];
        output[5] = step[20];
        output[6] = step[12];
        output[7] = step[28];
        output[8] = step[2];
        output[9] = step[18];
        output[10] = step[10];
        output[11] = step[26];
        output[12] = step[6];
        output[13] = step[22];
        output[14] = step[14];
        output[15] = step[30];
        output[16] = step[1];
        output[17] = step[17];
        output[18] = step[9];
        output[19] = step[25];
        output[20] = step[5];
        output[21] = step[21];
        output[22] = step[13];
        output[23] = step[29];
        output[24] = step[3];
        output[25] = step[19];
        output[26] = step[11];
        output[27] = step[27];
        output[28] = step[7];
        output[29] = step[23];
        output[30] = step[15];
        output[31] = step[31];
    }
}
