// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the fixed-point arithmetic shared by the scalar and SIMD AV1 one-dimensional transform kernels.
/// </summary>
internal static class Av1Transform1dMath
{
    /// <summary>
    /// The fixed-point representation of the square root of two with twelve fractional bits.
    /// </summary>
    public const int NewSqrt2 = 5793;

    /// <summary>
    /// The number of fractional bits in <see cref="NewSqrt2"/>.
    /// </summary>
    public const int NewSqrt2Bits = 12;

    /// <summary>
    /// Calculates one output of a rounded, weighted two-input butterfly.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first input value.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second input value.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <returns>The rounded fixed-point result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HalfButterfly(int weight0, int input0, int weight1, int input1, int cosBit)
    {
        // The scalar path widens before multiplication so it remains an exact oracle for stress inputs outside the
        // bounded production range as well as for conformant transform stages.
        long weightedSum = ((long)weight0 * input0) + ((long)weight1 * input1);
        return (int)((weightedSum + (1L << (cosBit - 1))) >> cosBit);
    }

    /// <summary>
    /// Clamps a transform-stage value to the signed range represented by a bit count.
    /// </summary>
    /// <param name="value">The transform-stage value.</param>
    /// <param name="bitCount">The width of the signed range.</param>
    /// <returns>The value clamped to the permitted stage range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, byte bitCount)
    {
        long maximum = (1L << (bitCount - 1)) - 1;
        long minimum = -(1L << (bitCount - 1));
        return (int)Math.Clamp(value, minimum, maximum);
    }

    /// <summary>
    /// Calculates four outputs of a rounded, weighted two-input butterfly in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first four input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second four input values.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <returns>The four rounded fixed-point results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> HalfButterfly(int weight0, Vector128<int> input0, int weight1, Vector128<int> input1, int cosBit)
    {
        // The transform stage ranges bound this sequence so the low 32-bit products and sum produce the normative
        // result. Keeping those operations in Int32 lanes maps directly to the optimized SSE and Neon kernels.
        Vector128<int> weightedSum = (input0 * weight0) + (input1 * weight1);
        return (weightedSum + Vector128.Create(1 << (cosBit - 1))) >> cosBit;
    }

    /// <summary>
    /// Calculates eight outputs of a rounded, weighted two-input butterfly in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first eight input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second eight input values.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <returns>The eight rounded fixed-point results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> HalfButterfly(int weight0, Vector256<int> input0, int weight1, Vector256<int> input1, int cosBit)
    {
        // The bounded stage inputs allow the complete butterfly to remain in 32-bit lanes, letting the JIT emit the
        // AVX2 multiply/add/shift sequence instead of splitting every input into widened 64-bit vectors.
        Vector256<int> weightedSum = (input0 * weight0) + (input1 * weight1);
        return (weightedSum + Vector256.Create(1 << (cosBit - 1))) >> cosBit;
    }

    /// <summary>
    /// Clamps four transform-stage values to the signed range represented by a bit count.
    /// </summary>
    /// <param name="value">The four transform-stage values.</param>
    /// <param name="bitCount">The width of the signed range.</param>
    /// <returns>The values clamped to the permitted stage range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Clamp(Vector128<int> value, byte bitCount)
    {
        int maximum = (1 << (bitCount - 1)) - 1;
        int minimum = -(1 << (bitCount - 1));
        return Vector128.Clamp(value, Vector128.Create(minimum), Vector128.Create(maximum));
    }

    /// <summary>
    /// Clamps eight transform-stage values to the signed range represented by a bit count.
    /// </summary>
    /// <param name="value">The eight transform-stage values.</param>
    /// <param name="bitCount">The width of the signed range.</param>
    /// <returns>The values clamped to the permitted stage range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Clamp(Vector256<int> value, byte bitCount)
    {
        int maximum = (1 << (bitCount - 1)) - 1;
        int minimum = -(1 << (bitCount - 1));
        return Vector256.Clamp(value, Vector256.Create(minimum), Vector256.Create(maximum));
    }

