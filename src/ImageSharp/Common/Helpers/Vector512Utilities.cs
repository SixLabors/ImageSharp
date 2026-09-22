// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Defines utility methods for <see cref="Vector512{T}"/> that have either:
/// <list type="number">
/// <item>Not yet been normalized in the runtime.</item>
/// <item>Produce codegen that is poorly optimized by the runtime.</item>
/// </list>
/// Should only be used if the intrinsics are available.
/// </summary>
#pragma warning disable SA1649 // File name should match first type name
internal static class Vector512_
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Packs signed 32-bit integers to signed 16-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> PackSignedSaturate(Vector512<int> left, Vector512<int> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.PackSignedSaturate(left, right);
        }

        return Vector512.Create(
            Vector256_.PackSignedSaturate(left.GetLower(), right.GetLower()),
            Vector256_.PackSignedSaturate(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the lower signed 64-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int64}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> UnpackLow(Vector512<long> left, Vector512<long> right)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.UnpackLow(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackLow(left.GetLower(), right.GetLower()),
            Vector256_.UnpackLow(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the upper signed 64-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int64}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> UnpackHigh(Vector512<long> left, Vector512<long> right)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.UnpackHigh(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector256_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the lower signed 16-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> UnpackLow(Vector512<short> left, Vector512<short> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.UnpackLow(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackLow(left.GetLower(), right.GetLower()),
            Vector256_.UnpackLow(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the upper signed 16-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> UnpackHigh(Vector512<short> left, Vector512<short> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.UnpackHigh(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector256_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the lower signed 32-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> UnpackLow(Vector512<int> left, Vector512<int> right)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.UnpackLow(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackLow(left.GetLower(), right.GetLower()),
            Vector256_.UnpackLow(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the upper signed 32-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector512{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> UnpackHigh(Vector512<int> left, Vector512<int> right)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.UnpackHigh(left, right);
        }

        return Vector512.Create(
            Vector256_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector256_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Selects each 32-bit element from one of two vectors by index.
    /// </summary>
    /// <param name="lower">The vector that supplies indices 0 through 15.</param>
    /// <param name="indices">The source index of each destination element.</param>
    /// <param name="upper">The vector that supplies indices 16 through 31.</param>
    /// <returns>The <see cref="Vector512{Int32}"/>.</returns>
    /// <remarks>
    /// An index addresses the 32 elements of the two vectors together. The portable form shuffles
    /// each vector by the low four bits and then selects between the two results on bit four, which
    /// is what the instruction does in one step.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> PermuteVar16x32x2(Vector512<int> lower, Vector512<int> indices, Vector512<int> upper)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.PermuteVar16x32x2(lower, indices, upper);
        }

        Vector512<int> within = indices & Vector512.Create(15);
        Vector512<int> selector = Vector512.Equals(indices & Vector512.Create(16), Vector512.Create(16));
        return Vector512.ConditionalSelect(selector, Vector512.Shuffle(upper, within), Vector512.Shuffle(lower, within));
    }

    /// <summary>
    /// Selects each 64-bit element from one of two vectors by index.
    /// </summary>
    /// <param name="lower">The vector that supplies indices 0 through 7.</param>
    /// <param name="indices">The source index of each destination element.</param>
    /// <param name="upper">The vector that supplies indices 8 through 15.</param>
    /// <returns>The <see cref="Vector512{Int64}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> PermuteVar8x64x2(Vector512<long> lower, Vector512<long> indices, Vector512<long> upper)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.PermuteVar8x64x2(lower, indices, upper);
        }

        Vector512<long> within = indices & Vector512.Create(7L);
        Vector512<long> selector = Vector512.Equals(indices & Vector512.Create(8L), Vector512.Create(8L));
        return Vector512.ConditionalSelect(selector, Vector512.Shuffle(upper, within), Vector512.Shuffle(lower, within));
    }

    /// <summary>
    /// Selects each 128-bit lane of the result from one of two vectors.
    /// </summary>
    /// <param name="lower">The vector that supplies the first two destination lanes.</param>
    /// <param name="upper">The vector that supplies the last two destination lanes.</param>
    /// <param name="control">Two bits per destination lane, selecting its source lane.</param>
    /// <returns>The <see cref="Vector512{Int32}"/>.</returns>
    /// <remarks>
    /// The first two destination lanes come from <paramref name="lower"/> and the last two from
    /// <paramref name="upper"/>, which is what the instruction does. The portable form expands the
    /// control into element indices and defers to the two-source permute.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Shuffle4x128(Vector512<int> lower, Vector512<int> upper, [ConstantExpected] byte control)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.Shuffle4x128(lower, upper, control);
        }

        // The control is a constant, so selecting whole 128-bit lanes lets the JIT fold each choice
        // away. Building an index vector instead would put a loop and a stack buffer in the path of
        // what is otherwise four register moves.
        return Vector512.Create(
            Vector256.Create(SelectLane(lower, control & 3), SelectLane(lower, (control >> 2) & 3)),
            Vector256.Create(SelectLane(upper, (control >> 4) & 3), SelectLane(upper, (control >> 6) & 3)));
    }

    /// <summary>
    /// Returns one 128-bit lane of a vector.
    /// </summary>
    /// <param name="value">The vector to read.</param>
    /// <param name="lane">The lane index, from zero through three.</param>
    /// <returns>The <see cref="Vector128{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> SelectLane(Vector512<int> value, int lane)
        => lane switch
        {
            0 => value.GetLower().GetLower(),
            1 => value.GetLower().GetUpper(),
            2 => value.GetUpper().GetLower(),
            _ => value.GetUpper().GetUpper(),
        };

    /// <summary>
    /// Adds the absolute differences of packed unsigned 8-bit integers in <paramref name="left"/> and
    /// <paramref name="right"/> into <paramref name="accumulator"/>.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to compare.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to compare.
    /// </param>
    /// <param name="accumulator">
    /// The running total that the differences are added to.
    /// </param>
    /// <returns>
    /// A vector whose lanes together hold <paramref name="accumulator"/> plus the sixty-four absolute differences
    /// </returns>
    /// <remarks>
    /// The spread of the sums across the lanes is not defined, because each platform keeps the grouping
    /// that its own instruction produces. Only the total across all lanes is defined, so the caller must
    /// reduce the result with a horizontal sum and must not read one lane on its own. A lane holds a
    /// 32-bit total, so it cannot overflow until more than sixteen million samples are added to it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<uint> SumAbsoluteDifferences(Vector512<byte> left, Vector512<byte> right, Vector512<uint> accumulator)
    {
        if (Avx512BW.IsSupported)
        {
            return accumulator + Avx512BW.SumAbsoluteDifferences(left, right).AsUInt32();
        }

        return Vector512.Create(
            Vector256_.SumAbsoluteDifferences(left.GetLower(), right.GetLower(), accumulator.GetLower()),
            Vector256_.SumAbsoluteDifferences(left.GetUpper(), right.GetUpper(), accumulator.GetUpper()));
    }

    /// <summary>
    /// Multiply packed signed 16-bit integers in <paramref name="left"/> and <paramref name="right"/>, producing
    /// intermediate signed 32-bit integers. Horizontally add adjacent pairs of intermediate 32-bit integers, and
    /// pack the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 16-bit integers to multiply and add.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 16-bit integers to multiply and add.
    /// </param>
    /// <returns>
    /// A vector containing the results of multiplying and adding adjacent pairs of packed signed 16-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> MultiplyAddAdjacent(Vector512<short> left, Vector512<short> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.MultiplyAddAdjacent(left, right);
        }

        return Vector512.Create(
            Vector256_.MultiplyAddAdjacent(left.GetLower(), right.GetLower()),
            Vector256_.MultiplyAddAdjacent(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Multiply packed unsigned 8-bit integers in <paramref name="left"/> by packed signed 8-bit integers in
    /// <paramref name="right"/>, producing intermediate signed 16-bit integers. Horizontally add adjacent pairs of
    /// intermediate integers and pack the saturated results.
    /// </summary>
    /// <param name="left">
    /// The vector containing packed unsigned 8-bit integers to multiply and add.
    /// </param>
    /// <param name="right">
    /// The vector containing packed signed 8-bit integers to multiply and add.
    /// </param>
    /// <returns>
    /// A vector containing the saturated results of multiplying and adding adjacent pairs of packed 8-bit integers
    /// </returns>
    /// <remarks>
    /// The x86 instruction pairs within each 128-bit lane, so composing the two halves of the
    /// narrower form gives the same lane order on every path.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> MultiplyAddAdjacent(Vector512<byte> left, Vector512<sbyte> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.MultiplyAddAdjacent(left, right);
        }

        return Vector512.Create(
            Vector256_.MultiplyAddAdjacent(left.GetLower(), right.GetLower()),
            Vector256_.MultiplyAddAdjacent(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using the control.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ShuffleNative(Vector512<float> vector, [ConstantExpected] byte control)
        => Avx512F.Shuffle(vector, vector, control);

    /// <summary>
    /// Performs a conversion from a 512-bit vector of 16 single-precision floating-point values to a 512-bit vector of 16 signed 32-bit integer values.
    /// Rounding is equivalent to <see cref="MidpointRounding.ToEven"/>.
    /// </summary>
    /// <param name="vector">The value to convert.</param>
    /// <returns>The <see cref="Vector128{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> ConvertToInt32RoundToEven(Vector512<float> vector)
        => Avx512F.ConvertToVector512Int32(vector);

    /// <summary>
    /// Converts all values in <paramref name="vector"/> to signed 32-bit integers, rounding midpoint values away from zero.
    /// </summary>
    /// <param name="vector">The values to convert.</param>
    /// <returns>The converted integer values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> ConvertToInt32RoundAwayFromZero(Vector512<float> vector)
    {
        // The x86 conversion truncates, so adding one half with each lane's sign implements round-to-nearest with midpoint values away from zero.
        Vector512<float> half = Vector512.Create(.5F) | (vector & Vector512.Create(-0F));
        return Avx512F.ConvertToVector512Int32WithTruncation(vector + half);
    }

    /// <summary>
    /// Performs a multiplication and a negated addition of the <see cref="Vector512{Single}"/>.
    /// </summary>
    /// <remarks>ret = va - (vm0 * vm1)</remarks>
    /// <param name="va">The vector to add to the negated intermediate result.</param>
    /// <param name="vm0">The first vector to multiply.</param>
    /// <param name="vm1">The second vector to multiply.</param>
    /// <returns>The <see cref="Vector512{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> MultiplyAddNegated(
        Vector512<float> va,
        Vector512<float> vm0,
        Vector512<float> vm1)
        => Avx512F.FusedMultiplyAddNegated(vm0, vm1, va);
}
