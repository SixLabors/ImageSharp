// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the eight-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the eight-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct8<TValue>(
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

        // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
        // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(
            Load<TValue>(ref values, inputStride, 0),
            Load<TValue>(ref values, inputStride, 7),
            out buffer0[0],
            out buffer1[7]);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(
            Load<TValue>(ref values, inputStride, 1),
            Load<TValue>(ref values, inputStride, 6),
            out buffer0[1],
            out buffer0[6]);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(
            Load<TValue>(ref values, inputStride, 2),
            Load<TValue>(ref values, inputStride, 5),
            out buffer0[2],
            out buffer0[5]);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(
            Load<TValue>(ref values, inputStride, 3),
            Load<TValue>(ref values, inputStride, 4),
            out buffer0[3],
            out buffer1[4]);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            -cospi[32],
            cospi[32],
            buffer0[5],
            buffer0[6],
            out buffer1[5],
            out buffer1[6],
            cosBit,
            in rounding);

        // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[32],
            cospi[32],
            buffer1[0],
            buffer1[1],
            out TValue output0,
            out TValue output4,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[16],
            cospi[48],
            buffer1[3],
            buffer1[2],
            out TValue output2,
            out TValue output6,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

        // Highway fuses the final two stages because no intermediate value is reused after either rotation.
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[8],
            cospi[56],
            buffer0[7],
            buffer0[4],
            out TValue output1,
            out TValue output7,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[40],
            cospi[24],
            buffer0[6],
            buffer0[5],
            out TValue output5,
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
    }
}
