// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the four-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the four-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct4<TValue>(
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

        TValue input0 = Load<TValue>(ref values, inputStride, 0);
        TValue input1 = Load<TValue>(ref values, inputStride, 1);
        TValue input2 = Load<TValue>(ref values, inputStride, 2);
        TValue input3 = Load<TValue>(ref values, inputStride, 3);

        // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
        // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[32],
            cospi[32],
            buffer0[0],
            buffer0[1],
            out TValue output0,
            out TValue output2,
            cosBit,
            in rounding);

        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            cospi[16],
            cospi[48],
            buffer0[3],
            buffer0[2],
            out TValue output1,
            out TValue output3,
            cosBit,
            in rounding);

        Store(ref values, outputStride, 0, output0);
        Store(ref values, outputStride, 1, output1);
        Store(ref values, outputStride, 2, output2);
        Store(ref values, outputStride, 3, output3);
    }

    /// <summary>
    /// Loads one scalar or SIMD transform value from strided block storage.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value to load.</typeparam>
    /// <param name="values">The first value in the transform block.</param>
    /// <param name="stride">The byte distance between consecutive transform positions.</param>
    /// <param name="index">The transform position to load.</param>
    /// <returns>The requested transform value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TValue Load<TValue>(ref byte values, nint stride, int index)
        where TValue : struct
        => Unsafe.ReadUnaligned<TValue>(ref Unsafe.AddByteOffset(ref values, index * stride));

    /// <summary>
    /// Stores one scalar or SIMD transform value in strided block storage.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value to store.</typeparam>
    /// <param name="values">The first value in the transform block.</param>
    /// <param name="stride">The byte distance between consecutive transform positions.</param>
    /// <param name="index">The transform position to store.</param>
    /// <param name="value">The transform value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store<TValue>(ref byte values, nint stride, int index, TValue value)
        where TValue : struct
        => Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref values, index * stride), value);

    /// <summary>
    /// Applies one paired rotation and stores both results directly in the strided transform block.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="weight0">The first fixed-point rotation weight.</param>
    /// <param name="weight1">The second fixed-point rotation weight.</param>
    /// <param name="input0">The first rotation input.</param>
    /// <param name="input1">The second rotation input.</param>
    /// <param name="values">The first value in the transform block.</param>
    /// <param name="stride">The byte distance between consecutive transform positions.</param>
    /// <param name="outputIndex0">The transform position for the first result.</param>
    /// <param name="outputIndex1">The transform position for the second result.</param>
    /// <param name="cosBit">The fixed-point precision of the rotation weights.</param>
    /// <param name="rounding">The lane-width-specific rounding constants.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ButterflyStore<TValue>(
        int weight0,
        int weight1,
        TValue input0,
        TValue input1,
        ref byte values,
        nint stride,
        int outputIndex0,
        int outputIndex1,
        int cosBit,
        in Av1TransformRounding rounding)
        where TValue : struct
    {
        Av1ForwardTransformArithmetic<TValue>.Butterfly(
            weight0,
            weight1,
            input0,
            input1,
            out TValue output0,
            out TValue output1,
            cosBit,
            in rounding);

        Store(ref values, stride, outputIndex0, output0);
        Store(ref values, stride, outputIndex1, output1);
    }

    /// <summary>
    /// Applies one paired rotation into two positions of a transform-stage buffer.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="weight0">The first fixed-point rotation weight.</param>
    /// <param name="weight1">The second fixed-point rotation weight.</param>
    /// <param name="input0">The first rotation input.</param>
    /// <param name="input1">The second rotation input.</param>
    /// <param name="output">The transform-stage buffer receiving both results.</param>
    /// <param name="outputIndex0">The buffer position for the first result.</param>
    /// <param name="outputIndex1">The buffer position for the second result.</param>
    /// <param name="cosBit">The fixed-point precision of the rotation weights.</param>
    /// <param name="rounding">The lane-width-specific rounding constants.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Butterfly<TValue>(
        int weight0,
        int weight1,
        TValue input0,
        TValue input1,
        ref Av1TransformVector<TValue> output,
        int outputIndex0,
        int outputIndex1,
        int cosBit,
        in Av1TransformRounding rounding)
        where TValue : struct
        => Av1ForwardTransformArithmetic<TValue>.Butterfly(
            weight0,
            weight1,
            input0,
            input1,
            out output[outputIndex0],
            out output[outputIndex1],
            cosBit,
            in rounding);
}
