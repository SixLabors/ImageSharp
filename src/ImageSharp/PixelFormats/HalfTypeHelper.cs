// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Helper methods for packing and unpacking floating point values
/// </summary>
internal static class HalfTypeHelper
{
    // IEEE 754 binary16 has a largest finite magnitude of 65504. Scaled pixel vectors map that complete finite
    // interval to [0, 1], while native vectors continue to expose the stored floating-point value directly.
    internal const float FiniteMinimum = -65504F;
    internal const float FiniteMaximum = 65504F;
    internal const float FiniteRange = FiniteMaximum - FiniteMinimum;
    internal const float InverseFiniteRange = (float)(1D / FiniteRange);
    internal const float ScaledMidpoint = .5F;

    // These constants mirror the binary16 conversion used by System.Half. Keeping the vector conversion
    // bit-for-bit equivalent to the scalar runtime conversion makes SIMD a pure throughput optimization.
    private const uint HalfExponentMask = 0x7C00;
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
    /// Packs a <see cref="float"/> into an <see cref="ushort"/>
    /// </summary>
    /// <param name="value">The float to pack</param>
    /// <returns>The <see cref="ushort"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort Pack(float value) => BitConverter.HalfToUInt16Bits((Half)value);

    /// <summary>
    /// Unpacks a <see cref="ushort"/> into a <see cref="float"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Unpack(ushort value) => (float)BitConverter.UInt16BitsToHalf(value);

    /// <summary>
    /// Normalizes a finite binary16 value to the scaled pixel range.
    /// </summary>
    /// <param name="value">The native binary16 value represented as a <see cref="float"/>.</param>
    /// <returns>The normalized value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ToScaled(float value) => (value * InverseFiniteRange) + ScaledMidpoint;

    /// <summary>
    /// Normalizes finite binary16 values to the scaled pixel range.
    /// </summary>
    /// <param name="value">The native binary16 values.</param>
    /// <returns>The normalized values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 ToScaled(Vector2 value) => (value * InverseFiniteRange) + new Vector2(ScaledMidpoint);

    /// <summary>
    /// Normalizes finite binary16 values to the scaled pixel range.
    /// </summary>
    /// <param name="value">The native binary16 values.</param>
    /// <returns>The normalized values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 ToScaled(Vector4 value) => (value * InverseFiniteRange) + new Vector4(ScaledMidpoint);

    /// <summary>
    /// Expands a normalized value to the finite binary16 range.
    /// </summary>
    /// <param name="value">The normalized value.</param>
    /// <returns>The native binary16 value represented as a <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float FromScaled(float value) => (value * FiniteRange) + FiniteMinimum;

    /// <summary>
    /// Expands normalized values to the finite binary16 range.
    /// </summary>
    /// <param name="value">The normalized values.</param>
    /// <returns>The native binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 FromScaled(Vector2 value) => (value * FiniteRange) + new Vector2(FiniteMinimum);

    /// <summary>
    /// Expands normalized values to the finite binary16 range.
    /// </summary>
    /// <param name="value">The normalized values.</param>
    /// <returns>The native binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 FromScaled(Vector4 value) => (value * FiniteRange) + new Vector4(FiniteMinimum);

    /// <summary>
    /// Unpacks eight binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector128<float> Lower, Vector128<float> Upper) Unpack(Vector128<ushort> value)
    {
        (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks sixteen binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector256<float> Lower, Vector256<float> Upper) Unpack(Vector256<ushort> value)
    {
        (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks thirty-two binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector512<float> Lower, Vector512<float> Upper) Unpack(Vector512<ushort> value)
    {
        (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Packs eight single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<ushort> Pack(Vector128<float> lower, Vector128<float> upper)
        => Vector128.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs sixteen single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<ushort> Pack(Vector256<float> lower, Vector256<float> upper)
        => Vector256.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs thirty-two single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<ushort> Pack(Vector512<float> lower, Vector512<float> upper)
        => Vector512.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> RoundToHalf(Vector128<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> RoundToHalf(Vector256<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> RoundToHalf(Vector512<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
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

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector128<uint> bits = Vector128.ShiftLeft(value, 13) & Vector128.Create(HalfToSingleBitsMask);
        exponentOffset = Vector128.ConditionalSelect(infinityOrNaNMask, Vector128.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector128<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
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

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector256<uint> bits = Vector256.ShiftLeft(value, 13) & Vector256.Create(HalfToSingleBitsMask);
        exponentOffset = Vector256.ConditionalSelect(infinityOrNaNMask, Vector256.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector256<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
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

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector512<uint> bits = Vector512.ShiftLeft(value, 13) & Vector512.Create(HalfToSingleBitsMask);
        exponentOffset = Vector512.ConditionalSelect(infinityOrNaNMask, Vector512.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector512<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ConvertSingleToHalfBits(Vector128<float> value)
    {
        Vector128<uint> bits = value.AsUInt32();
        Vector128<uint> sign = Vector128.ShiftRightLogical(bits & Vector128.Create(SingleSignMask), 16);
        Vector128<uint> realMask = Vector128.Equals(value, value).AsUInt32();
        value = Vector128.Abs(value);
        value = Vector128.Min(Vector128.Create(MaxHalfValueBelowInfinity), value);
        Vector128<uint> exponentOffset = Vector128.Max(value, Vector128.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector128.Create(SingleBiasedExponentMask);
        exponentOffset += Vector128.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector128.Create(SingleExponent126);
        Vector128<uint> newExponent = Vector128.ShiftRightLogical(bits, 13);
        Vector128<uint> maskedHalfExponentForNaN = ~realMask & Vector128.Create(HalfExponentMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ConvertSingleToHalfBits(Vector256<float> value)
    {
        Vector256<uint> bits = value.AsUInt32();
        Vector256<uint> sign = Vector256.ShiftRightLogical(bits & Vector256.Create(SingleSignMask), 16);
        Vector256<uint> realMask = Vector256.Equals(value, value).AsUInt32();
        value = Vector256.Abs(value);
        value = Vector256.Min(Vector256.Create(MaxHalfValueBelowInfinity), value);
        Vector256<uint> exponentOffset = Vector256.Max(value, Vector256.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector256.Create(SingleBiasedExponentMask);
        exponentOffset += Vector256.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector256.Create(SingleExponent126);
        Vector256<uint> newExponent = Vector256.ShiftRightLogical(bits, 13);
        Vector256<uint> maskedHalfExponentForNaN = ~realMask & Vector256.Create(HalfExponentMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ConvertSingleToHalfBits(Vector512<float> value)
    {
        Vector512<uint> bits = value.AsUInt32();
        Vector512<uint> sign = Vector512.ShiftRightLogical(bits & Vector512.Create(SingleSignMask), 16);
        Vector512<uint> realMask = Vector512.Equals(value, value).AsUInt32();
        value = Vector512.Abs(value);
        value = Vector512.Min(Vector512.Create(MaxHalfValueBelowInfinity), value);
        Vector512<uint> exponentOffset = Vector512.Max(value, Vector512.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector512.Create(SingleBiasedExponentMask);
        exponentOffset += Vector512.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector512.Create(SingleExponent126);
        Vector512<uint> newExponent = Vector512.ShiftRightLogical(bits, 13);
        Vector512<uint> maskedHalfExponentForNaN = ~realMask & Vector512.Create(HalfExponentMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }
}
