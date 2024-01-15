// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
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
internal static class Vector128Utilities
{
    /// <summary>
    /// Gets a value indicating whether shuffle operations are supported.
    /// </summary>
    public static bool SupportsShuffleFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Sse.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether shuffle operations are supported.
    /// </summary>
    public static bool SupportsShuffleByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Ssse3.IsSupported || AdvSimd.Arm64.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether right align operations are supported.
    /// </summary>
    public static bool SupportsRightAlign
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
    public static Vector128<float> Shuffle(Vector128<float> vector, [ConstantExpected] byte control)
    {
        if (Sse.IsSupported)
        {
            return Sse.Shuffle(vector, vector, control);
        }

        ThrowUnreachableException();
        return default;
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
    public static Vector128<byte> Shuffle(Vector128<byte> vector, Vector128<byte> indices)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(vector, indices);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.VectorTableLookup(vector, indices);
        }

        ThrowUnreachableException();
        return default;
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
            return AdvSimd.ExtractVector128(Vector128<byte>.Zero, value, numBytes);
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

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
