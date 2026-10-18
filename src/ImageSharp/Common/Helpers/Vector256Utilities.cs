// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// Selects each 32-bit element from one of two vectors by index.
    /// </summary>
    /// <param name="lower">The vector that supplies indices 0 through 7.</param>
    /// <param name="indices">The source index of each destination element.</param>
    /// <param name="upper">The vector that supplies indices 8 through 15.</param>
    /// <returns>The selected elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> PermuteVar8x32x2(Vector256<int> lower, Vector256<int> indices, Vector256<int> upper)
    {
        if (Avx512F.VL.IsSupported)
        {
            return Avx512F.VL.PermuteVar8x32x2(lower, indices, upper);
        }

        // The low three bits select a lane; bit three selects its source register.
        // Masking to 0..7 keeps every shuffle index valid, so ShuffleNative needs no out-of-range zeroing.
        Vector256<int> within = indices & Vector256.Create(7);
        Vector256<int> selector = Vector256.Equals(indices & Vector256.Create(8), Vector256.Create(8));
        return Vector256.ConditionalSelect(selector, Vector256.ShuffleNative(upper, within), Vector256.ShuffleNative(lower, within));
    }

    /// <summary>
    /// Approximates base-two exponentials in single-precision SIMD lanes.
    /// </summary>
    /// <param name="value">The binary exponents.</param>
    /// <returns>The powers of two, including the binary32 underflow and overflow ranges.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Exp2(Vector256<float> value)
    {
        const float SignificandScale = 1 << 23;
        const float Offset = 121.274057500F;
        const float Linear = 1.490129070F;
        const float Numerator = 27.728023300F;
        const float Denominator = 4.84252568F;
        const float InfinityBits = 0x7F800000;
        const float SubnormalScale = 1F / (1 << 24);

        Vector256<float> normal = Vector256.GreaterThanOrEqual(value, Vector256.Create(-126F))
            & Vector256.LessThanOrEqual(value, Vector256.Create(127F));
        bool hasSpecial = normal.AsUInt32() != Vector256<uint>.AllBitsSet;
        Vector256<float> exponent = value;
        Vector256<float> low = default;
        Vector256<float> high = default;
        if (hasSpecial)
        {
            // Build small outputs at 2^24 times their magnitude and large outputs at half their magnitude.
            // Scaling afterwards retains the float range without constructing invalid exponent bits.
            low = Vector256.LessThan(value, Vector256.Create(-126F));
            high = Vector256.GreaterThan(value, Vector256.Create(127F));
            exponent += Vector256.ConditionalSelect(low, Vector256.Create(24F), Vector256<float>.Zero);
            exponent -= Vector256.ConditionalSelect(high, Vector256<float>.One, Vector256<float>.Zero);
        }

        // Approximate exponent and mantissa bits together. Reinterpretation replaces a separate exponentiation step.
        Vector256<float> fraction = exponent - Vector256.Floor(exponent);
        Vector256<float> bits = exponent + Vector256.Create(Offset) - (Vector256.Create(Linear) * fraction);
        bits += Vector256.Create(Numerator) / (Vector256.Create(Denominator) - fraction);
        bits *= Vector256.Create(SignificandScale);
        if (hasSpecial)
        {
            bits = Vector256.Min(Vector256.Max(bits, Vector256<float>.Zero), Vector256.Create(InfinityBits));
        }

        // The encoded bit range is within Int32, so native truncation needs no saturation fixups.
        Vector256<float> result = Vector256.ConvertToInt32Native(bits).AsSingle();
        if (hasSpecial)
        {
            result *= Vector256.ConditionalSelect(low, Vector256.Create(SubnormalScale), Vector256<float>.One);
            result *= Vector256.ConditionalSelect(high, Vector256.Create(2F), Vector256<float>.One);
            result = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256.Create(-150F)), Vector256<float>.Zero, result);
            result = Vector256.ConditionalSelect(Vector256.GreaterThan(value, Vector256.Create(128F)), Vector256.Create(float.PositiveInfinity), result);
            result = Vector256.ConditionalSelect(Vector256.Equals(value, value), result, value);
        }

        return result;
    }

    /// <summary>
    /// Approximates natural exponentials without widening float SIMD lanes.
    /// </summary>
    /// <param name="value">The natural logarithms of the outputs.</param>
    /// <returns>The exponentials in the original lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Exp(Vector256<float> value)
    {
        const float InverseLogTwo = 1.4426950408889634F;
        return Exp2(value * Vector256.Create(InverseLogTwo));
    }

    /// <summary>
    /// Raises nonnegative values to a nonzero finite power with single-precision SIMD log2 and exp2 approximations.
    /// </summary>
    /// <param name="value">The nonnegative bases.</param>
    /// <param name="exponent">The nonzero finite exponent shared by every component.</param>
    /// <returns>The powers in the original lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Pow(Vector256<float> value, float exponent)
    {
        const float ExponentScale = 1F / (1 << 23);
        const float Offset = 124.225514990F;
        const float Linear = 1.498030302F;
        const float Numerator = 1.725879990F;
        const float Denominator = 0.3520887068F;
        const uint SmallestNormalBits = 0x00800000;
        const float SubnormalScale = 1 << 24;

        // Zero uses a safe logarithm input and is restored after exponentiation. Keep this rule in
        // the shared helper so finite gamma stages do not need the general power dispatcher.
        Vector256<float> zero = Vector256.Equals(value, Vector256<float>.Zero);
        Vector256<float> positive = Vector256.ConditionalSelect(zero, Vector256<float>.One, value);
        Vector256<uint> small = Vector256.LessThan(positive.AsUInt32(), Vector256.Create(SmallestNormalBits));
        bool hasSmall = small != Vector256<uint>.Zero;
        Vector256<float> normalized = positive;
        if (hasSmall)
        {
            normalized = Vector256.ConditionalSelect(small.AsSingle(), positive * Vector256.Create(SubnormalScale), positive);
        }

        // The float encoding supplies the exponent. The rational term approximates the mantissa's logarithm.
        Vector256<int> bits = normalized.AsInt32();
        Vector256<float> mantissa = ((bits & Vector256.Create(0x007FFFFF)) | Vector256.Create(0x3F000000)).AsSingle();
        Vector256<float> logarithm = (Vector256.ConvertToSingle(bits) * Vector256.Create(ExponentScale)) - Vector256.Create(Offset);
        logarithm -= Vector256.Create(Linear) * mantissa;
        logarithm -= Vector256.Create(Numerator) / (Vector256.Create(Denominator) + mantissa);
        if (hasSmall)
        {
            logarithm -= Vector256.ConditionalSelect(small.AsSingle(), Vector256.Create(24F), Vector256<float>.Zero);
        }

        Vector256<float> result = Exp2(logarithm * Vector256.Create(exponent));

        // Exponent zero is handled before this helper. Other powers of zero give zero or positive infinity.
        float zeroResult = exponent < 0F ? float.PositiveInfinity : 0F;
        result = Vector256.ConditionalSelect(zero, Vector256.Create(zeroResult), result);

        if (Vector256.EqualsAny(value, Vector256.Create(float.PositiveInfinity)))
        {
            float infinityResult = exponent < 0F ? 0F : float.PositiveInfinity;
            result = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256.Create(float.PositiveInfinity)), Vector256.Create(infinityResult), result);
        }

        // These selections also keep an exceptional neighbor from changing an ordinary lane's calculation.
        result = Vector256.ConditionalSelect(Vector256.Equals(value, Vector256<float>.One), value, result);
        return Vector256.ConditionalSelect(Vector256.Equals(value, value), result, value);
    }

    /// <summary>
    /// Average packed unsigned 8-bit integers in <paramref name="left"/> and <paramref name="right"/>, rounding up, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to average.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to average.
    /// </param>
    /// <returns>
    /// A vector containing (<paramref name="left"/> + <paramref name="right"/> + 1) &gt;&gt; 1 in each of its 32 lanes.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> Average(Vector256<byte> left, Vector256<byte> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Average(left, right);
        }

        // (a | b) - ((a ^ b) >> 1) equals (a + b + 1) >> 1 without the carry into a wider lane:
        // a + b = 2 * (a & b) + (a ^ b), and the shared and differing bits round up together.
        return (left | right) - ((left ^ right) >>> 1);
    }

    /// <summary>
    /// Average packed unsigned 16-bit integers in <paramref name="left"/> and <paramref name="right"/>, rounding up, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 16-bit integers to average.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 16-bit integers to average.
    /// </param>
    /// <returns>
    /// A vector containing (<paramref name="left"/> + <paramref name="right"/> + 1) &gt;&gt; 1 in each of its 16 lanes.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<ushort> Average(Vector256<ushort> left, Vector256<ushort> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Average(left, right);
        }

        // (a | b) - ((a ^ b) >> 1) equals (a + b + 1) >> 1 without the carry into a wider lane:
        // a + b = 2 * (a & b) + (a ^ b), and the shared and differing bits round up together.
        return (left | right) - ((left ^ right) >>> 1);
    }

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

        // Each half has its own byte indices. Like vpshufb, keep the low four index bits and the high zeroing bit;
        // bits 4-6 must not select outside the 128-bit lane on platforms with different native shuffle semantics.
        // The remaining indices are 0..15 or have the high zeroing bit set, so native byte shuffles preserve this contract.
        Vector128<byte> indexMask = Vector128.Create((byte)0x8F);
        Vector128<byte> lower = Vector128.ShuffleNative(vector.GetLower(), indices.GetLower() & indexMask);
        Vector128<byte> upper = Vector128.ShuffleNative(vector.GetUpper(), indices.GetUpper() & indexMask);
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
    /// Adds the absolute differences of packed unsigned 8-bit integers in <paramref name="left"/> and
    /// <paramref name="right"/> into <paramref name="accumulator"/>.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to compare.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to compare.
    /// </param>
    /// <param name="accumulator">
    /// The running total that the differences are added to.
    /// </param>
    /// <returns>
    /// A vector whose lanes together hold <paramref name="accumulator"/> plus the thirty-two absolute differences
    /// </returns>
    /// <remarks>
    /// The spread of the sums across the lanes is not defined, because each platform keeps the grouping
    /// that its own instruction produces. Only the total across all lanes is defined, so the caller must
    /// reduce the result with a horizontal sum and must not read one lane on its own. A lane holds a
    /// 32-bit total, so it cannot overflow until more than sixteen million samples are added to it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<uint> SumAbsoluteDifferences(Vector256<byte> left, Vector256<byte> right, Vector256<uint> accumulator)
    {
        if (Avx2.IsSupported)
        {
            return accumulator + Avx2.SumAbsoluteDifferences(left, right).AsUInt32();
        }

        return Vector256.Create(
            Vector128_.SumAbsoluteDifferences(left.GetLower(), right.GetLower(), accumulator.GetLower()),
            Vector128_.SumAbsoluteDifferences(left.GetUpper(), right.GetUpper(), accumulator.GetUpper()));
    }

    /// <summary>
    /// Horizontally adds adjacent pairs of single-precision values in <paramref name="left"/> and
    /// <paramref name="right"/> within each 128-bit lane.
    /// </summary>
    /// <param name="left">The vector whose pair sums fill the two lower values of each 128-bit lane.</param>
    /// <param name="right">The vector whose pair sums fill the two upper values of each 128-bit lane.</param>
    /// <returns>
    /// The vector (l0 + l1, l2 + l3, r0 + r1, r2 + r3, l4 + l5, l6 + l7, r4 + r5, r6 + r7).
    /// </returns>
    /// <remarks>
    /// The pairs never cross a 128-bit lane, which is what the x86 instruction does. The portable form applies the
    /// 128-bit operation to each half, so it gives the same bits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> HorizontalAdd(Vector256<float> left, Vector256<float> right)
    {
        if (Avx.IsSupported)
        {
            return Avx.HorizontalAdd(left, right);
        }

        return Vector256.Create(
            Vector128_.HorizontalAdd(left.GetLower(), right.GetLower()),
            Vector128_.HorizontalAdd(left.GetUpper(), right.GetUpper()));
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
    /// Multiply packed unsigned 8-bit integers in <paramref name="left"/> by packed signed 8-bit integers in
    /// <paramref name="right"/>, producing intermediate signed 16-bit integers. Horizontally add adjacent pairs of
    /// intermediate integers and pack the saturated results.
    /// </summary>
    /// <param name="left">
    /// The vector containing packed unsigned 8-bit integers to multiply and add.
    /// </param>
    /// <param name="right">
    /// The vector containing packed signed 8-bit integers to multiply and add.
    /// </param>
    /// <returns>
    /// A vector containing the saturated results of multiplying and adding adjacent pairs of packed 8-bit integers
    /// </returns>
    /// <remarks>
    /// The x86 instruction pairs within each 128-bit half, so composing the two halves of the
    /// narrower form gives the same lane order on every path.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> MultiplyAddAdjacent(Vector256<byte> left, Vector256<sbyte> right)
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
    /// Shifts each 128-bit lane right by a number of bytes, shifting in zeros.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="numBytes">The number of bytes to shift by.</param>
    /// <returns>The <see cref="Vector256{Byte}"/>.</returns>
    /// <remarks>
    /// The shift stays inside each 128-bit lane, which is what the x86 instruction does. Composing
    /// the two halves of the narrower shim reaches the same result on every other path.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> ShiftRightBytesInLane(Vector256<byte> value, [ConstantExpected(Max = (byte)15)] byte numBytes)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ShiftRightLogical128BitLane(value, numBytes);
        }

        return Vector256.Create(
            Vector128_.ShiftRightBytesInVector(value.GetLower(), numBytes),
            Vector128_.ShiftRightBytesInVector(value.GetUpper(), numBytes));
    }

    /// <summary>
    /// Shifts each 128-bit lane left by a number of bytes, shifting in zeros.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="numBytes">The number of bytes to shift by.</param>
    /// <returns>The <see cref="Vector256{Byte}"/>.</returns>
    /// <remarks>
    /// The shift stays inside each 128-bit lane, which is what the x86 instruction does. Composing
    /// the two halves of the narrower shim reaches the same result on every other path.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> ShiftLeftBytesInLane(Vector256<byte> value, [ConstantExpected(Max = (byte)15)] byte numBytes)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ShiftLeftLogical128BitLane(value, numBytes);
        }

        return Vector256.Create(
            Vector128_.ShiftLeftBytesInVector(value.GetLower(), numBytes),
            Vector128_.ShiftLeftBytesInVector(value.GetUpper(), numBytes));
    }

    /// <summary>
    /// Interleaves the upper signed 32-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int32}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> UnpackHigh(Vector256<int> left, Vector256<int> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackHigh(left, right);
        }

        return Vector256.Create(
            Vector128_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector128_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the lower signed 64-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int64}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<long> UnpackLow(Vector256<long> left, Vector256<long> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackLow(left, right);
        }

        return Vector256.Create(
            Vector128_.UnpackLow(left.GetLower(), right.GetLower()),
            Vector128_.UnpackLow(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the upper signed 64-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int64}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<long> UnpackHigh(Vector256<long> left, Vector256<long> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackHigh(left, right);
        }

        return Vector256.Create(
            Vector128_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector128_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the lower signed 16-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> UnpackLow(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackLow(left, right);
        }

        return Vector256.Create(
            Vector128_.UnpackLow(left.GetLower(), right.GetLower()),
            Vector128_.UnpackLow(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Interleaves the upper signed 16-bit integers of each 128-bit lane.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> UnpackHigh(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.UnpackHigh(left, right);
        }

        return Vector256.Create(
            Vector128_.UnpackHigh(left.GetLower(), right.GetLower()),
            Vector128_.UnpackHigh(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Widens eight unsigned bytes to signed 32-bit integers.
    /// </summary>
    /// <param name="value">The eight byte lanes.</param>
    /// <returns>The same values in eight integer lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Widen(Vector64<byte> value)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ConvertToVector256Int32(value.ToVector128());
        }

        // Widen bytes before words so values above 127 remain positive on the portable path.
        return Widen(Vector128.WidenLower(value.ToVector128()).AsInt16());
    }

    /// <summary>
    /// Widens eight signed bytes to signed 32-bit integers.
    /// </summary>
    /// <param name="value">The eight signed byte lanes.</param>
    /// <returns>The sign-extended values in eight integer lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Widen(Vector64<sbyte> value)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ConvertToVector256Int32(value.ToVector128());
        }

        return Widen(Vector128.WidenLower(value.ToVector128()));
    }

    /// <summary>
    /// Widens sixteen unsigned 8-bit integers to signed 16-bit integers.
    /// </summary>
    /// <param name="value">The vector to widen.</param>
    /// <returns>The <see cref="Vector256{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> Widen(Vector128<byte> value)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.ConvertToVector256Int16(value);
        }

        return Vector256.WidenLower(Vector256.Create(value, Vector128<byte>.Zero)).AsInt16();
    }

    /// <summary>
    /// Multiplies packed signed 16-bit integers, keeping the high 17 bits, rounds, and packs the
    /// high 16 bits of each result.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector256{Int16}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> MultiplyHighRoundScale(Vector256<short> left, Vector256<short> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.MultiplyHighRoundScale(left, right);
        }

        return Vector256.Create(
            Vector128_.MultiplyHighRoundScale(left.GetLower(), right.GetLower()),
            Vector128_.MultiplyHighRoundScale(left.GetUpper(), right.GetUpper()));
    }

    /// <summary>
    /// Permutes the four 64-bit elements of one vector.
    /// </summary>
    /// <param name="value">The vector to permute.</param>
    /// <param name="control">Two bits per destination element, selecting its source element.</param>
    /// <returns>The <see cref="Vector256{Int64}"/>.</returns>
    /// <remarks>
    /// This crosses the two 128-bit lanes, which the portable shuffle also does when its indices
    /// say so, so the control simply expands into an index vector.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<long> Permute4x64(Vector256<long> value, [ConstantExpected] byte control)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Permute4x64(value, control);
        }

        // Each two-bit field selects one of four elements. Native indices are therefore always in range.
        return Vector256.ShuffleNative(
            value,
            Vector256.Create((long)(control & 3), (control >> 2) & 3, (control >> 4) & 3, (control >> 6) & 3));
    }

    /// <summary>
    /// Reads 8 unaligned 32-bit words at byte offsets while the caller keeps the table pinned.
    /// </summary>
    /// <param name="table">The pinned or unmanaged byte storage.</param>
    /// <param name="byteOffsets">The start of each complete four-byte read.</param>
    /// <returns>The gathered words in lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector256<int> Gather(byte* table, Vector256<int> byteOffsets)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.GatherVector256((int*)table, byteOffsets, 1);
        }

        return Vector256.Create(
            Vector128_.Gather(table, byteOffsets.GetLower()),
            Vector128_.Gather(table, byteOffsets.GetUpper()));
    }

    /// <summary>
    /// Reads 8 table values while the caller keeps the table's address stable.
    /// </summary>
    /// <param name="table">The pinned or unmanaged table address.</param>
    /// <param name="indices">Element indices within the table, one per lane.</param>
    /// <returns>The selected 32-bit values in lane order.</returns>
    /// <remarks>
    /// Managed storage must remain pinned until this call returns. This overload lets a caller
    /// reuse one pin across several gathers instead of pinning the same table for every lookup.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector256<int> Gather(int* table, Vector256<int> indices)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.GatherVector256(table, indices, sizeof(int));
        }

        // The caller's pin also covers the portable fallback's individual reads.
        return Gather(ref Unsafe.AsRef<int>(table), indices);
    }

    /// <summary>
    /// Reads eight 32-bit values from a table, one per lane, at the given element indices.
    /// </summary>
    /// <param name="table">The first element of the table.</param>
    /// <param name="indices">The element index of each lane.</param>
    /// <returns>The <see cref="Vector256{Int32}"/>.</returns>
    /// <remarks>
    /// AVX2 gathers all eight words. Other targets combine two reads through the 128-bit helper.
    /// The caller is responsible for keeping every index inside the table.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector256<int> Gather(ref int table, Vector256<int> indices)
    {
        if (Avx2.IsSupported)
        {
            fixed (int* pointer = &table)
            {
                return Avx2.GatherVector256(pointer, indices, sizeof(int));
            }
        }

        // Reuse the 128-bit helper's AdvSimd lane loads or scalar reads for each half, in the original index order.
        return Vector256.Create(
            Vector128_.Gather(ref table, indices.GetLower()),
            Vector128_.Gather(ref table, indices.GetUpper()));
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

    /// <summary>
    /// Multiply the signed 32-bit integers in the even lanes of <paramref name="left"/> and <paramref name="right"/>,
    /// and store the signed 64-bit products. The odd lanes are ignored.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 32-bit integers to multiply.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 32-bit integers to multiply.
    /// </param>
    /// <returns>
    /// A vector containing the 64-bit products of the even lanes.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<long> MultiplyWideningEven(Vector256<int> left, Vector256<int> right)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.Multiply(left, right);
        }

        return Vector256.Create(
            Vector128_.MultiplyWideningEven(left.GetLower(), right.GetLower()),
            Vector128_.MultiplyWideningEven(left.GetUpper(), right.GetUpper()));
    }
}
