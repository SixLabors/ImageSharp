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
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct16<TValue>(
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

        // Stage 1 forms the mirror-symmetric pairs consumed by the recursive even and odd factorizations.
        for (int i = 0; i < 8; i++)
        {
            Av1ForwardTransformArithmetic<TValue>.AddSubtract(
                Load<TValue>(ref values, inputStride, i),
                Load<TValue>(ref values, inputStride, 15 - i),
                out buffer0[i],
                out buffer0[15 - i]);
        }

        // Stage 2 begins the recursive factorization of the even half and rotates the central odd pairs by pi/4.
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

        // Stage 3 reduces both eight-value groups into the four-value units consumed by the terminal rotations.
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

        // The even coefficients become final at stages 4 and 5, so they are written directly to their AV1 order.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[32],
            cospi[32],
            buffer0[0],
            buffer0[1],
            out TValue output0,
            out TValue output8,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[16],
            cospi[48],
            buffer0[3],
            buffer0[2],
            out TValue output4,
            out TValue output12,
            cosBit,
            in rounding);

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

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[8],
            cospi[56],
            buffer1[7],
            buffer1[4],
            out TValue output2,
            out TValue output14,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[40],
            cospi[24],
            buffer1[6],
            buffer1[5],
            out TValue output10,
            out TValue output6,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[8], buffer1[9], out buffer0[8], out buffer0[9]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[11], buffer1[10], out buffer0[11], out buffer0[10]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[12], buffer1[13], out buffer0[12], out buffer0[13]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[15], buffer1[14], out buffer0[15], out buffer0[14]);

        // Stage 6 applies the final pi/32 odd-frequency rotations. The following stores perform only the normative
        // coefficient permutation, so each rotation result is named by its final destination.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[4],
            cospi[60],
            buffer0[15],
            buffer0[8],
            out TValue output1,
            out TValue output15,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[36],
            cospi[28],
            buffer0[14],
            buffer0[9],
            out TValue output9,
            out TValue output7,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[20],
            cospi[44],
            buffer0[13],
            buffer0[10],
            out TValue output5,
            out TValue output11,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[52],
            cospi[12],
            buffer0[12],
            buffer0[11],
            out TValue output13,
            out TValue output3,
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
