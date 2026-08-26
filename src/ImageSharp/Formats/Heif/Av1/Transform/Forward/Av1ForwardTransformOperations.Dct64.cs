// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the sixty-four-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the sixty-four-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct64<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        // Stage 1 forms mirror-symmetric sums and differences, separating the even and odd DCT terms.
        for (int index = 0; index < 32; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[index], input[63 - index], out output[index], out output[63 - index]);
        }

        // Stage 2 begins the recursive radix-2 factorization and rotates the central odd pairs.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        for (int index = 0; index < 16; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[index], output[31 - index], out step[index], out step[31 - index]);
        }

        step[32] = output[32];
        step[33] = output[33];
        step[34] = output[34];
        step[35] = output[35];
        step[36] = output[36];
        step[37] = output[37];
        step[38] = output[38];
        step[39] = output[39];
        for (int index = 0; index < 8; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                -cospi[32], cospi[32], output[40 + index], output[55 - index], out step[40 + index], out step[55 - index], cosBit, in rounding);
        }

        step[56] = output[56];
        step[57] = output[57];
        step[58] = output[58];
        step[59] = output[59];
        step[60] = output[60];
        step[61] = output[61];
        step[62] = output[62];
        step[63] = output[63];

        // Stage 3 reduces the even half and folds the next odd-frequency groups into butterflies.
        for (int index = 0; index < 8; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[index], step[15 - index], out output[index], out output[15 - index]);
        }

        output[16] = step[16];
        output[17] = step[17];
        output[18] = step[18];
        output[19] = step[19];
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                -cospi[32], cospi[32], step[20 + index], step[27 - index], out output[20 + index], out output[27 - index], cosBit, in rounding);
        }

        output[28] = step[28];
        output[29] = step[29];
        output[30] = step[30];
        output[31] = step[31];
        for (int index = 0; index < 8; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[32 + index], step[47 - index], out output[32 + index], out output[47 - index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[63 - index], step[48 + index], out output[63 - index], out output[48 + index]);
        }

        // Stage 4 continues the factorization as independent sixteen-sample groups.
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[index], output[7 - index], out step[index], out step[7 - index]);
        }

        step[8] = output[8];
        step[9] = output[9];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[10], output[13], out step[10], out step[13], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], output[11], output[12], out step[11], out step[12], cosBit, in rounding);
        step[14] = output[14];
        step[15] = output[15];
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                output[16 + index], output[23 - index], out step[16 + index], out step[23 - index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                output[31 - index], output[24 + index], out step[31 - index], out step[24 + index]);
        }

        step[32] = output[32];
        step[33] = output[33];
        step[34] = output[34];
        step[35] = output[35];
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                -cospi[16], cospi[48], output[36 + index], output[59 - index], out step[36 + index], out step[59 - index], cosBit, in rounding);
            Av1ForwardTransformArithmetic<TValue>.Butterfly(
                -cospi[48], -cospi[16], output[40 + index], output[55 - index], out step[40 + index], out step[55 - index], cosBit, in rounding);
        }

        step[44] = output[44];
        step[45] = output[45];
        step[46] = output[46];
        step[47] = output[47];
        step[48] = output[48];
        step[49] = output[49];
        step[50] = output[50];
        step[51] = output[51];
        step[60] = output[60];
        step[61] = output[61];
        step[62] = output[62];
        step[63] = output[63];

        // Stage 5 reduces those groups into the eight-sample DCT and ADST building blocks.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[0], step[3], out output[0], out output[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[1], step[2], out output[1], out output[2]);

        output[4] = step[4];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[32], cospi[32], step[5], step[6], out output[5], out output[6], cosBit, in rounding);
        output[7] = step[7];
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[8], step[11], out output[8], out output[11]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[9], step[10], out output[9], out output[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[14], step[13], out output[14], out output[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[15], step[12], out output[15], out output[12]);
        output[16] = step[16];
        output[17] = step[17];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], step[18], step[29], out output[18], out output[29], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[16], cospi[48], step[19], step[28], out output[19], out output[28], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], step[20], step[27], out output[20], out output[27], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[48], -cospi[16], step[21], step[26], out output[21], out output[26], cosBit, in rounding);
        output[22] = step[22];
        output[23] = step[23];
        output[24] = step[24];
        output[25] = step[25];
        output[30] = step[30];
        output[31] = step[31];
        for (int index = 0; index < 4; index++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[32 + index], step[39 - index], out output[32 + index], out output[39 - index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[47 - index], step[40 + index], out output[47 - index], out output[40 + index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[48 + index], step[55 - index], out output[48 + index], out output[55 - index]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                step[63 - index], step[56 + index], out output[63 - index], out output[56 + index]);
        }

        // Stage 6 completes the low-frequency DCT and rotates the first separated odd groups.
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
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[16], output[19], out step[16], out step[19]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[17], output[18], out step[17], out step[18]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[23], output[20], out step[23], out step[20]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[22], output[21], out step[22], out step[21]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[24], output[27], out step[24], out step[27]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[25], output[26], out step[25], out step[26]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[31], output[28], out step[31], out step[28]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[30], output[29], out step[30], out step[29]);
        step[32] = output[32];
        step[33] = output[33];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[8], cospi[56], output[34], output[61], out step[34], out step[61], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[8], cospi[56], output[35], output[60], out step[35], out step[60], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[56], -cospi[8], output[36], output[59], out step[36], out step[59], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[56], -cospi[8], output[37], output[58], out step[37], out step[58], cosBit, in rounding);
        step[38] = output[38];
        step[39] = output[39];
        step[40] = output[40];
        step[41] = output[41];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[40], cospi[24], output[42], output[53], out step[42], out step[53], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[40], cospi[24], output[43], output[52], out step[43], out step[52], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[24], -cospi[40], output[44], output[51], out step[44], out step[51], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[24], -cospi[40], output[45], output[50], out step[45], out step[50], cosBit, in rounding);
        step[46] = output[46];
        step[47] = output[47];
        step[48] = output[48];
        step[49] = output[49];
        step[54] = output[54];
        step[55] = output[55];
        step[56] = output[56];
        step[57] = output[57];
        step[62] = output[62];
        step[63] = output[63];

        // Stage 7 merges adjacent odd-frequency terms with the required AV1 sign pattern.
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
        output[16] = step[16];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[8], cospi[56], step[17], step[30], out output[17], out output[30], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[56], -cospi[8], step[18], step[29], out output[18], out output[29], cosBit, in rounding);
        output[19] = step[19];
        output[20] = step[20];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[40], cospi[24], step[21], step[26], out output[21], out output[26], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[24], -cospi[40], step[22], step[25], out output[22], out output[25], cosBit, in rounding);
        output[23] = step[23];
        output[24] = step[24];
        output[27] = step[27];
        output[28] = step[28];
        output[31] = step[31];
        for (int offset = 32; offset < 64; offset += 8)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset], step[offset + 3], out output[offset], out output[offset + 3]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset + 1], step[offset + 2], out output[offset + 1], out output[offset + 2]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset + 7], step[offset + 4], out output[offset + 7], out output[offset + 4]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset + 6], step[offset + 5], out output[offset + 6], out output[offset + 5]);
        }

        // Stage 8 applies the next level of odd-frequency rotations.
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
        for (int offset = 16; offset < 32; offset += 4)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[offset], output[offset + 1], out step[offset], out step[offset + 1]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(output[offset + 3], output[offset + 2], out step[offset + 3], out step[offset + 2]);
        }

        step[32] = output[32];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[4], cospi[60], output[33], output[62], out step[33], out step[62], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[60], -cospi[4], output[34], output[61], out step[34], out step[61], cosBit, in rounding);
        step[35] = output[35];
        step[36] = output[36];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[36], cospi[28], output[37], output[58], out step[37], out step[58], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[28], -cospi[36], output[38], output[57], out step[38], out step[57], cosBit, in rounding);
        step[39] = output[39];
        step[40] = output[40];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[20], cospi[44], output[41], output[54], out step[41], out step[54], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[44], -cospi[20], output[42], output[53], out step[42], out step[53], cosBit, in rounding);
        step[43] = output[43];
        step[44] = output[44];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[52], cospi[12], output[45], output[50], out step[45], out step[50], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(-cospi[12], -cospi[52], output[46], output[49], out step[46], out step[49], cosBit, in rounding);
        step[47] = output[47];
        step[48] = output[48];
        step[51] = output[51];
        step[52] = output[52];
        step[55] = output[55];
        step[56] = output[56];
        step[59] = output[59];
        step[60] = output[60];
        step[63] = output[63];

        // Stage 9 merges the remaining odd-frequency pairs before their terminal rotations.
        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
        output[4] = step[4];
        output[5] = step[5];
        output[6] = step[6];
        output[7] = step[7];
        output[8] = step[8];
        output[9] = step[9];
        output[10] = step[10];
        output[11] = step[11];
        output[12] = step[12];
        output[13] = step[13];
        output[14] = step[14];
        output[15] = step[15];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[2], cospi[62], step[31], step[16], out output[16], out output[31], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[34], cospi[30], step[30], step[17], out output[17], out output[30], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[18], cospi[46], step[29], step[18], out output[18], out output[29], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[50], cospi[14], step[28], step[19], out output[19], out output[28], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[10], cospi[54], step[27], step[20], out output[20], out output[27], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[42], cospi[22], step[26], step[21], out output[21], out output[26], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[26], cospi[38], step[25], step[22], out output[22], out output[25], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[58], cospi[6], step[24], step[23], out output[23], out output[24], cosBit, in rounding);
        for (int offset = 32; offset < 64; offset += 4)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset], step[offset + 1], out output[offset], out output[offset + 1]);
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(step[offset + 3], step[offset + 2], out output[offset + 3], out output[offset + 2]);
        }

        // Stage 10 applies the pi/64 rotations to the penultimate odd-frequency level.
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
        step[16] = output[16];
        step[17] = output[17];
        step[18] = output[18];
        step[19] = output[19];
        step[20] = output[20];
        step[21] = output[21];
        step[22] = output[22];
        step[23] = output[23];
        step[24] = output[24];
        step[25] = output[25];
        step[26] = output[26];
        step[27] = output[27];
        step[28] = output[28];
        step[29] = output[29];
        step[30] = output[30];
        step[31] = output[31];
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[1], cospi[63], output[63], output[32], out step[32], out step[63], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[33], cospi[31], output[62], output[33], out step[33], out step[62], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[17], cospi[47], output[61], output[34], out step[34], out step[61], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[49], cospi[15], output[60], output[35], out step[35], out step[60], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[9], cospi[55], output[59], output[36], out step[36], out step[59], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[41], cospi[23], output[58], output[37], out step[37], out step[58], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[25], cospi[39], output[57], output[38], out step[38], out step[57], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[57], cospi[7], output[56], output[39], out step[39], out step[56], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[5], cospi[59], output[55], output[40], out step[40], out step[55], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[37], cospi[27], output[54], output[41], out step[41], out step[54], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[21], cospi[43], output[53], output[42], out step[42], out step[53], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[53], cospi[11], output[52], output[43], out step[43], out step[52], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[13], cospi[51], output[51], output[44], out step[44], out step[51], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[45], cospi[19], output[50], output[45], out step[45], out step[50], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[29], cospi[35], output[49], output[46], out step[46], out step[49], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[61], cospi[3], output[48], output[47], out step[47], out step[48], cosBit, in rounding);

        // Stage 11 applies the terminal pi/128 rotations and produces the staged coefficient values.
        output[0] = step[0];
        output[1] = step[32];
        output[2] = step[16];
        output[3] = step[48];
        output[4] = step[8];
        output[5] = step[40];
        output[6] = step[24];
        output[7] = step[56];
        output[8] = step[4];
        output[9] = step[36];
        output[10] = step[20];
        output[11] = step[52];
        output[12] = step[12];
        output[13] = step[44];
        output[14] = step[28];
        output[15] = step[60];
        output[16] = step[2];
        output[17] = step[34];
        output[18] = step[18];
        output[19] = step[50];
        output[20] = step[10];
        output[21] = step[42];
        output[22] = step[26];
        output[23] = step[58];
        output[24] = step[6];
        output[25] = step[38];
        output[26] = step[22];
        output[27] = step[54];
        output[28] = step[14];
        output[29] = step[46];
        output[30] = step[30];
        output[31] = step[62];
        output[32] = step[1];
        output[33] = step[33];
        output[34] = step[17];
        output[35] = step[49];
        output[36] = step[9];
        output[37] = step[41];
        output[38] = step[25];
        output[39] = step[57];
        output[40] = step[5];
        output[41] = step[37];
        output[42] = step[21];
        output[43] = step[53];
        output[44] = step[13];
        output[45] = step[45];
        output[46] = step[29];
        output[47] = step[61];
        output[48] = step[3];
        output[49] = step[35];
        output[50] = step[19];
        output[51] = step[51];
        output[52] = step[11];
        output[53] = step[43];
        output[54] = step[27];
        output[55] = step[59];
        output[56] = step[7];
        output[57] = step[39];
        output[58] = step[23];
        output[59] = step[55];
        output[60] = step[15];
        output[61] = step[47];
        output[62] = step[31];
        output[63] = step[63];
    }
}
