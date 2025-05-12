// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Defines utility methods for <see cref="Vector128{T}"/> that have either:
/// <list type="number">
/// <item>Not yet been normalized in the runtime.</item>
/// <item>Produce codegen that is poorly optimized by the runtime.</item>
/// </list>
/// Should only be used if the intrinsics are available.
/// </summary>
#pragma warning disable SA1649 // File name should match first type name
internal static class Vector128_
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Gets a value indicating whether shuffle operations are supported.
    /// </summary>
    public static bool SupportsShuffleNativeByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Vector128.IsHardwareAccelerated)
            {
                if (RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64)
                {
                    return Ssse3.IsSupported;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether right align operations are supported.
    /// </summary>
    public static bool SupportsAlignRight
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Ssse3.IsSupported || AdvSimd.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether right or left byte shift operations are supported.
    /// </summary>
    public static bool SupportsShiftByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Sse2.IsSupported || AdvSimd.IsSupported;
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using the control.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector128{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> ShuffleNative(Vector128<float> vector, [ConstantExpected] byte control)
    {
        if (Sse.IsSupported)
        {
            return Sse.Shuffle(vector, vector, control);
        }

        // Don't use InverseMMShuffle here as we want to avoid the cast.
        Vector128<int> indices = Vector128.Create(
            control & 0x3,
            (control >> 2) & 0x3,
            (control >> 4) & 0x3,
            (control >> 6) & 0x3);

        return Vector128.Shuffle(vector, indices);
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using a set of indices.
    /// </summary>
    /// <param name="vector">
    /// The input vector from which values are selected.</param>
    /// <param name="indices">
    /// The per-element indices used to select a value from <paramref name="vector" />.
    /// </param>
    /// <returns>
    /// A new vector containing the values from <paramref name="vector" /> selected by the given <paramref name="indices" />.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> ShuffleNative(Vector128<byte> vector, Vector128<byte> indices)
    {
        // For x64 we use the SSSE3 shuffle intrinsic to avoid additional instructions. 3 vs 1.
        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(vector, indices);
        }

        // For ARM and WASM, codegen will be optimal.
        // We don't throw for x86/x64 so we should never use this method without
        // checking for support.
        return Vector128.Shuffle(vector, indices);
    }

    /// <summary>
    /// Shifts a 128-bit value right by a specified number of bytes while shifting in zeros.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="numBytes">The number of bytes to shift by.</param>
    /// <returns>The <see cref="Vector128{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> ShiftRightBytesInVector(Vector128<byte> value, [ConstantExpected(Max = (byte)15)] byte numBytes)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ShiftRightLogical128BitLane(value, numBytes);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractVector128(value, Vector128<byte>.Zero, numBytes);
        }

        ThrowUnreachableException();
        return default;
    }

    /// <summary>
    /// Shifts a 128-bit value left by a specified number of bytes while shifting in zeros.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="numBytes">The number of bytes to shift by.</param>
    /// <returns>The <see cref="Vector128{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> ShiftLeftBytesInVector(Vector128<byte> value, [ConstantExpected(Max = (byte)15)] byte numBytes)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ShiftLeftLogical128BitLane(value, numBytes);
        }

        if (AdvSimd.IsSupported)
        {
#pragma warning disable CA1857 // A constant is expected for the parameter
            return AdvSimd.ExtractVector128(Vector128<byte>.Zero, value, (byte)(Vector128<byte>.Count - numBytes));
#pragma warning restore CA1857 // A constant is expected for the parameter
        }

        ThrowUnreachableException();
        return default;
    }

    /// <summary>
    /// Right aligns elements of two source 128-bit values depending on bits in a mask.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <param name="mask">An 8-bit mask used for the operation.</param>
    /// <returns>The <see cref="Vector128{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> AlignRight(Vector128<byte> left, Vector128<byte> right, [ConstantExpected(Max = (byte)15)] byte mask)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.AlignRight(left, right, mask);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractVector128(right, left, mask);
        }

        ThrowUnreachableException();
        return default;
    }

    /// <summary>
    /// Performs a conversion from a 128-bit vector of 4 single-precision floating-point values to a 128-bit vector of 4 signed 32-bit integer values.
    /// Rounding is equivalent to <see cref="MidpointRounding.ToEven"/>.
    /// </summary>
    /// <param name="vector">The value to convert.</param>
    /// <returns>The <see cref="Vector128{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> ConvertToInt32RoundToEven(Vector128<float> vector)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToVector128Int32(vector);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ConvertToInt32RoundToEven(vector);
        }

        Vector128<float> sign = vector & Vector128.Create(-0F);
        Vector128<float> val_2p23_f32 = sign | Vector128.Create(8388608F);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return Vector128.ConvertToInt32(val_2p23_f32 | sign);
    }

    /// <summary>
    /// Rounds all values in <paramref name="vector"/> to the nearest integer
    /// following <see cref="MidpointRounding.ToEven"/> semantics.
    /// </summary>
    /// <param name="vector">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> RoundToNearestInteger(Vector128<float> vector)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.RoundToNearestInteger(vector);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.RoundToNearest(vector);
        }

        Vector128<float> sign = vector & Vector128.Create(-0F);
        Vector128<float> val_2p23_f32 = sign | Vector128.Create(8388608F);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return val_2p23_f32 | sign;
    }

    /// <summary>
    /// Performs a multiplication and an addition of the <see cref="Vector128{Single}"/>.
    /// </summary>
    /// <remarks>ret = (vm0 * vm1) + va</remarks>
    /// <param name="va">The vector to add to the intermediate result.</param>
    /// <param name="vm0">The first vector to multiply.</param>
    /// <param name="vm1">The second vector to multiply.</param>
    /// <returns>The <see cref="Vector256{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> MultiplyAdd(
        Vector128<float> va,
        Vector128<float> vm0,
        Vector128<float> vm1)
    {
        if (Fma.IsSupported)
        {
            return Fma.MultiplyAdd(vm1, vm0, va);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.FusedMultiplyAdd(va, vm0, vm1);
        }

        return va + (vm0 * vm1);
    }

    /// <summary>
    /// Packs signed 16-bit integers to unsigned 8-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{Int16}"/>.</returns>
    public static Vector128<byte> PackUnsignedSaturate(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.PackUnsignedSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractNarrowingSaturateUnsignedUpper(AdvSimd.ExtractNarrowingSaturateUnsignedLower(left), right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertNarrowingSaturateUnsigned(left, right);
        }

        Vector128<short> min = Vector128.Create((short)byte.MinValue);
        Vector128<short> max = Vector128.Create((short)byte.MaxValue);
        Vector128<ushort> lefClamped = Clamp(left, min, max).AsUInt16();
        Vector128<ushort> rightClamped = Clamp(right, min, max).AsUInt16();
        return Vector128.Narrow(lefClamped, rightClamped);
    }

    /// <summary>
    /// Packs signed 32-bit integers to signed 16-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> PackSignedSaturate(Vector128<int> left, Vector128<int> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.PackSignedSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractNarrowingSaturateUpper(AdvSimd.ExtractNarrowingSaturateLower(left), right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertNarrowingSaturateSigned(left, right);
        }

        Vector128<int> min = Vector128.Create((int)short.MinValue);
        Vector128<int> max = Vector128.Create((int)short.MaxValue);
        Vector128<int> lefClamped = Clamp(left, min, max);
        Vector128<int> rightClamped = Clamp(right, min, max);
        return Vector128.Narrow(lefClamped, rightClamped);
    }

    /// <summary>
    /// Restricts a vector between a minimum and a maximum value.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the vector.</typeparam>
    /// <param name="value">The vector to restrict.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The restricted <see cref="Vector128{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Clamp<T>(Vector128<T> value, Vector128<T> min, Vector128<T> max)
        => Vector128.Min(Vector128.Max(value, min), max);

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
