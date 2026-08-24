// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Defines utility methods for <see cref="Vector256{T}"/> that have either:
/// <list type="number">
/// <item>Not yet been normalized in the runtime.</item>
/// <item>Produce codegen that is poorly optimized by the runtime.</item>
/// </list>
/// Should only be used if the intrinsics are available.
/// </summary>
#pragma warning disable SA1649 // File name should match first type name
internal static class Vector256_
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Creates a new vector by selecting values from an input vector using a set of indices.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ShuffleNative(Vector256<float> vector, [ConstantExpected] byte control)
        => Avx.Shuffle(vector, vector, control);

    /// <summary>
    /// Creates a new vector by selecting values from each 128-bit input lane using the corresponding indices.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="indices">The per-element indices used to select values within each 128-bit lane.</param>
    /// <returns>The shuffled <see cref="Vector256{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> ShufflePerLane(Vector256<byte> vector, Vector256<byte> indices)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Shuffle(vector, indices);
        }

        // The .NET 10 fallback treats indices as full-width when AVX2 is unavailable. Reusing
        // the low mask for each half preserves the lane-local vpshufb contract on AVX-only CPUs.
        Vector128<byte> indicesLo = indices.GetLower();
        Vector128<byte> lower = Vector128.ShuffleNative(vector.GetLower(), indicesLo);
        Vector128<byte> upper = Vector128.ShuffleNative(vector.GetUpper(), indicesLo);
        return Vector256.Create(lower, upper);
    }

    /// <summary>
    /// Performs a conversion from a 256-bit vector of 8 single-precision floating-point values to a 256-bit vector of 8 signed 32-bit integer values.
    /// Rounding is equivalent to <see cref="MidpointRounding.ToEven"/>.
    /// </summary>
    /// <param name="vector">The value to convert.</param>
    /// <returns>The <see cref="Vector256{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ConvertToInt32RoundToEven(Vector256<float> vector)
    {
        if (Avx.IsSupported)
        {
            return Avx.ConvertToVector256Int32(vector);
        }

        Vector256<float> sign = vector & Vector256.Create(-0F);
        Vector256<float> val_2p23_f32 = sign | Vector256.Create(8388608F);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return Vector256.ConvertToInt32(val_2p23_f32 | sign);
    }

    /// <summary>
    /// Converts all values in <paramref name="vector"/> to signed 32-bit integers, rounding midpoint values away from zero.
    /// </summary>
    /// <param name="vector">The values to convert.</param>
    /// <returns>The converted integer values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ConvertToInt32RoundAwayFromZero(Vector256<float> vector)
    {
        if (Avx.IsSupported)
        {
            // The x86 conversion truncates, so adding one half with each lane's sign implements round-to-nearest with midpoint values away from zero.
            Vector256<float> x86Adjustment = Vector256.Create(.5F) | (vector & Vector256.Create(-0F));
            return Avx.ConvertToVector256Int32WithTruncation(vector + x86Adjustment);
        }

        Vector256<float> sign = vector & Vector256.Create(-0F);
        Vector256<float> fallbackAdjustment = Vector256.Create(.5F) | sign;
        return Vector256.ConvertToInt32(vector + fallbackAdjustment);
    }

    /// <summary>
    /// Performs a multiplication and a negated addition of the <see cref="Vector256{Single}"/>.
    /// </summary>
    /// <remarks>ret = va - (vm0 * vm1)</remarks>
    /// <param name="va">The vector to add to the negated intermediate result.</param>
    /// <param name="vm0">The first vector to multiply.</param>
    /// <param name="vm1">The second vector to multiply.</param>
    /// <returns>The <see cref="Vector256{T}"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static Vector256<float> MultiplyAddNegated(
        Vector256<float> va,
        Vector256<float> vm0,
        Vector256<float> vm1)
    {
        if (Fma.IsSupported)
        {
            return Fma.MultiplyAddNegated(vm0, vm1, va);
        }

        return va - (vm0 * vm1);
    }

    /// <summary>
    /// Performs a multiplication and a subtraction of the <see cref="Vector256{Single}"/>.
    /// </summary>
    /// <remarks>ret = (vm0 * vm1) - vs</remarks>
    /// <param name="vs">The vector to subtract from the intermediate result.</param>
    /// <param name="vm0">The first vector to multiply.</param>
    /// <param name="vm1">The second vector to multiply.</param>
    /// <returns>The <see cref="Vector256{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> MultiplySubtract(
        Vector256<float> vs,
        Vector256<float> vm0,
        Vector256<float> vm1)
    {
        if (Fma.IsSupported)
        {
            return Fma.MultiplySubtract(vm1, vm0, vs);
        }

        return (vm0 * vm1) - vs;
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
    public static Vector256<int> MultiplyAddAdjacent(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.MultiplyAddAdjacent(left, right);
        }

        return Vector256.Create(
            Vector128_.MultiplyAddAdjacent(left.GetLower(), right.GetLower()),
            Vector128_.MultiplyAddAdjacent(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Packs signed 32-bit integers to signed 16-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{UInt16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<ushort> PackUnsignedSaturate(Vector256<int> left, Vector256<int> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.PackUnsignedSaturate(left, right);
        }

        Vector256<int> min = Vector256.Create((int)ushort.MinValue);
        Vector256<int> max = Vector256.Create((int)ushort.MaxValue);
        Vector256<uint> lefClamped = Vector256.Clamp(left, min, max).AsUInt32();
        Vector256<uint> rightClamped = Vector256.Clamp(right, min, max).AsUInt32();
        return Vector256.Narrow(lefClamped, rightClamped);
    }

    /// <summary>
    /// Packs signed 32-bit integers to signed 16-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> PackSignedSaturate(Vector256<int> left, Vector256<int> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.PackSignedSaturate(left, right);
        }

        Vector256<int> min = Vector256.Create((int)short.MinValue);
        Vector256<int> max = Vector256.Create((int)short.MaxValue);
        Vector256<int> lefClamped = Vector256.Clamp(left, min, max);
        Vector256<int> rightClamped = Vector256.Clamp(right, min, max);
        return Vector256.Narrow(lefClamped, rightClamped);
    }

    /// <summary>
    /// Packs signed 16-bit integers to signed 8-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{SByte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<sbyte> PackSignedSaturate(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.PackSignedSaturate(left, right);
        }

        Vector256<short> min = Vector256.Create((short)sbyte.MinValue);
        Vector256<short> max = Vector256.Create((short)sbyte.MaxValue);
        Vector256<short> lefClamped = Vector256.Clamp(left, min, max);
        Vector256<short> rightClamped = Vector256.Clamp(right, min, max);
        return Vector256.Narrow(lefClamped, rightClamped);
    }

    /// <summary>
    /// Widens a <see cref="Vector128{Int16}"/> to a <see cref="Vector256{Int32}"/>.
    /// </summary>
    /// <param name="value">The vector to widen.</param>
    /// <returns>The widened <see cref="Vector256{Int32}"/>.</returns>
    public static Vector256<int> Widen(Vector128<short> value)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ConvertToVector256Int32(value);
        }

        return Vector256.WidenLower(value.ToVector256());
    }

    /// <summary>
    /// Multiply the packed 16-bit integers in <paramref name="left"/> and <paramref name="right"/>, producing
    /// intermediate 32-bit integers, and store the low 16 bits of the intermediate integers in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 16-bit integers to multiply.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 16-bit integers to multiply.
    /// </param>
    /// <returns>
    /// A vector containing the low 16 bits of the products of the packed 16-bit integers
    /// from <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> MultiplyLow(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.MultiplyLow(left, right);
        }

        // Widen each half of the short vectors into two int vectors
        (Vector256<int> leftLower, Vector256<int> leftUpper) = Vector256.Widen(left);
        (Vector256<int> rightLower, Vector256<int> rightUpper) = Vector256.Widen(right);

        // Elementwise multiply: each int lane now holds the full 32-bit product
        Vector256<int> prodLo = leftLower * rightLower;
        Vector256<int> prodHi = leftUpper * rightUpper;

        // Narrow the two int vectors back into one short vector
        return Vector256.Narrow(prodLo, prodHi);
    }

    /// <summary>
    /// Multiply the packed 16-bit integers in <paramref name="left"/> and <paramref name="right"/>, producing
    /// intermediate 32-bit integers, and store the high 16 bits of the intermediate integers in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 16-bit integers to multiply.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 16-bit integers to multiply.
    /// </param>
    /// <returns>
    /// A vector containing the high 16 bits of the products of the packed 16-bit integers
    /// from <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> MultiplyHigh(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.MultiplyHigh(left, right);
        }

        // Widen each half of the short vectors into two int vectors
        (Vector256<int> leftLower, Vector256<int> leftUpper) = Vector256.Widen(left);
        (Vector256<int> rightLower, Vector256<int> rightUpper) = Vector256.Widen(right);

        // Elementwise multiply: each int lane now holds the full 32-bit product
        Vector256<int> prodLo = leftLower * rightLower;
        Vector256<int> prodHi = leftUpper * rightUpper;

        // Arithmetic shift right by 16 bits to extract the high word
        prodLo >>= 16;
        prodHi >>= 16;

        // Narrow the two int vectors back into one short vector
        return Vector256.Narrow(prodLo, prodHi);
    }

    /// <summary>
    /// Unpack and interleave 32-bit integers from the low half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 32-bit integers to unpack from the low half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 32-bit integers to unpack from the low half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 32-bit integers from the low
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> UnpackLow(Vector256<int> left, Vector256<int> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackLow(left, right);
        }

        Vector128<int> lo = Vector128_.UnpackLow(left.GetLower(), right.GetLower());
        Vector128<int> hi = Vector128_.UnpackLow(left.GetUpper(), right.GetUpper());

        return Vector256.Create(lo, hi);
    }

    /// <summary>
    /// Unpack and interleave 8-bit integers from the high half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 8-bit integers to unpack from the high half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 8-bit integers to unpack from the high half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 8-bit integers from the high
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> UnpackHigh(Vector256<byte> left, Vector256<byte> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackHigh(left, right);
        }

        Vector128<byte> lo = Vector128_.UnpackHigh(left.GetLower(), right.GetLower());
        Vector128<byte> hi = Vector128_.UnpackHigh(left.GetUpper(), right.GetUpper());

        return Vector256.Create(lo, hi);
    }

    /// <summary>
    /// Unpack and interleave 8-bit integers from the low half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 8-bit integers to unpack from the low half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 8-bit integers to unpack from the low half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 8-bit integers from the low
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> UnpackLow(Vector256<byte> left, Vector256<byte> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackLow(left, right);
        }

        Vector128<byte> lo = Vector128_.UnpackLow(left.GetLower(), right.GetLower());
        Vector128<byte> hi = Vector128_.UnpackLow(left.GetUpper(), right.GetUpper());

        return Vector256.Create(lo, hi);
    }
}
