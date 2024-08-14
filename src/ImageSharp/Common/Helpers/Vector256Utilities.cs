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

    /// <summary>
    /// Performs a multiply-add operation on three vectors, where each element of the resulting vector is the
    /// product of corresponding elements in <paramref name="a"/> and <paramref name="b"/> added to the
    /// corresponding element in <paramref name="c"/>.
    /// If the CPU supports FMA (Fused Multiply-Add) instructions, the operation is performed as a single
    /// fused operation for better performance and precision.
    /// </summary>
    /// <param name="a">The first vector of single-precision floating-point numbers to be multiplied.</param>
    /// <param name="b">The second vector of single-precision floating-point numbers to be multiplied.</param>
    /// <param name="c">The vector of single-precision floating-point numbers to be added to the product of
    /// <paramref name="a"/> and <paramref name="b"/>.</param>
    /// <returns>
    /// A <see cref="Vector256{Single}"/> where each element is the result of multiplying the corresponding elements
    /// of <paramref name="a"/> and <paramref name="b"/>, and then adding the corresponding element from <paramref name="c"/>.
    /// </returns>
    /// <remarks>
    /// If the FMA (Fused Multiply-Add) instruction set is supported by the CPU, the operation is performed using
    /// <see cref="Fma.MultiplyAdd(Vector256{float}, Vector256{float}, Vector256{float})"/>. This approach can result
    /// in slightly different results compared to performing the multiplication and addition separately due to
    /// differences in how floating-point
    /// rounding is handled.
    /// <para>
    /// If FMA is not supported, the operation is performed as a separate multiplication and addition. This might lead
    /// to a minor difference in precision compared to the fused operation, particularly in cases where numerical accuracy
    /// is critical.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> MultiplyAdd(Vector256<float> a, Vector256<float> b, Vector256<float> c)
    {
        if (Fma.IsSupported)
        {
            return Fma.MultiplyAdd(a, b, c);
        }

        return (a * b) + c;
    }

    [DoesNotReturn]
    private static void ThrowUnreachableException() => throw new UnreachableException();
}
