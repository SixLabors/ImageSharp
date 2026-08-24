// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Provides the integer arithmetic primitives used by AV1 syntax and reconstruction.
/// </summary>
internal static class Av1Math
{
    /// <summary>
    /// Gets the zero-based position of the most significant set bit.
    /// </summary>
    /// <param name="value">A nonzero unsigned value.</param>
    /// <returns>The most significant set-bit position.</returns>
    public static int MostSignificantBit(uint value)
    {
        int log = 0;
        int i;

        Guard.IsTrue(value != 0, nameof(value), "Must have at least one bit set.");

        for (i = 4; i >= 0; --i)
        {
            int shift = 1 << i;
            uint x = value >> shift;
            if (x != 0)
            {
                value = x;
                log += shift;
            }
        }

        return log;
    }

    /// <summary>
    /// Gets the integer base-two logarithm of a positive value.
    /// </summary>
    /// <param name="n">The value.</param>
    /// <returns>The zero-based position of the most significant set bit.</returns>
    public static int Log2(int n)
    {
        int result = 0;
        while ((n >>= 1) > 0)
        {
            result++;
        }

        return result;
    }

    /// <summary>
    /// Gets the integer base-two logarithm of an unsigned 32-bit value.
    /// </summary>
    /// <param name="x">The value.</param>
    /// <returns>The zero-based position of the most significant set bit.</returns>
    internal static uint Log2_32(uint x)
    {
        uint log = 0;
        int i;
        for (i = 4; i >= 0; --i)
        {
            uint shift = 1u << i;
            uint n = x >> (int)shift;
            if (n != 0)
            {
                x = n;
                log += shift;
            }
        }

        return log;
    }

    /// <summary>
    /// Gets the greatest integer less than or equal to the base-two logarithm of a nonzero value.
    /// </summary>
    /// <param name="value">The nonzero value.</param>
    /// <returns>The floor of the base-two logarithm.</returns>
    public static uint FloorLog2(uint value)
    {
        uint s = 0;
        while (value != 0U)
        {
            value >>= 1;
            s++;
        }

        return s - 1;
    }

    /// <summary>
    /// Gets the least integer greater than or equal to the base-two logarithm of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The ceiling of the base-two logarithm, or zero for values below two.</returns>
    public static uint CeilLog2(uint value)
    {
        if (value < 2)
        {
            return 0;
        }

        uint i = 1;
        uint p = 2;
        while (p < value)
        {
            i++;
            p <<= 1;
        }

        return i;
    }

    /// <summary>
    /// Clips an unsigned sample to the range represented by a bit depth.
    /// </summary>
    /// <param name="value">The sample value.</param>
    /// <param name="bitDepth">The number of sample bits.</param>
    /// <returns>The clipped sample.</returns>
    public static uint Clip1(uint value, int bitDepth) =>
        Clip3(0, (1U << bitDepth) - 1, value);

    /// <summary>
    /// Clips an unsigned value to an inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="value">The value to clip.</param>
    /// <returns>The clipped value.</returns>
    public static uint Clip3(uint min, uint max, uint value) => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// Clips a signed value to an inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="value">The value to clip.</param>
    /// <returns>The clipped value.</returns>
    public static int Clip3(int min, int max, int value) => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// Divides an unsigned value by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    public static uint Round2(uint value, int n)
    {
        if (n == 0)
        {
            return value;
        }

        return (uint)((value + (1 << (n - 1))) >> n);
    }

    /// <summary>
    /// Divides the absolute magnitude of a signed value by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The signed value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The rounded nonnegative magnitude.</returns>
    public static int Round2(int value, int n)
    {
        if (value < 0)
        {
            value = -value;
        }

        return (int)Round2((uint)value, n);
    }

    /// <summary>
    /// Aligns a value upward to a multiple of a power of two.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="n">The base-two alignment exponent.</param>
    /// <returns>The aligned value.</returns>
    internal static int AlignPowerOf2(int value, int n)
    {
        int mask = (1 << n) - 1;
        return (value + mask) & ~mask;
    }

    /// <summary>
    /// Divides a value by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    internal static int RoundPowerOf2(int value, int n) => (value + ((1 << n) >> 1)) >> n;

    /// <summary>
    /// Clamps a signed integer to an inclusive range.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <returns>The clamped value.</returns>
    internal static int Clamp(int value, int low, int high)
        => Math.Max(low, Math.Min(high, value));

    /// <summary>
    /// Clamps a signed long integer to an inclusive range.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <returns>The clamped value.</returns>
    internal static long Clamp(long value, long low, long high)
        => Math.Max(low, Math.Min(high, value));

    /// <summary>
    /// Divides a value by a power of two with floor rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The floor-rounded quotient.</returns>
    internal static int DivideLog2Floor(int value, int n)
        => value >> n;

    /// <summary>
    /// Divides a nonnegative value by a power of two with ceiling rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The ceiling-rounded quotient.</returns>
    internal static int DivideLog2Ceiling(int value, int n)
        => (value + (1 << n) - 1) >> n;

    /// <summary>
    /// Divides a value by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    internal static int DivideRound(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;

    /// <summary>
    /// Gets the nonnegative remainder after division by eight.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The low three bits of the value.</returns>
    internal static int Modulus8(int value) => value & 0x07;

    /// <summary>
    /// Divides a value by eight with floor rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The floor-rounded quotient.</returns>
    internal static int DivideBy8Floor(int value) => value >> 3;

    /// <summary>
    /// Divides a signed value by a power of two with symmetric nearest-integer rounding.
    /// </summary>
    /// <param name="value">The signed value.</param>
    /// <param name="n">The base-two divisor exponent.</param>
    /// <returns>The signed rounded quotient.</returns>
    internal static int RoundPowerOf2Signed(int value, int n)
        => (value < 0) ? -RoundPowerOf2(-value, n) : RoundPowerOf2(value, n);

    /// <summary>
    /// Right-shifts a long intermediate with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bit">The positive shift count.</param>
    /// <returns>The rounded signed result.</returns>
    internal static int RoundShift(long value, int bit)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(bit, 1, nameof(bit));
        return (int)((value + (1L << (bit - 1))) >> bit);
    }

    /// <summary>
    /// Evaluates logical implication from one Boolean condition to another.
    /// </summary>
    /// <param name="a">The antecedent.</param>
    /// <param name="b">The consequent.</param>
    /// <returns><see langword="false"/> only when <paramref name="a"/> is true and <paramref name="b"/> is false.</returns>
    internal static bool Implies(bool a, bool b) => !a || b;

    /// <summary>
    /// Gets one bit from an integer value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The zero-based bit position.</param>
    /// <returns>Zero or one.</returns>
    internal static int GetBit(int value, int n)
        => (value & (1 << n)) >> n;

    /// <summary>
    /// Sets one bit in an integer value.
    /// </summary>
    /// <param name="endOfBlockExtra">The value to update.</param>
    /// <param name="n">The zero-based bit position.</param>
    internal static void SetBit(ref int endOfBlockExtra, int n)
        => endOfBlockExtra |= 1 << n;

    /// <summary>
    /// Gets the absolute difference between two integers.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The nonnegative absolute difference.</returns>
    internal static int AbsoluteDifference(int a, int b) => (a > b) ? a - b : b - a;
}
