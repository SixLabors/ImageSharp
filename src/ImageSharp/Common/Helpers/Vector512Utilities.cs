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
