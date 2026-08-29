// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the fixed-point arithmetic shared by the scalar and SIMD AV1 one-dimensional transform kernels.
/// </summary>
/// <remarks>
/// The general transform path keeps one independent axis in each signed 32-bit lane. Low-bit-depth forward transforms
/// additionally use signed 16-bit lanes and whole-butterfly AVX2 or AVX-512 multiply-add operations, matching AV1's
/// stage saturation points before packing. Scalar overloads preserve the same rounding and serve as the fallback.
/// </remarks>
internal static class Av1Transform1dMath
{
    /// <summary>
    /// The signed stage width whose fixed-point terminal operations require widened SIMD intermediates.
    /// </summary>
    public const byte WidenedIntermediateBitCount = 20;

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
    /// Calculates sixteen outputs of a rounded, weighted two-input butterfly in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first sixteen input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second sixteen input values.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <returns>The sixteen rounded fixed-point results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> HalfButterfly(int weight0, Vector512<int> input0, int weight1, Vector512<int> input1, int cosBit)
    {
        // AV1 stage ranges keep the products and sum inside the normative wrapping Int32 domain. Preserving that lane
        // width lets 512-bit SIMD evaluate sixteen independent transform axes without widened intermediate vectors.
        Vector512<int> weightedSum = (input0 * weight0) + (input1 * weight1);
        return (weightedSum + Vector512.Create(1 << (cosBit - 1))) >> cosBit;
    }

