// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
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
    /// Gets a value indicating whether shuffle float operations are supported.
    /// </summary>
    public static bool SupportsShuffleNativeFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx512F.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether shuffle byte operations are supported.
    /// </summary>
    public static bool SupportsShuffleNativeByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx512BW.IsSupported;
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using the control.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector512{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> ShuffleNative(Vector512<float> vector, [ConstantExpected] byte control)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.Shuffle(vector, vector, control);
        }

        ThrowUnreachableException();
        return default;
    }

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

        ThrowUnreachableException();
        return default;
    }

    /// <summary>
    /// Performs a conversion from a 512-bit vector of 16 single-precision floating-point values to a 512-bit vector of 16 signed 32-bit integer values.
    /// Rounding is equivalent to <see cref="MidpointRounding.ToEven"/>.
    /// </summary>
    /// <param name="vector">The value to convert.</param>
    /// <returns>The <see cref="Vector128{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> ConvertToInt32RoundToEven(Vector512<float> vector)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.ConvertToVector512Int32(vector);
        }

        if (Avx.IsSupported)
        {
            Vector256<int> lower = Avx.ConvertToVector256Int32(vector.GetLower());
            Vector256<int> upper = Avx.ConvertToVector256Int32(vector.GetUpper());
            return Vector512.Create(lower, upper);
        }

        Vector512<float> sign = vector & Vector512.Create(-0.0f);
        Vector512<float> val_2p23_f32 = sign | Vector512.Create(8388608.0f);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return Vector512.ConvertToInt32(val_2p23_f32 | sign);
    }

    /// <summary>
    /// Rounds all values in <paramref name="vector"/> to the nearest integer
    /// following <see cref="MidpointRounding.ToEven"/> semantics.
    /// </summary>
    /// <param name="vector">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> RoundToNearestInteger(Vector512<float> vector)
    {
        if (Avx512F.IsSupported)
        {
            // imm8 = 0b1000:
            //   imm8[7:4] = 0b0000 -> preserve 0 fractional bits (round to whole numbers)
            //   imm8[3:0] = 0b1000 -> _MM_FROUND_TO_NEAREST_INT | _MM_FROUND_NO_EXC (round to nearest even, suppress exceptions)
            return Avx512F.RoundScale(vector, 0b0000_1000);
        }

        if (Avx.IsSupported)
        {
            Vector256<float> lower = Avx.RoundToNearestInteger(vector.GetLower());
            Vector256<float> upper = Avx.RoundToNearestInteger(vector.GetUpper());
            return Vector512.Create(lower, upper);
        }

        Vector512<float> sign = vector & Vector512.Create(-0F);
        Vector512<float> val_2p23_f32 = sign | Vector512.Create(8388608F);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return val_2p23_f32 | sign;
    }

    /// <summary>
    /// Performs a multiplication and an addition of the <see cref="Vector512{Single}"/>.
    /// </summary>
    /// <remarks>ret = (vm0 * vm1) + va</remarks>
    /// <param name="va">The vector to add to the intermediate result.</param>
    /// <param name="vm0">The first vector to multiply.</param>
    /// <param name="vm1">The second vector to multiply.</param>
    /// <returns>The <see cref="Vector256{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> MultiplyAdd(
        Vector512<float> va,
        Vector512<float> vm0,
        Vector512<float> vm1)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.FusedMultiplyAdd(vm0, vm1, va);
        }

        if (Fma.IsSupported)
        {
            Vector256<float> lower = Fma.MultiplyAdd(vm0.GetLower(), vm1.GetLower(), va.GetLower());
            Vector256<float> upper = Fma.MultiplyAdd(vm0.GetUpper(), vm1.GetUpper(), va.GetUpper());
            return Vector512.Create(lower, upper);
        }

        return va + (vm0 * vm1);
    }

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

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
