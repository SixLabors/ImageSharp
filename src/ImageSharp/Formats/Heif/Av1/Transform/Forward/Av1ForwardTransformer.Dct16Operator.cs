// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Implements the sixteen-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the sixteen-point forward transform for every supported lane width.
    /// </summary>
    internal readonly struct Dct16Operator : IAv1ForwardTransform1dOperator
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(
                    Load<int>(ref values, inputStride, i),
                    Load<int>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out int output0,
                out int output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out int output4,
                out int output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out int output2,
                out int output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out int output10,
                out int output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out int output1,
                out int output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out int output9,
                out int output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out int output5,
                out int output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out int output13,
                out int output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(
                    Load<short>(ref values, inputStride, i),
                    Load<short>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out short output0,
                out short output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out short output4,
                out short output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out short output2,
                out short output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out short output10,
                out short output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out short output1,
                out short output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out short output9,
                out short output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out short output5,
                out short output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out short output13,
                out short output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                    Load<Vector128<short>>(ref values, inputStride, i),
                    Load<Vector128<short>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector128<short> output0,
                out Vector128<short> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector128<short> output4,
                out Vector128<short> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector128<short> output2,
                out Vector128<short> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector128<short> output10,
                out Vector128<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector128<short> output1,
                out Vector128<short> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector128<short> output9,
                out Vector128<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector128<short> output5,
                out Vector128<short> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector128<short> output13,
                out Vector128<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                    Load<Vector256<short>>(ref values, inputStride, i),
                    Load<Vector256<short>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector256<short> output0,
                out Vector256<short> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector256<short> output4,
                out Vector256<short> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector256<short> output2,
                out Vector256<short> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector256<short> output10,
                out Vector256<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector256<short> output1,
                out Vector256<short> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector256<short> output9,
                out Vector256<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector256<short> output5,
                out Vector256<short> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector256<short> output13,
                out Vector256<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                    Load<Vector512<short>>(ref values, inputStride, i),
                    Load<Vector512<short>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector512<short> output0,
                out Vector512<short> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector512<short> output4,
                out Vector512<short> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector512<short> output2,
                out Vector512<short> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector512<short> output10,
                out Vector512<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector512<short> output1,
                out Vector512<short> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector512<short> output9,
                out Vector512<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector512<short> output5,
                out Vector512<short> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector512<short> output13,
                out Vector512<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                    Load<Vector128<int>>(ref values, inputStride, i),
                    Load<Vector128<int>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector128<int> output0,
                out Vector128<int> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector128<int> output4,
                out Vector128<int> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector128<int> output2,
                out Vector128<int> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector128<int> output10,
                out Vector128<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector128<int> output1,
                out Vector128<int> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector128<int> output9,
                out Vector128<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector128<int> output5,
                out Vector128<int> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector128<int> output13,
                out Vector128<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                    Load<Vector256<int>>(ref values, inputStride, i),
                    Load<Vector256<int>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector256<int> output0,
                out Vector256<int> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector256<int> output4,
                out Vector256<int> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector256<int> output2,
                out Vector256<int> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector256<int> output10,
                out Vector256<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector256<int> output1,
                out Vector256<int> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector256<int> output9,
                out Vector256<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector256<int> output5,
                out Vector256<int> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector256<int> output13,
                out Vector256<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
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

            // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                    Load<Vector512<int>>(ref values, inputStride, i),
                    Load<Vector512<int>>(ref values, inputStride, 15 - i),
                    out buffer0[i],
                    out buffer0[15 - i]);
            }

            // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

            // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

            // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector512<int> output0,
                out Vector512<int> output8,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector512<int> output4,
                out Vector512<int> output12,
                cosBit,
                in rounding);

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

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer1[7],
                buffer1[4],
                out Vector512<int> output2,
                out Vector512<int> output14,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer1[6],
                buffer1[5],
                out Vector512<int> output10,
                out Vector512<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

            // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
            // coefficient permutation, so each rotation result is named by its final destination.
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[4],
                cospi[60],
                buffer0[15],
                buffer0[8],
                out Vector512<int> output1,
                out Vector512<int> output15,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[36],
                cospi[28],
                buffer0[14],
                buffer0[9],
                out Vector512<int> output9,
                out Vector512<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[20],
                cospi[44],
                buffer0[13],
                buffer0[10],
                out Vector512<int> output5,
                out Vector512<int> output11,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[52],
                cospi[12],
                buffer0[12],
                buffer0[11],
                out Vector512<int> output13,
                out Vector512<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
            Store(ref values, outputStride, 8, output8);
            Store(ref values, outputStride, 9, output9);
            Store(ref values, outputStride, 10, output10);
            Store(ref values, outputStride, 11, output11);
            Store(ref values, outputStride, 12, output12);
            Store(ref values, outputStride, 13, output13);
            Store(ref values, outputStride, 14, output14);
            Store(ref values, outputStride, 15, output15);
        }
    }
}