    /// <summary>
    /// Adds and subtracts thirty-two pairs of low-bit-depth transform values with signed saturation.
    /// </summary>
    /// <param name="input0">The first thirty-two input values.</param>
    /// <param name="input1">The second thirty-two input values.</param>
    /// <param name="sum">The thirty-two saturated sums.</param>
    /// <param name="difference">The thirty-two saturated differences.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSubtract(
        in Vector512<short> input0,
        in Vector512<short> input1,
        out Vector512<short> sum,
        out Vector512<short> difference)
    {
        // Read both operands before either destination is written because libaom deliberately permits an input
        // buffer to alias one or both outputs while alternating between its two fixed transform-stage buffers.
        Vector512<short> left = input0;
        Vector512<short> right = input1;

        sum = Vector512.AddSaturate(left, right);
        difference = Vector512.SubtractSaturate(left, right);
    }

    /// <summary>
    /// Calculates both outputs of thirty-two rounded low-bit-depth butterflies in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input0">The first thirty-two input values.</param>
    /// <param name="input1">The second thirty-two input values.</param>
    /// <param name="output0">The first thirty-two saturated, rounded results.</param>
    /// <param name="output1">The second thirty-two saturated, rounded results.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding offset for the widened intermediate values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Butterfly(
        int weight0,
        int weight1,
        in Vector512<short> input0,
        in Vector512<short> input1,
        out Vector512<short> output0,
        out Vector512<short> output1,
        int cosBit,
        in Vector512<int> rounding)
    {
        // VPMADDWD evaluates adjacent Int16 products into Int32 lanes. Both outputs reuse the same interleaved
        // inputs, matching libaom's whole butterfly instead of loading and unpacking each input pair twice.
        Vector512<short> left = input0;
        Vector512<short> right = input1;
        Vector512<short> interleavedLower = Avx512BW.UnpackLow(left, right);
        Vector512<short> interleavedUpper = Avx512BW.UnpackHigh(left, right);
        Vector512<short> weight0Values = Vector512.Create((short)weight0);
        Vector512<short> weight1Values = Vector512.Create((short)weight1);
        Vector512<short> weights0 = Avx512BW.UnpackLow(weight0Values, weight1Values);
        Vector512<short> weights1 = Avx512BW.UnpackLow(weight1Values, Vector512.Create((short)-weight0));
        Vector512<int> output0Lower = Avx512BW.MultiplyAddAdjacent(interleavedLower, weights0);
        Vector512<int> output0Upper = Avx512BW.MultiplyAddAdjacent(interleavedUpper, weights0);
        Vector512<int> output1Lower = Avx512BW.MultiplyAddAdjacent(interleavedLower, weights1);
        Vector512<int> output1Upper = Avx512BW.MultiplyAddAdjacent(interleavedUpper, weights1);

        output0Lower = (output0Lower + rounding) >> cosBit;
        output0Upper = (output0Upper + rounding) >> cosBit;
        output1Lower = (output1Lower + rounding) >> cosBit;
        output1Upper = (output1Upper + rounding) >> cosBit;

        // VPACKSSDW restores the original lane order within each 128-bit block and narrows with the saturation
        // required by the low-bit-depth AV1 stage arithmetic.
        output0 = Avx512BW.PackSignedSaturate(output0Lower, output0Upper);
        output1 = Avx512BW.PackSignedSaturate(output1Lower, output1Upper);
    }

    /// <summary>
    /// Adds and subtracts sixteen pairs of low-bit-depth transform values with signed saturation.
    /// </summary>
    /// <param name="input0">The first sixteen input values.</param>
    /// <param name="input1">The second sixteen input values.</param>
    /// <param name="sum">The sixteen saturated sums.</param>
    /// <param name="difference">The sixteen saturated differences.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSubtract(
        in Vector256<short> input0,
        in Vector256<short> input1,
        out Vector256<short> sum,
        out Vector256<short> difference)
    {
        Vector256<short> left = input0;
        Vector256<short> right = input1;

        sum = Vector256.AddSaturate(left, right);
        difference = Vector256.SubtractSaturate(left, right);
    }

    /// <summary>
    /// Calculates both outputs of sixteen rounded low-bit-depth butterflies in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input0">The first sixteen input values.</param>
    /// <param name="input1">The second sixteen input values.</param>
    /// <param name="output0">The first sixteen saturated, rounded results.</param>
    /// <param name="output1">The second sixteen saturated, rounded results.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding offset for the widened intermediate values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Butterfly(
        int weight0,
        int weight1,
        in Vector256<short> input0,
        in Vector256<short> input1,
        out Vector256<short> output0,
        out Vector256<short> output1,
        int cosBit,
        in Vector256<int> rounding)
    {
        Vector256<short> left = input0;
        Vector256<short> right = input1;
        Vector256<short> interleavedLower = Avx2.UnpackLow(left, right);
        Vector256<short> interleavedUpper = Avx2.UnpackHigh(left, right);
        Vector256<short> weight0Values = Vector256.Create((short)weight0);
        Vector256<short> weight1Values = Vector256.Create((short)weight1);
        Vector256<short> weights0 = Avx2.UnpackLow(weight0Values, weight1Values);
        Vector256<short> weights1 = Avx2.UnpackLow(weight1Values, Vector256.Create((short)-weight0));
        Vector256<int> output0Lower = Avx2.MultiplyAddAdjacent(interleavedLower, weights0);
        Vector256<int> output0Upper = Avx2.MultiplyAddAdjacent(interleavedUpper, weights0);
        Vector256<int> output1Lower = Avx2.MultiplyAddAdjacent(interleavedLower, weights1);
        Vector256<int> output1Upper = Avx2.MultiplyAddAdjacent(interleavedUpper, weights1);

        output0Lower = (output0Lower + rounding) >> cosBit;
        output0Upper = (output0Upper + rounding) >> cosBit;
        output1Lower = (output1Lower + rounding) >> cosBit;
        output1Upper = (output1Upper + rounding) >> cosBit;

        output0 = Avx2.PackSignedSaturate(output0Lower, output0Upper);
        output1 = Avx2.PackSignedSaturate(output1Lower, output1Upper);
    }

    /// <summary>
    /// Calculates both outputs of eight rounded low-bit-depth butterflies in parallel.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input0">The first eight input values.</param>
    /// <param name="input1">The second eight input values.</param>
    /// <param name="output0">The first eight saturated, rounded results.</param>
    /// <param name="output1">The second eight saturated, rounded results.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding offset for the widened intermediate values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Butterfly(
        int weight0,
        int weight1,
        in Vector128<short> input0,
        in Vector128<short> input1,
        out Vector128<short> output0,
        out Vector128<short> output1,
        int cosBit,
        in Vector128<int> rounding)
    {
        Vector128<short> left = input0;
        Vector128<short> right = input1;

        if (Sse2.IsSupported)
        {
            // PMADDWD is the native x86 form of Highway's pairwise widening multiply-add. Interleaving once lets
            // both butterfly outputs reuse the same input arrangement before signed-saturating demotion.
            Vector128<short> interleavedLower = Sse2.UnpackLow(left, right);
            Vector128<short> interleavedUpper = Sse2.UnpackHigh(left, right);
            Vector128<short> weight0Values = Vector128.Create((short)weight0);
            Vector128<short> weight1Values = Vector128.Create((short)weight1);
            Vector128<short> weights0 = Sse2.UnpackLow(weight0Values, weight1Values);
            Vector128<short> weights1 = Sse2.UnpackLow(weight1Values, Vector128.Create((short)-weight0));
            Vector128<int> output0Lower = Sse2.MultiplyAddAdjacent(interleavedLower, weights0);
            Vector128<int> output0Upper = Sse2.MultiplyAddAdjacent(interleavedUpper, weights0);
            Vector128<int> output1Lower = Sse2.MultiplyAddAdjacent(interleavedLower, weights1);
            Vector128<int> output1Upper = Sse2.MultiplyAddAdjacent(interleavedUpper, weights1);

            output0Lower = (output0Lower + rounding) >> cosBit;
            output0Upper = (output0Upper + rounding) >> cosBit;
            output1Lower = (output1Lower + rounding) >> cosBit;
            output1Upper = (output1Upper + rounding) >> cosBit;

            output0 = Sse2.PackSignedSaturate(output0Lower, output0Upper);
            output1 = Sse2.PackSignedSaturate(output1Lower, output1Upper);
            return;
        }

        // AdvSimd and WebAssembly do not expose PMADDWD. Widen both inputs once and retain the complete operation
        // in Vector128 lanes so those targets still execute the transform as a whole SIMD butterfly.
        (Vector128<int> leftLower, Vector128<int> leftUpper) = Vector128.Widen(left);
        (Vector128<int> rightLower, Vector128<int> rightUpper) = Vector128.Widen(right);

        Vector128<int> weight0Vector = Vector128.Create(weight0);
        Vector128<int> weight1Vector = Vector128.Create(weight1);
        Vector128<int> output0LowerVector = ((leftLower * weight0Vector) + (rightLower * weight1Vector) + rounding) >> cosBit;
        Vector128<int> output0UpperVector = ((leftUpper * weight0Vector) + (rightUpper * weight1Vector) + rounding) >> cosBit;
        Vector128<int> output1LowerVector = ((leftLower * weight1Vector) - (rightLower * weight0Vector) + rounding) >> cosBit;
        Vector128<int> output1UpperVector = ((leftUpper * weight1Vector) - (rightUpper * weight0Vector) + rounding) >> cosBit;
        Vector128<int> minimum = Vector128.Create((int)short.MinValue);
        Vector128<int> maximum = Vector128.Create((int)short.MaxValue);

        output0LowerVector = Vector128.Clamp(output0LowerVector, minimum, maximum);
        output0UpperVector = Vector128.Clamp(output0UpperVector, minimum, maximum);
        output1LowerVector = Vector128.Clamp(output1LowerVector, minimum, maximum);
        output1UpperVector = Vector128.Clamp(output1UpperVector, minimum, maximum);
        output0 = Vector128.Narrow(output0LowerVector, output0UpperVector);
        output1 = Vector128.Narrow(output1LowerVector, output1UpperVector);
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
    /// Clamps sixteen transform-stage values to the signed range represented by a bit count.
    /// </summary>
    /// <param name="value">The sixteen transform-stage values.</param>
    /// <param name="bitCount">The width of the signed range.</param>
    /// <returns>The values clamped to the permitted stage range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Clamp(Vector512<int> value, byte bitCount)
    {
        int maximum = (1 << (bitCount - 1)) - 1;
        int minimum = -(1 << (bitCount - 1));
        return Vector512.Clamp(value, Vector512.Create(minimum), Vector512.Create(maximum));
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
    /// Multiplies and rounds sixteen fixed-point values in parallel.
    /// </summary>
    /// <param name="value">The sixteen values to scale.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="fractionalBits">The number of fractional bits in the multiplier.</param>
    /// <returns>The sixteen rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> MultiplyRound(Vector512<int> value, int multiplier, int fractionalBits)
        => HalfButterfly(multiplier, value, 0, Vector512<int>.Zero, fractionalBits);

    /// <summary>
    /// Multiplies and rounds four fixed-point values with signed sixty-four-bit intermediate lanes.
    /// </summary>
    /// <param name="value">The four values to scale.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="fractionalBits">The number of fractional bits in the multiplier.</param>
    /// <returns>The four rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> MultiplyRoundWidened(Vector128<int> value, int multiplier, int fractionalBits)
    {
        (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(value);
        Vector128<long> rounding = Vector128.Create(1L << (fractionalBits - 1));

        // Pinned libaom's high-bit-depth identity kernels multiply in signed 64-bit lanes. Widen before both the
        // product and rounding addition so a valid 20-bit twelve-bit row value cannot wrap through Int32.
        lower = ((lower * multiplier) + rounding) >> fractionalBits;
        upper = ((upper * multiplier) + rounding) >> fractionalBits;
        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Multiplies and rounds eight fixed-point values with signed sixty-four-bit intermediate lanes.
    /// </summary>
    /// <param name="value">The eight values to scale.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="fractionalBits">The number of fractional bits in the multiplier.</param>
    /// <returns>The eight rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> MultiplyRoundWidened(Vector256<int> value, int multiplier, int fractionalBits)
    {
        (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(value);
        Vector256<long> rounding = Vector256.Create(1L << (fractionalBits - 1));

        lower = ((lower * multiplier) + rounding) >> fractionalBits;
        upper = ((upper * multiplier) + rounding) >> fractionalBits;
        return Vector256.Narrow(lower, upper);
    }

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

    /// <summary>
    /// Multiplies four sets of sixteen inputs by fixed-point weights and rounds their sums.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first sixteen input values.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second sixteen input values.</param>
    /// <param name="weight2">The third fixed-point weight.</param>
    /// <param name="input2">The third sixteen input values.</param>
    /// <param name="weight3">The fourth fixed-point weight.</param>
    /// <param name="input3">The fourth sixteen input values.</param>
    /// <param name="fractionalBits">The number of fractional bits in each weight.</param>
    /// <returns>The sixteen rounded fixed-point sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> MultiplyAdd4(
        int weight0,
        Vector512<int> input0,
        int weight1,
        Vector512<int> input1,
        int weight2,
        Vector512<int> input2,
        int weight3,
        Vector512<int> input3,
        int fractionalBits)
    {
        Vector512<int> weightedSum = (input0 * weight0) + (input1 * weight1) + (input2 * weight2) + (input3 * weight3);
        return (weightedSum + Vector512.Create(1 << (fractionalBits - 1))) >> fractionalBits;
    }

    /// <summary>
    /// Multiplies four sets of four inputs in signed thirty-two-bit lanes, then widens the terminal rounding step.
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
    public static Vector128<int> MultiplyAdd4WidenedRound(
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
        // libaom keeps conformant ADST4 sine products and their factorized sums in Int32, then widens the terminal
        // scaling and rounding. Preserve that exact boundary instead of widening every transform multiplication.
        Vector128<int> weightedSum = (input0 * weight0) + (input1 * weight1) + (input2 * weight2) + (input3 * weight3);
        return MultiplyRoundWidened(weightedSum, 1, fractionalBits);
    }

    /// <summary>
    /// Multiplies four sets of eight inputs in signed thirty-two-bit lanes, then widens the terminal rounding step.
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
    public static Vector256<int> MultiplyAdd4WidenedRound(
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
        return MultiplyRoundWidened(weightedSum, 1, fractionalBits);
    }
}
