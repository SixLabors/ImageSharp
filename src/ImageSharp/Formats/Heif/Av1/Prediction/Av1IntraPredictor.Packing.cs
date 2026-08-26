// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Widens AV1 sample vectors for fixed-point prediction arithmetic and narrows completed results.
/// </summary>
internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Widens sixteen 8-bit samples into four 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first four widened samples.</param>
    /// <param name="result1">The second four widened samples.</param>
    /// <param name="result2">The third four widened samples.</param>
    /// <param name="result3">The fourth four widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector128<byte> source, out Vector128<int> result0, out Vector128<int> result1, out Vector128<int> result2, out Vector128<int> result3)
    {
        (Vector128<ushort> low, Vector128<ushort> high) = Vector128.Widen(source);
        (Vector128<uint> low0, Vector128<uint> low1) = Vector128.Widen(low);
        (Vector128<uint> high0, Vector128<uint> high1) = Vector128.Widen(high);
        result0 = low0.AsInt32();
        result1 = low1.AsInt32();
        result2 = high0.AsInt32();
        result3 = high1.AsInt32();
    }

    /// <summary>
    /// Widens thirty-two 8-bit samples into four 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first eight widened samples.</param>
    /// <param name="result1">The second eight widened samples.</param>
    /// <param name="result2">The third eight widened samples.</param>
    /// <param name="result3">The fourth eight widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector256<byte> source, out Vector256<int> result0, out Vector256<int> result1, out Vector256<int> result2, out Vector256<int> result3)
    {
        (Vector256<ushort> low, Vector256<ushort> high) = Vector256.Widen(source);
        (Vector256<uint> low0, Vector256<uint> low1) = Vector256.Widen(low);
        (Vector256<uint> high0, Vector256<uint> high1) = Vector256.Widen(high);
        result0 = low0.AsInt32();
        result1 = low1.AsInt32();
        result2 = high0.AsInt32();
        result3 = high1.AsInt32();
    }

    /// <summary>
    /// Widens sixty-four 8-bit samples into four 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first sixteen widened samples.</param>
    /// <param name="result1">The second sixteen widened samples.</param>
    /// <param name="result2">The third sixteen widened samples.</param>
    /// <param name="result3">The fourth sixteen widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector512<byte> source, out Vector512<int> result0, out Vector512<int> result1, out Vector512<int> result2, out Vector512<int> result3)
    {
        (Vector512<ushort> low, Vector512<ushort> high) = Vector512.Widen(source);
        (Vector512<uint> low0, Vector512<uint> low1) = Vector512.Widen(low);
        (Vector512<uint> high0, Vector512<uint> high1) = Vector512.Widen(high);
        result0 = low0.AsInt32();
        result1 = low1.AsInt32();
        result2 = high0.AsInt32();
        result3 = high1.AsInt32();
    }

    /// <summary>
    /// Widens eight high-bit-depth samples into two 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first four widened samples.</param>
    /// <param name="result1">The second four widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector128<short> source, out Vector128<int> result0, out Vector128<int> result1)
        => (result0, result1) = Vector128.Widen(source);

    /// <summary>
    /// Widens sixteen high-bit-depth samples into two 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first eight widened samples.</param>
    /// <param name="result1">The second eight widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector256<short> source, out Vector256<int> result0, out Vector256<int> result1)
        => (result0, result1) = Vector256.Widen(source);

    /// <summary>
    /// Widens thirty-two high-bit-depth samples into two 32-bit vectors.
    /// </summary>
    /// <param name="source">The packed samples.</param>
    /// <param name="result0">The first sixteen widened samples.</param>
    /// <param name="result1">The second sixteen widened samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Widen(Vector512<short> source, out Vector512<int> result0, out Vector512<int> result1)
        => (result0, result1) = Vector512.Widen(source);

    /// <summary>
    /// Narrows four 32-bit vectors into sixteen 8-bit samples.
    /// </summary>
    /// <param name="source0">The first four samples.</param>
    /// <param name="source1">The second four samples.</param>
    /// <param name="source2">The third four samples.</param>
    /// <param name="source3">The fourth four samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Narrow(Vector128<int> source0, Vector128<int> source1, Vector128<int> source2, Vector128<int> source3)
        => Vector128.Narrow(Vector128.Narrow(source0.AsUInt32(), source1.AsUInt32()), Vector128.Narrow(source2.AsUInt32(), source3.AsUInt32()));

    /// <summary>
    /// Narrows four 32-bit vectors into thirty-two 8-bit samples.
    /// </summary>
    /// <param name="source0">The first eight samples.</param>
    /// <param name="source1">The second eight samples.</param>
    /// <param name="source2">The third eight samples.</param>
    /// <param name="source3">The fourth eight samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Narrow(Vector256<int> source0, Vector256<int> source1, Vector256<int> source2, Vector256<int> source3)
        => Vector256.Narrow(Vector256.Narrow(source0.AsUInt32(), source1.AsUInt32()), Vector256.Narrow(source2.AsUInt32(), source3.AsUInt32()));

    /// <summary>
    /// Narrows four 32-bit vectors into sixty-four 8-bit samples.
    /// </summary>
    /// <param name="source0">The first sixteen samples.</param>
    /// <param name="source1">The second sixteen samples.</param>
    /// <param name="source2">The third sixteen samples.</param>
    /// <param name="source3">The fourth sixteen samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> Narrow(Vector512<int> source0, Vector512<int> source1, Vector512<int> source2, Vector512<int> source3)
        => Vector512.Narrow(Vector512.Narrow(source0.AsUInt32(), source1.AsUInt32()), Vector512.Narrow(source2.AsUInt32(), source3.AsUInt32()));

    /// <summary>
    /// Narrows two 32-bit vectors into eight high-bit-depth samples.
    /// </summary>
    /// <param name="source0">The first four samples.</param>
    /// <param name="source1">The second four samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> Narrow(Vector128<int> source0, Vector128<int> source1) => Vector128.Narrow(source0, source1);

    /// <summary>
    /// Narrows two 32-bit vectors into sixteen high-bit-depth samples.
    /// </summary>
    /// <param name="source0">The first eight samples.</param>
    /// <param name="source1">The second eight samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> Narrow(Vector256<int> source0, Vector256<int> source1) => Vector256.Narrow(source0, source1);

    /// <summary>
    /// Narrows two 32-bit vectors into thirty-two high-bit-depth samples.
    /// </summary>
    /// <param name="source0">The first sixteen samples.</param>
    /// <param name="source1">The second sixteen samples.</param>
    /// <returns>The packed samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> Narrow(Vector512<int> source0, Vector512<int> source1) => Vector512.Narrow(source0, source1);
}
