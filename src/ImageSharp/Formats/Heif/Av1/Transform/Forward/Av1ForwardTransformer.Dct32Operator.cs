// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Implements the thirty-two-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the thirty-two-point forward transform for every supported lane width.
    /// </summary>
    internal readonly struct Dct32Operator : IAv1ForwardTransform1dOperator
    {
        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<int> buffer0,
            ref Av1TransformVector<int> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<int>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(
                    Load<int>(ref values, inputStride, i),
                    Load<int>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<int>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
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
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<int>.Butterfly(
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
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<short> buffer0,
            ref Av1TransformVector<short> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<short>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(
                    Load<short>(ref values, inputStride, i),
                    Load<short>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<short>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
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
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<short>.Butterfly(
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
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<short>> buffer0,
            ref Av1TransformVector<Vector128<short>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<short>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                    Load<Vector128<short>>(ref values, inputStride, i),
                    Load<Vector128<short>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<short>> buffer0,
            ref Av1TransformVector<Vector256<short>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<short>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                    Load<Vector256<short>>(ref values, inputStride, i),
                    Load<Vector256<short>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<short>> buffer0,
            ref Av1TransformVector<Vector512<short>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<short>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                    Load<Vector512<short>>(ref values, inputStride, i),
                    Load<Vector512<short>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<int>> buffer0,
            ref Av1TransformVector<Vector128<int>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<int>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                    Load<Vector128<int>>(ref values, inputStride, i),
                    Load<Vector128<int>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<int>> buffer0,
            ref Av1TransformVector<Vector256<int>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<int>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                    Load<Vector256<int>>(ref values, inputStride, i),
                    Load<Vector256<int>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
            ButterflyStore(cospi[2], cospi[62], buffer1[31], buffer1[16], ref values, outputStride, 1, 31, cosBit, in rounding);
            ButterflyStore(cospi[34], cospi[30], buffer1[30], buffer1[17], ref values, outputStride, 17, 15, cosBit, in rounding);
            ButterflyStore(cospi[18], cospi[46], buffer1[29], buffer1[18], ref values, outputStride, 9, 23, cosBit, in rounding);
            ButterflyStore(cospi[50], cospi[14], buffer1[28], buffer1[19], ref values, outputStride, 25, 7, cosBit, in rounding);
            ButterflyStore(cospi[10], cospi[54], buffer1[27], buffer1[20], ref values, outputStride, 5, 27, cosBit, in rounding);
            ButterflyStore(cospi[42], cospi[22], buffer1[26], buffer1[21], ref values, outputStride, 21, 11, cosBit, in rounding);
            ButterflyStore(cospi[26], cospi[38], buffer1[25], buffer1[22], ref values, outputStride, 13, 19, cosBit, in rounding);
            ButterflyStore(cospi[58], cospi[6], buffer1[24], buffer1[23], ref values, outputStride, 29, 3, cosBit, in rounding);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<int>> buffer0,
            ref Av1TransformVector<Vector512<int>> buffer1,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<int>>.CreateRounding(cosBit);

            // Stage 1 adds and subtracts the mirrored input pairs. It reads the whole source block before any output overwrites it.
            for (int i = 0; i < 16; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                    Load<Vector512<int>>(ref values, inputStride, i),
                    Load<Vector512<int>>(ref values, inputStride, 31 - i),
                    out buffer1[i],
                    out buffer1[31 - i]);
            }

            // Stage 2 adds and subtracts the mirrored pairs of the even half and rotates the central odd pairs by pi/4.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[i], buffer1[15 - i], out buffer0[i], out buffer0[15 - i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                    -cospi[32],
                    cospi[32],
                    buffer1[20 + i],
                    buffer1[27 - i],
                    out buffer0[20 + i],
                    out buffer0[27 - i],
                    cosBit,
                    in rounding);
            }

            // Stage 3 adds and subtracts the first eight even terms, rotates the central terms 10 to 13 by pi/4, and combines the odd terms.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[i], buffer0[7 - i], out buffer1[i], out buffer1[7 - i]);
            }

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[16 + i], buffer0[23 - i], out buffer1[16 + i], out buffer1[23 - i]);
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[31 - i], buffer0[24 + i], out buffer1[31 - i], out buffer1[24 + i]);
            }

            // Stage 4 continues the even terms and rotates the odd terms 18 to 21 and 26 to 29 by pi/8.
            for (int i = 0; i < 2; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[i], buffer1[3 - i], out buffer0[i], out buffer0[3 - i]);
            }

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
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
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[8 + i], buffer1[11 - i], out buffer0[8 + i], out buffer0[11 - i]);
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[15 - i], buffer1[12 + i], out buffer0[15 - i], out buffer0[12 + i]);
            }

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer1[19],
                buffer1[28],
                out buffer0[19],
                out buffer0[28],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[20],
                buffer1[27],
                out buffer0[20],
                out buffer0[27],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            // Stage 5 stores outputs 0, 8, 16 and 24 and continues the remaining terms. ButterflyStore writes each final output straight to the block.
            // Thus the transform needs no third buffer.
            ButterflyStore(cospi[32], cospi[32], buffer0[0], buffer0[1], ref values, outputStride, 0, 16, cosBit, in rounding);
            ButterflyStore(cospi[16], cospi[48], buffer0[3], buffer0[2], ref values, outputStride, 8, 24, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[4], buffer0[5], out buffer1[4], out buffer1[5]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[7], buffer0[6], out buffer1[7], out buffer1[6]);
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[16],
                cospi[48],
                buffer0[9],
                buffer0[14],
                out buffer1[9],
                out buffer1[14],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[48],
                -cospi[16],
                buffer0[10],
                buffer0[13],
                out buffer1[10],
                out buffer1[13],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[16], buffer0[19], out buffer1[16], out buffer1[19]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[17], buffer0[18], out buffer1[17], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[23], buffer0[20], out buffer1[23], out buffer1[20]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[22], buffer0[21], out buffer1[22], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[24], buffer0[27], out buffer1[24], out buffer1[27]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[25], buffer0[26], out buffer1[25], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[31], buffer0[28], out buffer1[31], out buffer1[28]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[30], buffer0[29], out buffer1[30], out buffer1[29]);

            // Stage 6 stores outputs 4, 12, 20 and 28, combines the terms 8 to 15, and rotates four odd pairs by multiples of pi/16.
            ButterflyStore(cospi[8], cospi[56], buffer1[7], buffer1[4], ref values, outputStride, 4, 28, cosBit, in rounding);
            ButterflyStore(cospi[40], cospi[24], buffer1[6], buffer1[5], ref values, outputStride, 20, 12, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[8],
                cospi[56],
                buffer1[17],
                buffer1[30],
                out buffer0[17],
                out buffer0[30],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[56],
                -cospi[8],
                buffer1[18],
                buffer1[29],
                out buffer0[18],
                out buffer0[29],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[40],
                cospi[24],
                buffer1[21],
                buffer1[26],
                out buffer0[21],
                out buffer0[26],
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[24],
                -cospi[40],
                buffer1[22],
                buffer1[25],
                out buffer0[22],
                out buffer0[25],
                cosBit,
                in rounding);

            // Stage 7 stores the outputs 2 + 4k with rotations in multiples of pi/32 and combines the odd terms 16 to 31.
            ButterflyStore(cospi[4], cospi[60], buffer0[15], buffer0[8], ref values, outputStride, 2, 30, cosBit, in rounding);
            ButterflyStore(cospi[36], cospi[28], buffer0[14], buffer0[9], ref values, outputStride, 18, 14, cosBit, in rounding);
            ButterflyStore(cospi[20], cospi[44], buffer0[13], buffer0[10], ref values, outputStride, 10, 22, cosBit, in rounding);
            ButterflyStore(cospi[52], cospi[12], buffer0[12], buffer0[11], ref values, outputStride, 26, 6, cosBit, in rounding);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[16], buffer0[17], out buffer1[16], out buffer1[17]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[19], buffer0[18], out buffer1[19], out buffer1[18]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[20], buffer0[21], out buffer1[20], out buffer1[21]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[23], buffer0[22], out buffer1[23], out buffer1[22]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[24], buffer0[25], out buffer1[24], out buffer1[25]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[27], buffer0[26], out buffer1[27], out buffer1[26]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[28], buffer0[29], out buffer1[28], out buffer1[29]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[31], buffer0[30], out buffer1[31], out buffer1[30]);

            // Stages 8 and 9 fuse the final rotations in multiples of pi/64 with the output permutation.
            // No later stage reads these results, so ButterflyStore writes them straight to the odd outputs.
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
}
