// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    /// Average packed unsigned 8-bit integers in <paramref name="left"/> and <paramref name="right"/>, and store the results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed unsigned 8-bit integers to average.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed unsigned 8-bit integers to average.
    /// </param>
    /// <returns>
    /// A vector containing the average of the packed unsigned 8-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> Average(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.Average(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.FusedAddRoundedHalving(left, right);
        }

        // Account for potential 9th bit to ensure correct rounded result.
        return Vector128.Narrow(
            (Vector128.WidenLower(left) + Vector128.WidenLower(right) + Vector128<ushort>.One) >> 1,
            (Vector128.WidenUpper(left) + Vector128.WidenUpper(right) + Vector128<ushort>.One) >> 1);
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
    /// A vector containing (<paramref name="left"/> + <paramref name="right"/> + 1) &gt;&gt; 1 in each of its 8 lanes.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> Average(Vector128<ushort> left, Vector128<ushort> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.Average(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.FusedAddRoundedHalving(left, right);
        }

        // (a | b) - ((a ^ b) >> 1) equals (a + b + 1) >> 1 without the carry into a wider lane:
        // a + b = 2 * (a & b) + (a ^ b), and the shared and differing bits round up together.
        return (left | right) - ((left ^ right) >>> 1);
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

        return Vector128.ShuffleNative(vector, indices);
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
        // Don't use InverseMMShuffle here as we want to avoid the cast.
        Vector128<int> indices = Vector128.Create(
            control & 0x3,
            (control >> 2) & 0x3,
            (control >> 4) & 0x3,
            (control >> 6) & 0x3);

        return Vector128.ShuffleNative(vector, indices);
    }

    /// <summary>
    /// Shuffle 16-bit integers in the high 64 bits of <paramref name="value"/> using the control in <paramref name="control"/>.
    /// Store the results in the high 64 bits of the destination, with the low 64 bits being copied from <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The input vector containing packed 16-bit integers to shuffle.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>
    /// A vector containing the shuffled 16-bit integers in the high 64 bits, with the low 64 bits copied from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> ShuffleHigh(Vector128<short> value, [ConstantExpected] byte control)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ShuffleHigh(value, control);
        }

        // Don't use InverseMMShuffle here as we want to avoid the cast.
        Vector128<short> indices = Vector128.Create(
           0,
           1,
           2,
           3,
           (short)((control & 0x3) + 4),
           (short)(((control >> 2) & 0x3) + 4),
           (short)(((control >> 4) & 0x3) + 4),
           (short)(((control >> 6) & 0x3) + 4));

        return Vector128.ShuffleNative(value, indices);
    }

    /// <summary>
    /// Shuffle 16-bit integers in the low 64 bits of <paramref name="value"/> using the control in <paramref name="control"/>.
    /// Store the results in the low 64 bits of the destination, with the high 64 bits being copied from <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The input vector containing packed 16-bit integers to shuffle.</param>
    /// <param name="control">The shuffle control byte.</param>
    /// <returns>
    /// A vector containing the shuffled 16-bit integers in the low 64 bits, with the high 64 bits copied from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> ShuffleLow(Vector128<short> value, [ConstantExpected] byte control)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ShuffleLow(value, control);
        }

        // Don't use InverseMMShuffle here as we want to avoid the cast.
        Vector128<short> indices = Vector128.Create(
           (short)(control & 0x3),
           (short)((control >> 2) & 0x3),
           (short)((control >> 4) & 0x3),
           (short)((control >> 6) & 0x3),
           4,
           5,
           6,
           7);

        return Vector128.ShuffleNative(value, indices);
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
    /// Converts all values in <paramref name="vector"/> to signed 32-bit integers, rounding midpoint values away from zero.
    /// </summary>
    /// <param name="vector">The values to convert.</param>
    /// <returns>The converted integer values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> ConvertToInt32RoundAwayFromZero(Vector128<float> vector)
    {
        if (Sse2.IsSupported)
        {
            // The x86 conversion truncates, so adding one half with each lane's sign implements round-to-nearest with midpoint values away from zero.
            Vector128<float> x86Adjustment = Vector128.Create(.5F) | (vector & Vector128.Create(-0F));
            return Sse2.ConvertToVector128Int32WithTruncation(vector + x86Adjustment);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.ConvertToInt32RoundAwayFromZero(vector);
        }

        Vector128<float> sign = vector & Vector128.Create(-0F);
        Vector128<float> fallbackAdjustment = Vector128.Create(.5F) | sign;
        return Vector128.ConvertToInt32(vector + fallbackAdjustment);
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
        Vector128<ushort> lefClamped = Vector128.Clamp(left, min, max).AsUInt16();
        Vector128<ushort> rightClamped = Vector128.Clamp(right, min, max).AsUInt16();
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
        Vector128<uint> lefClamped = Vector128.Clamp(left, min, max).AsUInt32();
        Vector128<uint> rightClamped = Vector128.Clamp(right, min, max).AsUInt32();
        return Vector128.Narrow(lefClamped, rightClamped);
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
    /// A vector whose lanes together hold <paramref name="accumulator"/> plus the sixteen absolute differences
    /// </returns>
    /// <remarks>
    /// The spread of the sums across the lanes is not defined, because each platform keeps the grouping
    /// that its own instruction produces. Only the total across all lanes is defined, so the caller must
    /// reduce the result with a horizontal sum and must not read one lane on its own. A lane holds a
    /// 32-bit total, so it cannot overflow until more than sixteen million samples are added to it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> SumAbsoluteDifferences(Vector128<byte> left, Vector128<byte> right, Vector128<uint> accumulator)
    {
        if (Sse2.IsSupported)
        {
            // The instruction puts the two eight-byte totals in 16-bit lanes 0 and 4, which are 32-bit
            // lanes 0 and 2, and leaves the rest zero.
            return accumulator + Sse2.SumAbsoluteDifferences(left, right).AsUInt32();
        }

        if (AdvSimd.IsSupported)
        {
            // Two widening pairwise adds fold the sixteen 8-bit differences into four 32-bit lanes.
            Vector128<ushort> pairs = AdvSimd.AddPairwiseWidening(AdvSimd.AbsoluteDifference(left, right));
            return accumulator + AdvSimd.AddPairwiseWidening(pairs);
        }

        // Unsigned lanes make the absolute difference the larger value minus the smaller one, so the
        // subtraction needs no widening and cannot wrap.
        Vector128<byte> difference = Vector128.Max(left, right) - Vector128.Min(left, right);
        (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(difference);
        (Vector128<uint> first, Vector128<uint> second) = Vector128.Widen(lower);
        (Vector128<uint> third, Vector128<uint> fourth) = Vector128.Widen(upper);
        return accumulator + first + second + third + fourth;
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
    public static Vector128<int> MultiplyAddAdjacent(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyAddAdjacent(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector128<int> prodLo = AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower());
            Vector128<int> prodHi = AdvSimd.MultiplyWideningUpper(left, right);

            if (AdvSimd.Arm64.IsSupported)
            {
                return AdvSimd.Arm64.AddPairwise(prodLo, prodHi);
            }

            Vector64<int> v0 = AdvSimd.AddPairwise(prodLo.GetLower(), prodLo.GetUpper());
            Vector64<int> v1 = AdvSimd.AddPairwise(prodHi.GetLower(), prodHi.GetUpper());
            return Vector128.Create(v0, v1);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> MultiplyAddAdjacent(Vector128<byte> left, Vector128<sbyte> right)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.MultiplyAddAdjacent(left, right);
        }

        // One product cannot leave a signed 16-bit lane, because the largest magnitude is 255 * -128,
        // so the widened multiply is exact. A pair sum can leave it, so the pairs are added in 32-bit
        // lanes and clamped before they narrow, which is the saturation the x86 instruction applies.
        (Vector128<ushort> leftLower, Vector128<ushort> leftUpper) = Vector128.Widen(left);
        (Vector128<short> rightLower, Vector128<short> rightUpper) = Vector128.Widen(right);
        (Vector128<int> lowerFirst, Vector128<int> lowerSecond) = Vector128.Widen(leftLower.AsInt16() * rightLower);
        (Vector128<int> upperFirst, Vector128<int> upperSecond) = Vector128.Widen(leftUpper.AsInt16() * rightUpper);

        Vector128<int> min = Vector128.Create((int)short.MinValue);
        Vector128<int> max = Vector128.Create((int)short.MaxValue);
        return Vector128.Narrow(
            Vector128.Clamp(HorizontalAdd(lowerFirst, lowerSecond), min, max),
            Vector128.Clamp(HorizontalAdd(upperFirst, upperSecond), min, max));
    }

    /// <summary>
    /// Horizontally add adjacent pairs of 32-bit integers in <paramref name="left"/> and <paramref name="right"/>, and
    /// pack the signed 32-bit results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 32-bit integers to add.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 32-bit integers to add.
    /// </param>
    /// <returns>
    /// A vector containing the results of horizontally adding adjacent pairs of packed signed 32-bit integers
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> HorizontalAdd(Vector128<int> left, Vector128<int> right)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.HorizontalAdd(left, right);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.AddPairwise(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector64<int> leftPairs = AdvSimd.AddPairwise(left.GetLower(), left.GetUpper());
            Vector64<int> rightPairs = AdvSimd.AddPairwise(right.GetLower(), right.GetUpper());
            return Vector128.Create(leftPairs, rightPairs);
        }

        {
            // Gather the even and the odd lanes of each source into the half of the result that
            // source owns, then add. An out-of-range index zeroes the lanes the other source fills.
            Vector128<int> v0 = Vector128.Shuffle(left, Vector128.Create(0, 2, 8, 8));
            Vector128<int> v1 = Vector128.Shuffle(left, Vector128.Create(1, 3, 8, 8));
            Vector128<int> v2 = Vector128.Shuffle(right, Vector128.Create(8, 8, 0, 2));
            Vector128<int> v3 = Vector128.Shuffle(right, Vector128.Create(8, 8, 1, 3));

            return v0 + v1 + v2 + v3;
        }
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
            Vector64<short> lower = AdvSimd.ExtractNarrowingSaturateLower(left);
            return AdvSimd.ExtractNarrowingSaturateUpper(lower, right);
        }

        Vector128<int> min = Vector128.Create((int)short.MinValue);
        Vector128<int> max = Vector128.Create((int)short.MaxValue);
        return Vector128.Narrow(Vector128.Clamp(left, min, max), Vector128.Clamp(right, min, max));
    }

    /// <summary>
    /// Packs signed 16-bit integers to signed 8-bit integers and saturates.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{SByte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> PackSignedSaturate(Vector128<short> left, Vector128<short> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.PackSignedSaturate(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector64<sbyte> lower = AdvSimd.ExtractNarrowingSaturateLower(left);
            return AdvSimd.ExtractNarrowingSaturateUpper(lower, right);
        }

        Vector128<short> min = Vector128.Create((short)sbyte.MinValue);
        Vector128<short> max = Vector128.Create((short)sbyte.MaxValue);
        return Vector128.Narrow(Vector128.Clamp(left, min, max), Vector128.Clamp(right, min, max));
    }

    /// <summary>
    /// Multiplies packed signed 16-bit integers, keeping the high 17 bits, rounds, and packs the
    /// high 16 bits of each result.
    /// </summary>
    /// <param name="left">The left hand source vector.</param>
    /// <param name="right">The right hand source vector.</param>
    /// <returns>The <see cref="Vector128{Int16}"/>.</returns>
    /// <remarks>
    /// The rounding term is added at bit 14 and the sum shifts right by 15, which is what the x86
    /// instruction does. The product of two signed 16-bit values fits a 32-bit lane, so the widened
    /// form is exact and needs no saturation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> MultiplyHighRoundScale(Vector128<short> left, Vector128<short> right)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.MultiplyHighRoundScale(left, right);
        }

        (Vector128<int> leftLower, Vector128<int> leftUpper) = Vector128.Widen(left);
        (Vector128<int> rightLower, Vector128<int> rightUpper) = Vector128.Widen(right);
        Vector128<int> rounding = Vector128.Create(1 << 14);
        Vector128<int> lower = ((leftLower * rightLower) + rounding) >> 15;
        Vector128<int> upper = ((leftUpper * rightUpper) + rounding) >> 15;
        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Horizontally add adjacent pairs of 16-bit integers in <paramref name="left"/> and <paramref name="right"/>, and
    /// pack the signed 16-bit results.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed signed 16-bit integers to add.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed signed 16-bit integers to add.
    /// </param>
    /// <returns>
    /// A vector containing the results of horizontally adding adjacent pairs of packed signed 16-bit integers
    /// </returns>
    public static Vector128<short> HorizontalAdd(Vector128<short> left, Vector128<short> right)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.HorizontalAdd(left, right);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.AddPairwise(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector128<int> v0 = AdvSimd.AddPairwiseWidening(left);
            Vector128<int> v1 = AdvSimd.AddPairwiseWidening(right);

            return Vector128.Narrow(v0, v1);
        }

        {
            // Extract the low and high parts of the products shuffling them to form a result we can add together.
            // Use out-of-bounds to zero out the unused lanes.
            Vector128<short> even = Vector128.Create(0, 2, 4, 6, 8, 8, 8, 8);
            Vector128<short> odd = Vector128.Create(1, 3, 5, 7, 8, 8, 8, 8);
            Vector128<short> v0 = Vector128.Shuffle(right, even);
            Vector128<short> v1 = Vector128.Shuffle(right, odd);
            Vector128<short> v2 = Vector128.Shuffle(left, even);
            Vector128<short> v3 = Vector128.Shuffle(left, odd);

            return v0 + v1 + v2 + v3;
        }
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

        if (AdvSimd.IsSupported)
        {
            Vector128<int> prodLo = AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower());
            Vector128<int> prodHi = AdvSimd.MultiplyWideningUpper(left, right);

            prodLo >>= 16;
            prodHi >>= 16;

            return Vector128.Narrow(prodLo, prodHi);
        }

        {
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
    }

    /// <summary>
    /// Multiply the packed 16-bit unsigned integers in <paramref name="left"/> and <paramref name="right"/>, producing
    /// intermediate unsigned 32-bit integers, and store the high 16 bits of the intermediate integers in the result.
    /// </summary>
    /// <param name="left">
    /// The first vector containing packed 16-bit unsigned integers to multiply.
    /// </param>
    /// <param name="right">
    /// The second vector containing packed 16-bit unsigned integers to multiply.
    /// </param>
    /// <returns>
    /// A vector containing the high 16 bits of the products of the packed 16-bit unsigned integers
    /// from <paramref name="left"/> and <paramref name="right"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> MultiplyHigh(Vector128<ushort> left, Vector128<ushort> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyHigh(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector128<uint> prodLo = AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower());
            Vector128<uint> prodHi = AdvSimd.MultiplyWideningUpper(left, right);

            prodLo >>= 16;
            prodHi >>= 16;

            return Vector128.Narrow(prodLo, prodHi);
        }

        {
            // Widen each half of the short vectors into two uint vectors
            (Vector128<uint> leftLo, Vector128<uint> leftHi) = Vector128.Widen(left);
            (Vector128<uint> rightLo, Vector128<uint> rightHi) = Vector128.Widen(right);

            // Elementwise multiply: each int lane now holds the full 32-bit product
            Vector128<uint> prodLo = leftLo * rightLo;
            Vector128<uint> prodHi = leftHi * rightHi;

            // Arithmetic shift right by 16 bits to extract the high word
            prodLo >>= 16;
            prodHi >>= 16;

            // Narrow the two int vectors back into one short vector
            return Vector128.Narrow(prodLo, prodHi);
        }
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
    /// Interleaves the high 64-bit floating-point components of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The interleaved high components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> UnpackHigh(Vector128<double> left, Vector128<double> right)
        => UnpackHigh(left.AsInt64(), right.AsInt64()).AsDouble();

    /// <summary>
    /// Interleaves the low 64-bit floating-point components of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The interleaved low components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> UnpackLow(Vector128<double> left, Vector128<double> right)
        => UnpackLow(left.AsInt64(), right.AsInt64()).AsDouble();

    /// <summary>
    /// Interleaves the high 32-bit floating-point components of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The interleaved high components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnpackHigh(Vector128<float> left, Vector128<float> right)
        => UnpackHigh(left.AsInt32(), right.AsInt32()).AsSingle();

    /// <summary>
    /// Interleaves the low 32-bit floating-point components of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The interleaved low components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnpackLow(Vector128<float> left, Vector128<float> right)
        => UnpackLow(left.AsInt32(), right.AsInt32()).AsSingle();

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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 2, 1, 3));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 2, 1, 3));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 4, 1, 5, 2, 6, 3, 7));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 4, 1, 5, 2, 6, 3, 7));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
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
        return Vector128.ShuffleNative(unpacked, Vector128.Create(0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
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
    public static Vector128<long> MultiplyWideningEven(Vector128<int> left, Vector128<int> right)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.Multiply(left, right);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.MultiplyWideningLower(
                AdvSimd.Arm64.UnzipEven(left, left).GetLower(),
                AdvSimd.Arm64.UnzipEven(right, right).GetLower());
        }

        // Sign-extend the even lanes in place, then multiply the 64-bit lanes.
        return ((left.AsInt64() << 32) >> 32) * ((right.AsInt64() << 32) >> 32);
    }

    /// <summary>
    /// Reads four 32-bit values from a table, one per lane, at the given element indices.
    /// </summary>
    /// <param name="table">The first element of the table.</param>
    /// <param name="indices">The element index of each lane.</param>
    /// <returns>The <see cref="Vector128{Int32}"/>.</returns>
    /// <remarks>
    /// A gather has no portable form, so every path other than AVX2 reads the four elements one at a time. The
    /// indices are stored once and the result is built once. The caller is responsible for keeping every index
    /// inside the table.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector128<int> Gather(ref int table, Vector128<int> indices)
    {
        if (Avx2.IsSupported)
        {
            fixed (int* pointer = &table)
            {
                return Avx2.GatherVector128(pointer, indices, sizeof(int));
            }
        }

        InlineArray4<int> lanes = default;
        ref int first = ref Unsafe.As<InlineArray4<int>, int>(ref lanes);
        indices.StoreUnsafe(ref first);
        return Vector128.Create(
            Unsafe.Add(ref table, Unsafe.Add(ref first, 0)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 1)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 2)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 3)));
    }
}
