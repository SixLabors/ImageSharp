// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    /// Creates a new vector by selecting values from an input vector using the control.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector128{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> ShuffleNative(Vector128<int> vector, [ConstantExpected] byte control)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.Shuffle(vector, control);
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

        return Vector128.Shuffle(value, Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15) + Vector128.Create(numBytes));
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

        return Vector128.Shuffle(value, Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15) - Vector128.Create(numBytes));
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

#pragma warning disable CA1857 // A constant is expected for the parameter
        return ShiftLeftBytesInVector(left, (byte)(Vector128<byte>.Count - mask)) | ShiftRightBytesInVector(right, mask);
#pragma warning restore CA1857 // A constant is expected for the parameter
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

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertToInt32Saturate(PackedSimd.RoundToNearest(vector));
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

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.RoundToNearest(vector);
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
    /// Packs signed 32-bit integers to unsigned 16-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{UInt16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> PackUnsignedSaturate(Vector128<int> left, Vector128<int> right)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.PackUnsignedSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractNarrowingSaturateUnsignedUpper(AdvSimd.ExtractNarrowingSaturateUnsignedLower(left), right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertNarrowingSaturateUnsigned(left, right);
        }

        Vector128<int> min = Vector128.Create((int)ushort.MinValue);
        Vector128<int> max = Vector128.Create((int)ushort.MaxValue);
        Vector128<uint> lefClamped = Clamp(left, min, max).AsUInt32();
        Vector128<uint> rightClamped = Clamp(right, min, max).AsUInt32();
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
    /// Packs signed 16-bit integers to signed 8-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> PackSignedSaturate(Vector128<short> left, Vector128<short> right)
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

        Vector128<short> min = Vector128.Create((short)sbyte.MinValue);
        Vector128<short> max = Vector128.Create((short)sbyte.MaxValue);
        Vector128<short> lefClamped = Clamp(left, min, max);
        Vector128<short> rightClamped = Clamp(right, min, max);
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
    public static Vector128<int> MultiplyAddAdjacent(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyAddAdjacent(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector128<int> prodLo = AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower());
            Vector128<int> prodHi = AdvSimd.MultiplyWideningLower(left.GetUpper(), right.GetUpper());

            Vector128<long> v0 = AdvSimd.AddPairwiseWidening(prodLo);
            Vector128<long> v1 = AdvSimd.AddPairwiseWidening(prodHi);

            return Vector128.Narrow(v0, v1);
        }

        {
            // Widen each half of the short vectors into two int vectors
            (Vector128<int> leftLo, Vector128<int> leftHi) = Vector128.Widen(left);
            (Vector128<int> rightLo, Vector128<int> rightHi) = Vector128.Widen(right);

            // Elementwise multiply: each int lane now holds the full 32-bit product
            Vector128<int> prodLo = leftLo * rightLo;
            Vector128<int> prodHi = leftHi * rightHi;

            // Extract the low and high parts of the products shuffling them to form a result we can add together.
            // Use out-of-bounds to zero out the unused lanes.
            Vector128<int> v0 = Vector128.Shuffle(prodLo, Vector128.Create(0, 2, 8, 8));
            Vector128<int> v1 = Vector128.Shuffle(prodHi, Vector128.Create(8, 8, 0, 2));
            Vector128<int> v2 = Vector128.Shuffle(prodLo, Vector128.Create(1, 3, 8, 8));
            Vector128<int> v3 = Vector128.Shuffle(prodHi, Vector128.Create(8, 8, 1, 3));

            return v0 + v1 + v2 + v3;
        }
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
    public static Vector128<short> MultiplyLow(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyLow(left, right);
        }

        // Widen each half of the short vectors into two int vectors
        (Vector128<int> leftLo, Vector128<int> leftHi) = Vector128.Widen(left);
        (Vector128<int> rightLo, Vector128<int> rightHi) = Vector128.Widen(right);

        // Elementwise multiply: each int lane now holds the full 32-bit product
        Vector128<int> prodLo = leftLo * rightLo;
        Vector128<int> prodHi = leftHi * rightHi;

        // Narrow the two int vectors back into one short vector
        return Vector128.Narrow(prodLo, prodHi);
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
    public static Vector128<short> MultiplyHigh(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyHigh(left, right);
        }

        // Widen each half of the short vectors into two int vectors
        (Vector128<int> leftLo, Vector128<int> leftHi) = Vector128.Widen(left);
        (Vector128<int> rightLo, Vector128<int> rightHi) = Vector128.Widen(right);

        // Elementwise multiply: each int lane now holds the full 32-bit product
        Vector128<int> prodLo = leftLo * rightLo;
        Vector128<int> prodHi = leftHi * rightHi;

        // Arithmetic shift right by 16 bits to extract the high word
        prodLo >>= 16;
        prodHi >>= 16;

        // Narrow the two int vectors back into one short vector
        return Vector128.Narrow(prodLo, prodHi);
    }

    /// <summary>
    /// Unpack and interleave 64-bit integers from the high half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 64-bit integers to unpack from the high half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 64-bit integers to unpack from the high half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 64-bit integers from the high
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<long> UnpackHigh(Vector128<long> left, Vector128<long> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipHigh(left, right);
        }

        return Vector128.Create(left.GetUpper(), right.GetUpper());
    }

    /// <summary>
    /// Unpack and interleave 64-bit integers from the low half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 64-bit integers to unpack from the low half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 64-bit integers to unpack from the low half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 64-bit integers from the low
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<long> UnpackLow(Vector128<long> left, Vector128<long> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipLow(left, right);
        }

        return Vector128.Create(left.GetLower(), right.GetLower());
    }

    /// <summary>
    /// Unpack and interleave 32-bit integers from the high half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 32-bit integers to unpack from the high half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 32-bit integers to unpack from the high half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 32-bit integers from the high
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> UnpackHigh(Vector128<int> left, Vector128<int> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipHigh(left, right);
        }

        Vector128<int> unpacked = Vector128.Create(left.GetUpper(), right.GetUpper());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 2, 1, 3));
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
    public static Vector128<int> UnpackLow(Vector128<int> left, Vector128<int> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipLow(left, right);
        }

        Vector128<int> unpacked = Vector128.Create(left.GetLower(), right.GetLower());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 2, 1, 3));
    }

    /// <summary>
    /// Unpack and interleave 16-bit integers from the high half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 16-bit integers to unpack from the high half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 16-bit integers to unpack from the high half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 16-bit integers from the high
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> UnpackHigh(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipHigh(left, right);
        }

        Vector128<short> unpacked = Vector128.Create(left.GetUpper(), right.GetUpper());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 4, 1, 5, 2, 6, 3, 7));
    }

    /// <summary>
    /// Unpack and interleave 16-bit integers from the low half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 16-bit integers to unpack from the low half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 16-bit integers to unpack from the low half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 16-bit integers from the low
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> UnpackLow(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipLow(left, right);
        }

        Vector128<short> unpacked = Vector128.Create(left.GetLower(), right.GetLower());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 4, 1, 5, 2, 6, 3, 7));
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
    public static Vector128<byte> UnpackHigh(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipHigh(left, right);
        }

        Vector128<byte> unpacked = Vector128.Create(left.GetUpper(), right.GetUpper());
        return Vector128.Shuffle(unpacked, Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
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
    public static Vector128<byte> UnpackLow(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipLow(left, right);
        }

        Vector128<byte> unpacked = Vector128.Create(left.GetLower(), right.GetLower());
        return Vector128.Shuffle(unpacked, Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
    }

    /// <summary>
    /// Unpack and interleave 8-bit signed integers from the high half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 8-bit signed integers to unpack from the high half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 8-bit signed integers to unpack from the high half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 8-bit signed integers from the high
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> UnpackHigh(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipHigh(left, right);
        }

        Vector128<sbyte> unpacked = Vector128.Create(left.GetUpper(), right.GetUpper());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
    }

    /// <summary>
    /// Unpack and interleave 8-bit signed integers from the low half of <paramref name="left"/> and <paramref name="right"/>
    /// and store the results in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 8-bit signed integers to unpack from the low half.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 8-bit signed integers to unpack from the low half.
    /// </param>
    /// <returns>
    /// A vector containing the unpacked and interleaved 8-bit signed integers from the low
    /// halves of <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> UnpackLow(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Arm64.ZipLow(left, right);
        }

        Vector128<sbyte> unpacked = Vector128.Create(left.GetLower(), right.GetLower());
        return Vector128.Shuffle(unpacked, Vector128.Create(0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
    }

    /// <summary>
    /// Subtract packed signed 16-bit integers in <paramref name="right"/> from packed signed 16-bit integers
    /// in <paramref name="left"/> using saturation, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 16-bit integers to subtract from.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 16-bit integers to subtract.
    /// </param>
    /// <returns>
    /// A vector containing the results of subtracting packed unsigned 16-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> SubtractSaturate(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.SubtractSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.SubtractSaturate(left, right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.SubtractSaturate(left, right);
        }

        // Widen inputs to 32-bit signed
        (Vector128<int> leftLo, Vector128<int> leftHi) = Vector128.Widen(left);
        (Vector128<int> rightLo, Vector128<int> rightHi) = Vector128.Widen(right);

        // Subtract
        Vector128<int> diffLo = leftLo - rightLo;
        Vector128<int> diffHi = leftHi - rightHi;

        // Clamp to signed 16-bit range
        Vector128<int> min = Vector128.Create((int)short.MinValue);
        Vector128<int> max = Vector128.Create((int)short.MaxValue);

        diffLo = Clamp(diffLo, min, max);
        diffHi = Clamp(diffHi, min, max);

        // Narrow back to 16 bit signed.
        return Vector128.Narrow(diffLo, diffHi);
    }

    /// <summary>
    /// Add packed unsigned 8-bit integers in <paramref name="right"/> from packed unsigned 8-bit integers
    /// in <paramref name="left"/> using saturation, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to add to.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to add.
    /// </param>
    /// <returns>
    /// A vector containing the results of adding packed unsigned 8-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> AddSaturate(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.AddSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.AddSaturate(left, right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.AddSaturate(left, right);
        }

        // Widen inputs to 16-bit
        (Vector128<ushort> leftLo, Vector128<ushort> leftHi) = Vector128.Widen(left);
        (Vector128<ushort> rightLo, Vector128<ushort> rightHi) = Vector128.Widen(right);

        // Add
        Vector128<ushort> sumLo = leftLo + rightLo;
        Vector128<ushort> sumHi = leftHi + rightHi;

        // Clamp to signed 8-bit range
        Vector128<ushort> max = Vector128.Create((ushort)byte.MaxValue);

        sumLo = Clamp(sumLo, Vector128<ushort>.Zero, max);
        sumHi = Clamp(sumHi, Vector128<ushort>.Zero, max);

        // Narrow back to bytes
        return Vector128.Narrow(sumLo, sumHi);
    }

    /// <summary>
    /// Subtract packed unsigned 8-bit integers in <paramref name="right"/> from packed unsigned 8-bit integers
    /// in <paramref name="left"/> using saturation, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to subtract from.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to subtract.
    /// </param>
    /// <returns>
    /// A vector containing the results of subtracting packed unsigned 8-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> SubtractSaturate(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.SubtractSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.SubtractSaturate(left, right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.SubtractSaturate(left, right);
        }

        // Widen inputs to 16-bit
        (Vector128<ushort> leftLo, Vector128<ushort> leftHi) = Vector128.Widen(left);
        (Vector128<ushort> rightLo, Vector128<ushort> rightHi) = Vector128.Widen(right);

        // Subtract
        Vector128<ushort> diffLo = leftLo - rightLo;
        Vector128<ushort> diffHi = leftHi - rightHi;

        // Clamp to signed 8-bit range
        Vector128<ushort> max = Vector128.Create((ushort)byte.MaxValue);

        diffLo = Clamp(diffLo, Vector128<ushort>.Zero, max);
        diffHi = Clamp(diffHi, Vector128<ushort>.Zero, max);

        // Narrow back to bytes
        return Vector128.Narrow(diffLo, diffHi);
    }

    /// <summary>
    /// Add packed unsigned 8-bit integers in <paramref name="right"/> from packed unsigned 8-bit integers
    /// in <paramref name="left"/> using saturation, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to add to.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to add.
    /// </param>
    /// <returns>
    /// A vector containing the results of adding packed unsigned 8-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> AddSaturate(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.AddSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.AddSaturate(left, right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.AddSaturate(left, right);
        }

        // Widen inputs to 16-bit
        (Vector128<short> leftLo, Vector128<short> leftHi) = Vector128.Widen(left);
        (Vector128<short> rightLo, Vector128<short> rightHi) = Vector128.Widen(right);

        // Add
        Vector128<short> sumLo = leftLo + rightLo;
        Vector128<short> sumHi = leftHi + rightHi;

        // Clamp to signed 8-bit range
        Vector128<short> min = Vector128.Create((short)sbyte.MinValue);
        Vector128<short> max = Vector128.Create((short)sbyte.MaxValue);

        sumLo = Clamp(sumLo, min, max);
        sumHi = Clamp(sumHi, min, max);

        // Narrow back to signed bytes
        return Vector128.Narrow(sumLo, sumHi);
    }

    /// <summary>
    /// Subtract packed signed 8-bit integers in <paramref name="right"/> from packed signed 8-bit integers
    /// in <paramref name="left"/> using saturation, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 8-bit integers to subtract from.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 8-bit integers to subtract.
    /// </param>
    /// <returns>
    /// A vector containing the results of subtracting packed signed 8-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> SubtractSaturate(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.SubtractSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.SubtractSaturate(left, right);
        }

        if (PackedSimd.IsSupported)
        {
            return PackedSimd.SubtractSaturate(left, right);
        }

        // Widen inputs to 16-bit
        (Vector128<short> leftLo, Vector128<short> leftHi) = Vector128.Widen(left);
        (Vector128<short> rightLo, Vector128<short> rightHi) = Vector128.Widen(right);

        // Subtract
        Vector128<short> diffLo = leftLo - rightLo;
        Vector128<short> diffHi = leftHi - rightHi;

        // Clamp to signed 8-bit range
        Vector128<short> min = Vector128.Create((short)sbyte.MinValue);
        Vector128<short> max = Vector128.Create((short)sbyte.MaxValue);

        diffLo = Clamp(diffLo, min, max);
        diffHi = Clamp(diffHi, min, max);

        // Narrow back to signed bytes
        return Vector128.Narrow(diffLo, diffHi);
    }
}
