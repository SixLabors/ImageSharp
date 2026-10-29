// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Converts half-precision storage to and from single-precision components.
/// </summary>
/// <remarks>
/// These methods apply the rounding and special-value rules of <see cref="Half"/>.
/// Pixel operations apply component bounds and alpha conversion before or after these storage primitives.
/// </remarks>
internal static class HalfTypeHelper
{
    // These constants mirror the half-precision conversion used by System.Half. Keeping the vector conversion
    // bit-for-bit equivalent to the scalar runtime conversion makes SIMD a pure throughput optimization.
    private const uint HalfExponentMask = 0x7C00;
    private const uint HalfQuietNaNMask = 0x0200;
    private const uint HalfSignMask = 0x8000;
    private const uint HalfToSingleBitsMask = 0x0FFF_E000;
    private const uint SingleExponentLowerBound = 0x3880_0000;
    private const uint SingleExponentOffset = 0x3800_0000;
    private const uint SingleExponent126 = 0x3F00_0000;
    private const uint SingleBiasedExponentMask = 0x7F80_0000;
    private const uint SingleExponent13 = 0x0680_0000;
    private const uint SingleSignMask = 0x8000_0000;
    private const float MaxHalfValueBelowInfinity = 65520F;

    /// <summary>
    /// Rounds a single-precision value to <see cref="Half"/> and returns its stored bits.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The half-precision bit pattern, including the sign and any infinity or NaN encoding.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Pack(float value) => BitConverter.HalfToUInt16Bits((Half)value);

    /// <summary>
    /// Expands a half-precision bit pattern to its single-precision value.
    /// </summary>
    /// <param name="value">The half-precision bit pattern.</param>
    /// <returns>The expanded value, including signed zero, infinity or NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Unpack(ushort value) => (float)BitConverter.UInt16BitsToHalf(value);

