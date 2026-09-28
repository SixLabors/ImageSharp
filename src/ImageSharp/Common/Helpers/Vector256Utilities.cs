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

        // The .NET 10 fallback treats indices as full-width when AVX2 is unavailable. Reusing
        // the low mask for each half preserves the lane-local vpshufb contract on AVX-only CPUs.
        Vector128<byte> indicesLo = indices.GetLower();
        Vector128<byte> lower = Vector128.ShuffleNative(vector.GetLower(), indicesLo);
        Vector128<byte> upper = Vector128.ShuffleNative(vector.GetUpper(), indicesLo);
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

        return Vector256.Shuffle(
            value,
            Vector256.Create((long)(control & 3), (control >> 2) & 3, (control >> 4) & 3, (control >> 6) & 3));
    }

    /// <summary>
    /// Reads eight 32-bit values from a table, one per lane, at the given element indices.
    /// </summary>
    /// <param name="table">The first element of the table.</param>
    /// <param name="indices">The element index of each lane.</param>
    /// <returns>The <see cref="Vector256{Int32}"/>.</returns>
    /// <remarks>
    /// A gather has no portable form, so every path other than AVX2 reads the eight elements one at
    /// a time. The caller is responsible for keeping every index inside the table.
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

        // A variable lane index defeats both vector accessors: each GetElement spills the index
        // vector to the stack and reloads one value, and each WithElement spills and reloads the
        // result. The indices are therefore stored once and the result is built once, which costs
        // one store, eight loads and one construct.
        //
        // The scratch is an inline array rather than a stackalloc. This method is small enough to
        // inline into a caller loop, and a stackalloc inside a loop body is not released per
        // iteration, so it would grow the frame of the caller for as long as that method runs.
        InlineArray8<int> lanes = default;
        ref int first = ref Unsafe.As<InlineArray8<int>, int>(ref lanes);
        indices.StoreUnsafe(ref first);
        return Vector256.Create(
            Unsafe.Add(ref table, Unsafe.Add(ref first, 0)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 1)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 2)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 3)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 4)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 5)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 6)),
            Unsafe.Add(ref table, Unsafe.Add(ref first, 7)));
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
}
