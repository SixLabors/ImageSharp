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
    /// Creates a new vector by selecting values from an input vector using the control.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ShuffleNative(Vector512<float> vector, [ConstantExpected] byte control)
        => Avx512F.Shuffle(vector, vector, control);

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using a set of indices.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="indices">
    /// The per-element indices used to select a value from <paramref name="vector" />.
    /// </param>
    /// <returns>The <see cref="Vector512{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> ShuffleNative(Vector512<byte> vector, Vector512<byte> indices)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.Shuffle(vector, indices);
        }

        return Vector512.Shuffle(vector, indices);
    }

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
    /// Rounds all values in <paramref name="vector"/> to the nearest integer
    /// following <see cref="MidpointRounding.ToEven"/> semantics.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The vector with each value rounded to the nearest integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> RoundToNearestInteger(Vector512<float> vector)

        // imm8 = 0b1000:
        //   imm8[7:4] = 0b0000 -> preserve 0 fractional bits (round to whole numbers)
        //   imm8[3:0] = 0b1000 -> _MM_FROUND_TO_NEAREST_INT | _MM_FROUND_NO_EXC (round to nearest even, suppress exceptions)
        => Avx512F.RoundScale(vector, 0b0000_1000);

    /// <summary>
    /// Computes an estimate of (<paramref name="left"/> * <paramref name="right"/>) + <paramref name="addend"/>.
    /// </summary>
    /// <param name="left">The first vector to multiply.</param>
    /// <param name="right">The second vector to multiply.</param>
    /// <param name="addend">The vector to add to the product.</param>
    /// <returns>An estimate of the multiplication and addition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> MultiplyAddEstimate(Vector512<float> left, Vector512<float> right, Vector512<float> addend)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.FusedMultiplyAdd(left, right, addend);
        }

        Vector256<float> lower = Vector256_.MultiplyAddEstimate(left.GetLower(), right.GetLower(), addend.GetLower());
        Vector256<float> upper = Vector256_.MultiplyAddEstimate(left.GetUpper(), right.GetUpper(), addend.GetUpper());

        return Vector512.Create(lower, upper);
    }

    /// <summary>
    /// Computes (<paramref name="left"/> * <paramref name="right"/>) + <paramref name="addend"/>, rounded as one ternary operation.
    /// </summary>
    /// <param name="left">The first vector to multiply.</param>
    /// <param name="right">The second vector to multiply.</param>
    /// <param name="addend">The vector to add to the product.</param>
    /// <returns>The fused multiplication and addition result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> FusedMultiplyAdd(Vector512<float> left, Vector512<float> right, Vector512<float> addend)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.FusedMultiplyAdd(left, right, addend);
        }

        // Match the runtime fallback by recursively applying the same fused contract to both halves.
        Vector256<float> lower = Vector256_.FusedMultiplyAdd(left.GetLower(), right.GetLower(), addend.GetLower());
        Vector256<float> upper = Vector256_.FusedMultiplyAdd(left.GetUpper(), right.GetUpper(), addend.GetUpper());

        return Vector512.Create(lower, upper);
    }

    /// <summary>
    /// Subtracts packed unsigned 8-bit integers in <paramref name="right"/> from
    /// <paramref name="left"/>, saturating negative lane results to zero.
    /// </summary>
    /// <param name="left">The vector from which <paramref name="right"/> is subtracted.</param>
    /// <param name="right">The vector to subtract from <paramref name="left"/>.</param>
    /// <returns>The element-wise saturated differences.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<byte> SubtractSaturate(Vector512<byte> left, Vector512<byte> right)
    {
        if (Avx512BW.IsSupported)
        {
            return Avx512BW.SubtractSaturate(left, right);
        }

        // This mirrors the .NET 10 portable implementation: recursively processing both
        // 256-bit halves preserves lane order and lets each half select its available ISA.
        return Vector512.Create(Vector256_.SubtractSaturate(left.GetLower(), right.GetLower()), Vector256_.SubtractSaturate(left.GetUpper(), right.GetUpper()));
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

    /// <summary>
    /// Restricts a vector between a minimum and a maximum value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the vector.</typeparam>
    /// <param name="value">The vector to restrict.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The restricted <see cref="Vector512{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Clamp<T>(Vector512<T> value, Vector512<T> min, Vector512<T> max)
        => Vector512.Min(Vector512.Max(value, min), max);
}
