// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
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
internal static class Vector256Utilities
{
    /// <summary>
    /// Gets a value indicating whether shuffle byte operations are supported.
    /// </summary>
    public static bool SupportsShuffleFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx.IsSupported || Sse.IsSupported;
    }

    /// <summary>
    /// Gets a value indicating whether shuffle byte operations are supported.
    /// </summary>
    public static bool SupportsShuffleByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx2.IsSupported;
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using a set of indices.
    /// </summary>
    /// <param name="vector">The input vector from which values are selected.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Shuffle(Vector256<float> vector, [ConstantExpected] byte control)
    {
        if (Avx.IsSupported)
        {
            return Avx.Shuffle(vector, vector, control);
        }

        if (Sse.IsSupported)
        {
            Vector128<float> lower = vector.GetLower();
            Vector128<float> upper = vector.GetUpper();
            return Vector256.Create(Sse.Shuffle(lower, lower, control), Sse.Shuffle(upper, upper, control));
        }

        ThrowUnreachableException();
        return default;
    }

    /// <summary>
    /// Creates a new vector by selecting values from an input vector using a set of indices.</summary>
    /// <param name="vector">
    /// The input vector from which values are selected.</param>
    /// <param name="indices">
    /// The per-element indices used to select a value from <paramref name="vector" />.
    /// </param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Shuffle(Vector256<byte> vector, Vector256<byte> indices)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Shuffle(vector, indices);
        }

        ThrowUnreachableException();
        return default;
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

        if (Sse2.IsSupported)
        {
            Vector128<int> lower = Sse2.ConvertToVector128Int32(vector.GetLower());
            Vector128<int> upper = Sse2.ConvertToVector128Int32(vector.GetUpper());
            return Vector256.Create(lower, upper);
        }

        Vector256<float> sign = vector & Vector256.Create(-0.0f);
        Vector256<float> val_2p23_f32 = sign | Vector256.Create(8388608.0f);

        val_2p23_f32 = (vector + val_2p23_f32) - val_2p23_f32;
        return Vector256.ConvertToInt32(val_2p23_f32 | sign);
    }

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
