// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines shared forward-transform storage and rotation primitives.
/// </content>
internal static partial class Av1ForwardTransformer
{
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
