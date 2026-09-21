// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Provides the lane primitives shared by the halving filter's vector kernels.
/// </content>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// Gets the shuffle table that gathers even-position bytes into the lower half of a vector.
    /// </summary>
    /// <remarks>
    /// The upper half repeats the last index because those lanes are discarded by the half join. The
    /// .NET cross-platform shuffle permutes the whole vector, so one table serves every lane.
    /// </remarks>
    private static ReadOnlySpan<byte> EvenIndices128 =>
    [
        0, 2, 4, 6, 8, 10, 12, 14,
        14, 14, 14, 14, 14, 14, 14, 14
    ];

    /// <summary>
    /// Gets the shuffle table that gathers odd-position bytes into the lower half of a vector.
    /// </summary>
    private static ReadOnlySpan<byte> OddIndices128 =>
    [
        1, 3, 5, 7, 9, 11, 13, 15,
        15, 15, 15, 15, 15, 15, 15, 15
    ];

    /// <summary>
    /// Gets the two hundred and fifty-six bit form of <see cref="EvenIndices128"/>.
    /// </summary>
    private static ReadOnlySpan<byte> EvenIndices256 =>
    [
        0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30,
        30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30
    ];

    /// <summary>
    /// Gets the two hundred and fifty-six bit form of <see cref="OddIndices128"/>.
    /// </summary>
    private static ReadOnlySpan<byte> OddIndices256 =>
    [
        1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31,
        31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31
    ];

    /// <summary>
    /// Joins the lower halves of two vectors into one whole vector.
    /// </summary>
    /// <param name="low">The vector supplying the lower lanes.</param>
    /// <param name="high">The vector supplying the upper lanes.</param>
    /// <returns>The joined vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> JoinLowerHalves(Vector128<byte> low, Vector128<byte> high)
        => Vector128.Create(low.GetLower(), high.GetLower());

    /// <summary>
    /// Joins the lower halves of two vectors into one whole vector.
    /// </summary>
    /// <param name="low">The vector supplying the lower lanes.</param>
    /// <param name="high">The vector supplying the upper lanes.</param>
    /// <returns>The joined vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> JoinLowerHalves(Vector256<byte> low, Vector256<byte> high)
        => Vector256.Create(low.GetLower(), high.GetLower());

    /// <summary>
    /// Loads one band of a source row, with the row index clamped to the plane.
    /// </summary>
    /// <param name="plane">The first sample of the plane.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="row">The requested row, which may lie outside the plane.</param>
    /// <param name="last">The last row inside the plane.</param>
    /// <param name="offset">The band's first column.</param>
    /// <returns>The band samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> LoadRow128(ref byte plane, int stride, int row, int last, nuint offset)
        => Vector128.LoadUnsafe(ref plane, (nuint)(Math.Clamp(row, 0, last) * stride) + offset);

    /// <summary>
    /// Loads one band of a source row, with the row index clamped to the plane.
    /// </summary>
    /// <param name="plane">The first sample of the plane.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="row">The requested row, which may lie outside the plane.</param>
    /// <param name="last">The last row inside the plane.</param>
    /// <param name="offset">The band's first column.</param>
    /// <returns>The band samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> LoadRow256(ref byte plane, int stride, int row, int last, nuint offset)
        => Vector256.LoadUnsafe(ref plane, (nuint)(Math.Clamp(row, 0, last) * stride) + offset);

    /// <summary>
    /// Starts four accumulators from one symmetric tap.
    /// </summary>
    /// <param name="first">The samples on one side of the sampling position.</param>
    /// <param name="second">The samples on the other side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">Receives the first quarter of the band.</param>
    /// <param name="sum1">Receives the second quarter.</param>
    /// <param name="sum2">Receives the third quarter.</param>
    /// <param name="sum3">Receives the fourth quarter.</param>
    /// <remarks>
    /// The accumulators start at the rounding term, so the final shift needs no separate add. The band
    /// is widened to thirty-two bit lanes because the four tap products of a byte by fifty-six exceed
    /// sixteen bits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Start128(
        Vector128<byte> first,
        Vector128<byte> second,
        short tap,
        out Vector128<int> sum0,
        out Vector128<int> sum1,
        out Vector128<int> sum2,
        out Vector128<int> sum3)
    {
        Vector128<int> rounding = Vector128.Create(1 << (FilterBits - 1));
        sum0 = rounding;
        sum1 = rounding;
        sum2 = rounding;
        sum3 = rounding;
        Accumulate128(first, second, tap, ref sum0, ref sum1, ref sum2, ref sum3);
    }

    /// <summary>
    /// Adds one symmetric tap to four accumulators.
    /// </summary>
    /// <param name="first">The samples on one side of the sampling position.</param>
    /// <param name="second">The samples on the other side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">The first quarter of the band.</param>
    /// <param name="sum1">The second quarter.</param>
    /// <param name="sum2">The third quarter.</param>
    /// <param name="sum3">The fourth quarter.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate128(
        Vector128<byte> first,
        Vector128<byte> second,
        short tap,
        ref Vector128<int> sum0,
        ref Vector128<int> sum1,
        ref Vector128<int> sum2,
        ref Vector128<int> sum3)
    {
        Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector128<int> a0, out Vector128<int> a1, out Vector128<int> a2, out Vector128<int> a3);
        Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector128<int> b0, out Vector128<int> b1, out Vector128<int> b2, out Vector128<int> b3);
        Vector128<int> weight = Vector128.Create((int)tap);

        // The kernel is symmetric, so the two sides share one tap and are summed before the multiply.
        sum0 += (a0 + b0) * weight;
        sum1 += (a1 + b1) * weight;
        sum2 += (a2 + b2) * weight;
        sum3 += (a3 + b3) * weight;
    }

    /// <summary>
    /// Starts four accumulators from one symmetric tap.
    /// </summary>
    /// <param name="first">The samples on one side of the sampling position.</param>
    /// <param name="second">The samples on the other side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">Receives the first quarter of the band.</param>
    /// <param name="sum1">Receives the second quarter.</param>
    /// <param name="sum2">Receives the third quarter.</param>
    /// <param name="sum3">Receives the fourth quarter.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Start256(
        Vector256<byte> first,
        Vector256<byte> second,
        short tap,
        out Vector256<int> sum0,
        out Vector256<int> sum1,
        out Vector256<int> sum2,
        out Vector256<int> sum3)
    {
        Vector256<int> rounding = Vector256.Create(1 << (FilterBits - 1));
        sum0 = rounding;
        sum1 = rounding;
        sum2 = rounding;
        sum3 = rounding;
        Accumulate256(first, second, tap, ref sum0, ref sum1, ref sum2, ref sum3);
    }

    /// <summary>
    /// Adds one symmetric tap to four accumulators.
    /// </summary>
    /// <param name="first">The samples on one side of the sampling position.</param>
    /// <param name="second">The samples on the other side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">The first quarter of the band.</param>
    /// <param name="sum1">The second quarter.</param>
    /// <param name="sum2">The third quarter.</param>
    /// <param name="sum3">The fourth quarter.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate256(
        Vector256<byte> first,
        Vector256<byte> second,
        short tap,
        ref Vector256<int> sum0,
        ref Vector256<int> sum1,
        ref Vector256<int> sum2,
        ref Vector256<int> sum3)
    {
        Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector256<int> a0, out Vector256<int> a1, out Vector256<int> a2, out Vector256<int> a3);
        Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector256<int> b0, out Vector256<int> b1, out Vector256<int> b2, out Vector256<int> b3);
        Vector256<int> weight = Vector256.Create((int)tap);

        sum0 += (a0 + b0) * weight;
        sum1 += (a1 + b1) * weight;
        sum2 += (a2 + b2) * weight;
        sum3 += (a3 + b3) * weight;
    }

    /// <summary>
    /// Adds one tap of the separated streams to four accumulators.
    /// </summary>
    /// <param name="left">The stream read on the low side.</param>
    /// <param name="right">The stream read on the high side.</param>
    /// <param name="offset">The band's first output sample.</param>
    /// <param name="leftShift">The sample shift applied to the low side.</param>
    /// <param name="rightShift">The sample shift applied to the high side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">The first quarter of the band.</param>
    /// <param name="sum1">The second quarter.</param>
    /// <param name="sum2">The third quarter.</param>
    /// <param name="sum3">The fourth quarter.</param>
    /// <remarks>
    /// The shifts are whole samples of the separated streams, so both sides are ordinary unaligned
    /// loads. The clamped padding written by the separation keeps those loads inside the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTap128(
        ref byte left,
        ref byte right,
        nuint offset,
        int leftShift,
        int rightShift,
        short tap,
        ref Vector128<int> sum0,
        ref Vector128<int> sum1,
        ref Vector128<int> sum2,
        ref Vector128<int> sum3)
        => Accumulate128(
            Vector128.LoadUnsafe(ref Unsafe.Add(ref left, leftShift), offset),
            Vector128.LoadUnsafe(ref Unsafe.Add(ref right, rightShift), offset),
            tap,
            ref sum0,
            ref sum1,
            ref sum2,
            ref sum3);

    /// <summary>
    /// Adds one tap of the separated streams to four accumulators.
    /// </summary>
    /// <param name="left">The stream read on the low side.</param>
    /// <param name="right">The stream read on the high side.</param>
    /// <param name="offset">The band's first output sample.</param>
    /// <param name="leftShift">The sample shift applied to the low side.</param>
    /// <param name="rightShift">The sample shift applied to the high side.</param>
    /// <param name="tap">The tap weight in Q7.</param>
    /// <param name="sum0">The first quarter of the band.</param>
    /// <param name="sum1">The second quarter.</param>
    /// <param name="sum2">The third quarter.</param>
    /// <param name="sum3">The fourth quarter.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTap256(
        ref byte left,
        ref byte right,
        nuint offset,
        int leftShift,
        int rightShift,
        short tap,
        ref Vector256<int> sum0,
        ref Vector256<int> sum1,
        ref Vector256<int> sum2,
        ref Vector256<int> sum3)
        => Accumulate256(
            Vector256.LoadUnsafe(ref Unsafe.Add(ref left, leftShift), offset),
            Vector256.LoadUnsafe(ref Unsafe.Add(ref right, rightShift), offset),
            tap,
            ref sum0,
            ref sum1,
            ref sum2,
            ref sum3);

    /// <summary>
    /// Applies the filter shift to one accumulator.
    /// </summary>
    /// <param name="sum">The accumulated Q7 sum, including the rounding term.</param>
    /// <returns>The shifted sum.</returns>
    /// <remarks>
    /// The rounding term was added when the accumulator was started, so only the arithmetic shift is
    /// left. The clamp is explicit because the narrowing that follows truncates rather than
    /// saturates, and the negative taps of this kernel put sums on both sides of the sample range.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Round128(Vector128<int> sum)
        => Vector128.Min(Vector128.Max(sum >> FilterBits, Vector128<int>.Zero), Vector128.Create(255));

    /// <summary>
    /// Applies the filter shift to one accumulator.
    /// </summary>
    /// <param name="sum">The accumulated Q7 sum, including the rounding term.</param>
    /// <returns>The shifted sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Round256(Vector256<int> sum)
        => Vector256.Min(Vector256.Max(sum >> FilterBits, Vector256<int>.Zero), Vector256.Create(255));
}