    /// <summary>
    /// Unpacks eight half-precision values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed half-precision values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vector128<float> Lower, Vector128<float> Upper) Unpack(Vector128<ushort> value)
    {
        (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks sixteen half-precision values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed half-precision values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vector256<float> Lower, Vector256<float> Upper) Unpack(Vector256<ushort> value)
    {
        (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks thirty-two half-precision values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed half-precision values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vector512<float> Lower, Vector512<float> Upper) Unpack(Vector512<ushort> value)
    {
        (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Packs eight single-precision values into half-precision storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed half-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> Pack(Vector128<float> lower, Vector128<float> upper)
        => Vector128.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs sixteen single-precision values into half-precision storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed half-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<ushort> Pack(Vector256<float> lower, Vector256<float> upper)
        => Vector256.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs thirty-two single-precision values into half-precision storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed half-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<ushort> Pack(Vector512<float> lower, Vector512<float> upper)
        => Vector512.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Expands half storage into single-precision components without color or alpha conversion.
    /// </summary>
    /// <param name="source">The source components.</param>
    /// <param name="destination">Storage for at least the source component count.</param>
    public static void Unpack(ReadOnlySpan<ushort> source, Span<float> destination)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Widening retains component order across each register. Full registers use
        // SIMD; the scalar tail handles any remaining components in that same order.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector512Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                (Vector512<float> lower, Vector512<float> upper) = Unpack(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector512.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector512<float>.Count));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector256Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                (Vector256<float> lower, Vector256<float> upper) = Unpack(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector256.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector256<float>.Count));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector128Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                (Vector128<float> lower, Vector128<float> upper) = Unpack(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector128.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector128<float>.Count));
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref destinationBase, (uint)i) = Unpack(Unsafe.Add(ref sourceBase, (uint)i));
        }
    }

    /// <summary>
    /// Rounds single-precision components into half storage without color or alpha conversion.
    /// </summary>
    /// <param name="source">The source components.</param>
    /// <param name="destination">Storage for at least the source component count.</param>
    public static void Pack(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<ushort> packedDestination = destination[..source.Length];
        int i = 0;

        // Each pack consumes two float registers and produces one ushort register.
        // Components retain their input order. This raw helper has no pixel layout or alpha rules.
        // Pack rounds each value to half precision. Smaller loops consume the remainder without reading beyond the source.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector512Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                Vector512<float> lower = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector512<float> upper = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count));
                Vector512.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector256Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                Vector256<float> lower = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector256<float> upper = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count));
                Vector256.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector128Count<ushort>();

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                Vector128<float> lower = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector128<float> upper = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count));
                Vector128.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref destinationBase, (uint)i) = Pack(Unsafe.Add(ref sourceBase, (uint)i));
        }
    }

    /// <summary>
    /// Rounds single-precision values through half-precision without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after half-precision quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> RoundToHalf(Vector128<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through half-precision without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after half-precision quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> RoundToHalf(Vector256<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through half-precision without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after half-precision quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> RoundToHalf(Vector512<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Converts zero-extended half-precision bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The half-precision bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ConvertHalfBitsToSingle(Vector128<uint> value)
    {
        Vector128<uint> sign = Vector128.ShiftLeft(value & Vector128.Create(HalfSignMask), 16);
        Vector128<uint> exponent = value & Vector128.Create(HalfExponentMask);
        Vector128<uint> subnormalMask = Vector128.Equals(exponent, Vector128<uint>.Zero);
        Vector128<uint> infinityOrNaNMask = Vector128.Equals(exponent, Vector128.Create(HalfExponentMask));
        Vector128<uint> maskedExponentLowerBound = subnormalMask & Vector128.Create(SingleExponentLowerBound);
        Vector128<uint> exponentOffset = Vector128.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Half precision has 10 fraction bits; single precision has 23. Shift by 13 to align those fields.
        // Normal values need the exponent-bias difference. All-ones exponents need twice that offset for infinity and NaN.
        // For subnormals, add a temporary normal exponent, then subtract its float value to restore the fraction magnitude.
        Vector128<uint> bits = Vector128.ShiftLeft(value, 13) & Vector128.Create(HalfToSingleBitsMask);
        exponentOffset = Vector128.ConditionalSelect(infinityOrNaNMask, Vector128.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;

        // The subtraction is zero for normal lanes. For subnormal lanes it removes the temporary exponent exactly.
        // Restore the saved sign after subtraction, including negative zero.
        Vector128<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended half-precision bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The half-precision bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ConvertHalfBitsToSingle(Vector256<uint> value)
    {
        Vector256<uint> sign = Vector256.ShiftLeft(value & Vector256.Create(HalfSignMask), 16);
        Vector256<uint> exponent = value & Vector256.Create(HalfExponentMask);
        Vector256<uint> subnormalMask = Vector256.Equals(exponent, Vector256<uint>.Zero);
        Vector256<uint> infinityOrNaNMask = Vector256.Equals(exponent, Vector256.Create(HalfExponentMask));
        Vector256<uint> maskedExponentLowerBound = subnormalMask & Vector256.Create(SingleExponentLowerBound);
        Vector256<uint> exponentOffset = Vector256.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Half precision has 10 fraction bits; single precision has 23. Shift by 13 to align those fields.
        // Normal values need the exponent-bias difference. All-ones exponents need twice that offset for infinity and NaN.
        // For subnormals, add a temporary normal exponent, then subtract its float value to restore the fraction magnitude.
        Vector256<uint> bits = Vector256.ShiftLeft(value, 13) & Vector256.Create(HalfToSingleBitsMask);
        exponentOffset = Vector256.ConditionalSelect(infinityOrNaNMask, Vector256.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;

        // The subtraction is zero for normal lanes. For subnormal lanes it removes the temporary exponent exactly.
        // Restore the saved sign after subtraction, including negative zero.
        Vector256<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended half-precision bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The half-precision bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ConvertHalfBitsToSingle(Vector512<uint> value)
    {
        Vector512<uint> sign = Vector512.ShiftLeft(value & Vector512.Create(HalfSignMask), 16);
        Vector512<uint> exponent = value & Vector512.Create(HalfExponentMask);
        Vector512<uint> subnormalMask = Vector512.Equals(exponent, Vector512<uint>.Zero);
        Vector512<uint> infinityOrNaNMask = Vector512.Equals(exponent, Vector512.Create(HalfExponentMask));
        Vector512<uint> maskedExponentLowerBound = subnormalMask & Vector512.Create(SingleExponentLowerBound);
        Vector512<uint> exponentOffset = Vector512.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Half precision has 10 fraction bits; single precision has 23. Shift by 13 to align those fields.
        // Normal values need the exponent-bias difference. All-ones exponents need twice that offset for infinity and NaN.
        // For subnormals, add a temporary normal exponent, then subtract its float value to restore the fraction magnitude.
        Vector512<uint> bits = Vector512.ShiftLeft(value, 13) & Vector512.Create(HalfToSingleBitsMask);
        exponentOffset = Vector512.ConditionalSelect(infinityOrNaNMask, Vector512.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;

        // The subtraction is zero for normal lanes. For subnormal lanes it removes the temporary exponent exactly.
        // Restore the saved sign after subtraction, including negative zero.
        Vector512<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts single-precision values to zero-extended half-precision bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The half-precision bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ConvertSingleToHalfBits(Vector128<float> value)
    {
        Vector128<uint> bits = value.AsUInt32();
        Vector128<uint> sign = Vector128.ShiftRightLogical(bits & Vector128.Create(SingleSignMask), 16);
        Vector128<uint> realMask = Vector128.Equals(value, value).AsUInt32();

        // Save the sign before absolute value. A self-comparison distinguishes NaN from all numeric values.
        value = Vector128.Abs(value);

        // 65520 is the midpoint that rounds to half infinity. Limit larger magnitudes to this same overflow result.
        value = Vector128.Min(Vector128.Create(MaxHalfValueBelowInfinity), value);
        Vector128<uint> exponentOffset = Vector128.Max(value, Vector128.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector128.Create(SingleBiasedExponentMask);
        exponentOffset += Vector128.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to half precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector128.Create(SingleExponent126);
        Vector128<uint> newExponent = Vector128.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector128<uint> maskedHalfExponentForNaN = ~realMask & Vector128.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended half-precision bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The half-precision bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ConvertSingleToHalfBits(Vector256<float> value)
    {
        Vector256<uint> bits = value.AsUInt32();
        Vector256<uint> sign = Vector256.ShiftRightLogical(bits & Vector256.Create(SingleSignMask), 16);
        Vector256<uint> realMask = Vector256.Equals(value, value).AsUInt32();

        // Save the sign before absolute value. A self-comparison distinguishes NaN from all numeric values.
        value = Vector256.Abs(value);

        // 65520 is the midpoint that rounds to half infinity. Limit larger magnitudes to this same overflow result.
        value = Vector256.Min(Vector256.Create(MaxHalfValueBelowInfinity), value);
        Vector256<uint> exponentOffset = Vector256.Max(value, Vector256.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector256.Create(SingleBiasedExponentMask);
        exponentOffset += Vector256.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to half precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector256.Create(SingleExponent126);
        Vector256<uint> newExponent = Vector256.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector256<uint> maskedHalfExponentForNaN = ~realMask & Vector256.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended half-precision bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The half-precision bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ConvertSingleToHalfBits(Vector512<float> value)
    {
        Vector512<uint> bits = value.AsUInt32();
        Vector512<uint> sign = Vector512.ShiftRightLogical(bits & Vector512.Create(SingleSignMask), 16);
        Vector512<uint> realMask = Vector512.Equals(value, value).AsUInt32();

        // Save the sign before absolute value. A self-comparison distinguishes NaN from all numeric values.
        value = Vector512.Abs(value);

        // 65520 is the midpoint that rounds to half infinity. Limit larger magnitudes to this same overflow result.
        value = Vector512.Min(Vector512.Create(MaxHalfValueBelowInfinity), value);
        Vector512<uint> exponentOffset = Vector512.Max(value, Vector512.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector512.Create(SingleBiasedExponentMask);
        exponentOffset += Vector512.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to half precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector512.Create(SingleExponent126);
        Vector512<uint> newExponent = Vector512.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector512<uint> maskedHalfExponentForNaN = ~realMask & Vector512.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }
}
