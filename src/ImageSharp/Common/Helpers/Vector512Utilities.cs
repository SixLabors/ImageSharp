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
internal static class Vector512Utilities
{
    /// <summary>
    /// Gets a value indicating whether shuffle float operations are supported.
    /// </summary>
    public static bool SupportsShuffleFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx512F.IsSupported || Avx.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether shuffle byte operations are supported.
    /// </summary>
    public static bool SupportsShuffleByte
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
    public static Vector512<float> Shuffle(Vector512<float> vector, [ConstantExpected] byte control)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.Shuffle(vector, vector, control);
        }

        if (Avx.IsSupported)
        {
            Vector256<float> lower = vector.GetLower();
            Vector256<float> upper = vector.GetUpper();
            return Vector512.Create(Avx.Shuffle(lower, lower, control), Avx.Shuffle(upper, upper, control));
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
    public static Vector512<byte> Shuffle(Vector512<byte> vector, Vector512<byte> indices)
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

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