    /// <summary>
    /// Multiplies and rounds four fixed-point values in parallel.
    /// </summary>
    /// <param name="value">The four values to scale.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="fractionalBits">The number of fractional bits in the multiplier.</param>
    /// <returns>The four rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> MultiplyRound(Vector128<int> value, int multiplier, int fractionalBits)
        => HalfButterfly(multiplier, value, 0, Vector128<int>.Zero, fractionalBits);

    /// <summary>
    /// Multiplies and rounds eight fixed-point values in parallel.
    /// </summary>
    /// <param name="value">The eight values to scale.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="fractionalBits">The number of fractional bits in the multiplier.</param>
    /// <returns>The eight rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> MultiplyRound(Vector256<int> value, int multiplier, int fractionalBits)
        => HalfButterfly(multiplier, value, 0, Vector256<int>.Zero, fractionalBits);

    /// <summary>
    /// Multiplies four scalar inputs by fixed-point weights and rounds their sum.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first input value.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second input value.</param>
    /// <param name="weight2">The third fixed-point weight.</param>
    /// <param name="input2">The third input value.</param>
    /// <param name="weight3">The fourth fixed-point weight.</param>
    /// <param name="input3">The fourth input value.</param>
    /// <param name="fractionalBits">The number of fractional bits in each weight.</param>
    /// <returns>The rounded fixed-point sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MultiplyAdd4(int weight0, int input0, int weight1, int input1, int weight2, int input2, int weight3, int input3, int fractionalBits)
    {
        long weightedSum = ((long)weight0 * input0) + ((long)weight1 * input1) + ((long)weight2 * input2) + ((long)weight3 * input3);
        return (int)((weightedSum + (1L << (fractionalBits - 1))) >> fractionalBits);
    }

    /// <summary>
    /// Multiplies four sets of four inputs by fixed-point weights and rounds their sums.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first four input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second four input values.</param>
    /// <param name="weight2">The third fixed-point weight.</param>
    /// <param name="input2">The third four input values.</param>
    /// <param name="weight3">The fourth fixed-point weight.</param>
    /// <param name="input3">The fourth four input values.</param>
    /// <param name="fractionalBits">The number of fractional bits in each weight.</param>
    /// <returns>The four rounded fixed-point sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> MultiplyAdd4(
        int weight0,
        Vector128<int> input0,
        int weight1,
        Vector128<int> input1,
        int weight2,
        Vector128<int> input2,
        int weight3,
        Vector128<int> input3,
        int fractionalBits)
    {
        // The four-point ADST factorization has the same bounded-intermediate contract as the butterfly stages.
        // Accumulating in Int32 lanes preserves the normative result and avoids eight widening operations per sum.
        Vector128<int> weightedSum = (input0 * weight0) + (input1 * weight1) + (input2 * weight2) + (input3 * weight3);
        return (weightedSum + Vector128.Create(1 << (fractionalBits - 1))) >> fractionalBits;
    }

    /// <summary>
    /// Multiplies four sets of eight inputs by fixed-point weights and rounds their sums.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first eight input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second eight input values.</param>
    /// <param name="weight2">The third fixed-point weight.</param>
    /// <param name="input2">The third eight input values.</param>
    /// <param name="weight3">The fourth fixed-point weight.</param>
    /// <param name="input3">The fourth eight input values.</param>
    /// <param name="fractionalBits">The number of fractional bits in each weight.</param>
    /// <returns>The eight rounded fixed-point sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> MultiplyAdd4(
        int weight0,
        Vector256<int> input0,
        int weight1,
        Vector256<int> input1,
        int weight2,
        Vector256<int> input2,
        int weight3,
        Vector256<int> input3,
        int fractionalBits)
    {
        Vector256<int> weightedSum = (input0 * weight0) + (input1 * weight1) + (input2 * weight2) + (input3 * weight3);
        return (weightedSum + Vector256.Create(1 << (fractionalBits - 1))) >> fractionalBits;
    }
}
