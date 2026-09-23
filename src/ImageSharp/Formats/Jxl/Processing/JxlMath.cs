// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Common math functions used by the JPEG XL codec.
/// </summary>
internal static class JxlMath
{
    /// <summary>
    /// Number of bits in a single byte.
    /// </summary>
    public const int BitsPerByte = 8;   // This makes it more clear than just typing the number 8

    /// <summary>
    /// Default intensity target constant.
    /// </summary>
    public const float DefaultIntensityTarget = 255f;

    /// <summary>
    /// Multiplier for conversion of log2(x) result to ln(x). The
    /// value is derived by <c>1.0f / MathF.Log2(MathF.E).</c>
    /// </summary>
    public const float InverseLog2E = 0.6931471805599453f;

    /// <summary>
    /// Integer division by default is truncated toward zero. This
    /// function performs division and ceiling without any floating-point
    /// operations.
    /// </summary>
    /// <param name="a">Dividend</param>
    /// <param name="b">Divisor</param>
    /// <returns>
    /// Result of the division with ceiling. It is equivalent to
    /// <c>MathF.Ceiling(a / b)</c> without any floating-point usage.
    /// </returns>
    /// <remarks>
    /// This function only works for positive values. Division will be
    /// incorrect for negative values.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DivCeil(int a, int b)
    {
        unchecked
        {
            // This is a wrapper for Numerics.DivideCeil but for
            // int input values.
            return (int)Numerics.DivideCeil((uint)a, (uint)b);
        }
    }

    /// <summary>
    /// Ensures that <paramref name="value"/> rounded to the multiple of <paramref name="align"/>.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <param name="align">The allowed step size.</param>
    /// <returns>
    /// <paramref name="value"/>, transformed to ensure that it stays within the step size of <paramref name="align"/>.
    /// </returns>
    /// <remarks>
    ///   <para>
    ///     If value=4, align=6, the result is 6. If value=7 and align=3 the result is 9.
    ///   </para>
    ///   <para>
    ///     This will not work correctly for negative values.
    ///   </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundUpTo(int value, int align) => DivCeil(value, align) * align;

    /// <summary>
    /// Ensures that <paramref name="value"/> rounded to the multiple of <paramref name="align"/>.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <param name="align">The allowed step size.</param>
    /// <returns>
    /// <paramref name="value"/>, transformed to ensure that it stays within the step size of <paramref name="align"/>.
    /// </returns>
    /// <remarks>
    ///   <para>
    ///     If value=4, align=6, the result is 6. If value=7 and align=3 the result is 9.
    ///   </para>
    ///   <para>
    ///     This will not work correctly for negative values.
    ///   </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpTo(uint value, uint align) => Numerics.DivideCeil(value, align) * align;

    /// <summary>
    /// Performs subtraction &amp; returns a boolean indicating whether or not did the subtraction
    /// result in an overflow.
    /// </summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <param name="c">Subtracted value.</param>
    /// <returns>True if the subtraction led to an overflow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SubOverflow(uint a, uint b, out uint c)
    {
        c = a - b;
        return (((a ^ b) & (a ^ c)) >> 31) != 0;
    }

    /// <summary>
    /// Performs subtraction &amp; returns a boolean indicating whether or not did the subtraction
    /// result in an overflow.
    /// </summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <param name="c">Subtracted value.</param>
    /// <returns>True if the subtraction led to an overflow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SubOverflow(int a, int b, out int c)
    {
        c = 0;
        unchecked
        {
            return SubOverflow((uint)a, (uint)b, out Unsafe.As<int, uint>(ref c));
        }
    }

    /// <summary>
    /// A slow, but safe multiplication method which returns false on an overflow.
    /// </summary>
    /// <param name="a">The multiplier</param>
    /// <param name="b">The multiplicand</param>
    /// <param name="product">The resulting value (or 0 on error)</param>
    /// <returns>
    /// If the multiplication leads to an overflow returns false,
    /// otherwise returns true and places the output in <paramref name="product"/>.
    /// </returns>
    /// <remarks>
    /// This method is meant for unsigned multiplication only. Negative values
    /// won't work properly.
    /// </remarks>
    public static bool SafeMultiply(int a, int b, out int product)
    {
        product = 0;

        if (a == 0 || b == 0)
        {
            return true;
        }

        if (b > (int.MaxValue / a))
        {
            return false;
        }

        product = a * b;

        return true;
    }

    /// <summary>
    /// A slow, but safe multiplication method which returns false on an overflow.
    /// </summary>
    /// <param name="a">The multiplier</param>
    /// <param name="b">The multiplicand</param>
    /// <param name="product">The resulting value (or 0 on error)</param>
    /// <returns>
    /// If the multiplication leads to an overflow returns false,
    /// otherwise returns true and places the output in <paramref name="product"/>.
    /// </returns>
    /// <remarks>
    /// This method is meant for unsigned multiplication only. Negative values
    /// won't work properly.
    /// </remarks>
    public static bool SafeMultiply(uint a, uint b, out uint product)
    {
        product = 0;

        if (a == 0 || b == 0)
        {
            return true;
        }

        if (b > (uint.MaxValue / a))
        {
            return false;
        }

        product = a * b;

        return true;
    }

    /// <summary>
    /// Performs addition and returns a boolean indicating whether the addition led to
    /// an overflow.
    /// </summary>
    /// <param name="a">The augend</param>
    /// <param name="b">The addend</param>
    /// <param name="sum">The resulting sum of addition.</param>
    /// <returns>A boolean indicating whether the addition led to an overflow.</returns>
    /// <remarks>
    /// This method is meant for unsigned addition only. Negative values
    /// won't work properly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SafeAdd(int a, int b, out int sum)
    {
        unchecked
        {
            sum = a + b;
            return sum >= a;
        }
    }

    /// <summary>
    /// Performs addition and returns a boolean indicating whether the addition led to
    /// an overflow.
    /// </summary>
    /// <param name="a">The augend</param>
    /// <param name="b">The addend</param>
    /// <param name="sum">The resulting sum of addition.</param>
    /// <returns>A boolean indicating whether the addition led to an overflow.</returns>
    /// <remarks>
    /// This method is meant for unsigned addition only. Negative values
    /// won't work properly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SafeAdd(long a, long b, out long sum)
    {
        unchecked
        {
            sum = a + b;
            return sum >= a;
        }
    }

    /// <summary>
    /// Performs addition and returns a boolean indicating whether the addition led to
    /// an overflow.
    /// </summary>
    /// <param name="a">The augend</param>
    /// <param name="b">The addend</param>
    /// <param name="sum">The resulting sum of addition.</param>
    /// <returns>A boolean indicating whether the addition led to an overflow.</returns>
    /// <remarks>
    /// This method is meant for unsigned addition only. Negative values
    /// won't work properly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SafeAdd(uint a, uint b, out uint sum)
    {
        unchecked
        {
            sum = a + b;
            return sum >= a;
        }
    }

    /// <summary>
    /// Performs addition and returns a boolean indicating whether the addition led to
    /// an overflow.
    /// </summary>
    /// <param name="a">The augend</param>
    /// <param name="b">The addend</param>
    /// <param name="sum">The resulting sum of addition.</param>
    /// <returns>A boolean indicating whether the addition led to an overflow.</returns>
    /// <remarks>
    /// This method is meant for unsigned addition only. Negative values
    /// won't work properly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SafeAdd(ulong a, ulong b, out ulong sum)
    {
        unchecked
        {
            sum = a + b;
            return sum >= a;
        }
    }

    /// <summary>
    /// Ensures that the value stays within the specified range.
    /// </summary>
    /// <param name="value">The input value</param>
    /// <param name="low">The lower bound</param>
    /// <param name="high">The upper bound</param>
    /// <returns>
    /// <paramref name="low" /> if value is lower than <paramref name="low"/>. <paramref name="high"/>
    /// if value is higher than <paramref name="high"/>. Otherwise, <paramref name="value"/> if it's within
    /// the range.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp1(int value, int low, int high) => Numerics.Clamp(value, low, high);

    /// <summary>
    /// Rounds the dimensions up to block dimensions.
    /// </summary>
    /// <param name="dim">The dimensions to round up to block dimensions.</param>
    /// <returns>The rounded dimensions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundUpToBlockDimensions(int dim)
    {
        unchecked
        {
            return (dim + 7) & ~7;
        }
    }

    /// <summary>
    /// Rounds the dimensions up to block dimensions.
    /// </summary>
    /// <param name="dim">The dimensions to round up to block dimensions.</param>
    /// <returns>The rounded dimensions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpToBlockDimensions(uint dim)
    {
        unchecked
        {
            return (dim + 7u) & ~7u;
        }
    }

    /// <summary>
    /// Rounds the specified number of bits to a multiple of bytes.
    /// </summary>
    /// <param name="bits">The input bits.</param>
    /// <returns>Bits rounded to the multiples of bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundUpBitsToByteMultiple(int bits)
    {
        unchecked
        {
            return (bits + 7) & ~7;
        }
    }

    /// <summary>
    /// Rounds the specified number of bits to a multiple of bytes.
    /// </summary>
    /// <param name="bits">The input bits.</param>
    /// <returns>Bits rounded to the multiples of bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RoundUpBitsToByteMultiple(uint bits)
    {
        unchecked
        {
            return (bits + 7u) & ~7u;
        }
    }

    /// <summary>
    /// Multiplies <paramref name="multiplier"/> by π.
    /// </summary>
    /// <param name="multiplier">The value to multiply.</param>
    /// <returns>
    ///   <c>multiplier * π</c>
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pi(float multiplier) => multiplier * MathF.PI;

    /// <summary>
    /// Multiplies <paramref name="multiplier"/> by π.
    /// </summary>
    /// <param name="multiplier">The value to multiply.</param>
    /// <returns>
    ///   <c>multiplier * π</c>
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Pi(double multiplier) => multiplier * Math.PI;

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Num0BitsAboveMS1Bit_Nonzero(uint x)
    {
        DebugGuard.MustBeGreaterThan(x, 0u, nameof(x));

        return BitOperations.LeadingZeroCount(x);
    }

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Num0BitsAboveMS1Bit_Nonzero(int x)
    {
        unchecked
        {
            return Num0BitsAboveMS1Bit_Nonzero((uint)x);
        }
    }

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Num0BitsAboveMS1Bit_Nonzero(ulong x)
    {
        DebugGuard.MustBeGreaterThan(x, 0uL, nameof(x));

        return BitOperations.LeadingZeroCount(x);
    }

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Num0BitsAboveMS1Bit_Nonzero(long x)
    {
        unchecked
        {
            return Num0BitsAboveMS1Bit_Nonzero((ulong)x);
        }
    }

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit_Nonzero(uint x)
    {
        DebugGuard.MustBeGreaterThan(x, 0u, nameof(x));

        return (uint)BitOperations.TrailingZeroCount(x);
    }

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit_Nonzero(ulong x)
    {
        DebugGuard.MustBeGreaterThan(x, 0uL, nameof(x));

        return (uint)BitOperations.TrailingZeroCount(x);
    }

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit_Nonzero(int x) => Num0BitsBelowLS1Bit_Nonzero((uint)x);

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit_Nonzero(long x) => Num0BitsBelowLS1Bit_Nonzero((ulong)x);

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit(uint x) => x == 0 ? 32u : Num0BitsBelowLS1Bit_Nonzero(x);

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit(int x) => Num0BitsBelowLS1Bit_Nonzero((uint)x);

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit(ulong x) => x == 0 ? 64u : Num0BitsBelowLS1Bit_Nonzero(x);

    /// <summary>
    /// Returns the number of 0 bits to the right of the lowest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits after the lowest 1 bit. This is the equivalent
    /// of trailing zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsBelowLS1Bit(long x) => Num0BitsBelowLS1Bit_Nonzero((ulong)x);

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Num0BitsAboveMS1Bit(int x) => x == 0 ? sizeof(int) * 8 : Num0BitsAboveMS1Bit_Nonzero(x);

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Num0BitsAboveMS1Bit(uint x) => x == 0 ? sizeof(uint) * 8u : unchecked((uint)Num0BitsAboveMS1Bit_Nonzero((int)x));

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Num0BitsAboveMS1Bit(long x) => x == 0 ? sizeof(long) * 8L : Num0BitsAboveMS1Bit_Nonzero(x);

    /// <summary>
    /// Returns the amount of zero bits before the highest 1 bit.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>
    /// Number of zero bits before the highest 1 bit. This is the equivalent
    /// of leading zero count.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Num0BitsAboveMS1Bit(ulong x) => x == 0 ? sizeof(ulong) * 8uL : unchecked((ulong)Num0BitsAboveMS1Bit_Nonzero((long)x));

    /// <summary>
    /// Integer equivalent of MathF.Floor(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Floor(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FloorLog2Nonzero(uint x) => (uint)(((sizeof(uint) * 8) - 1) ^ Num0BitsAboveMS1Bit_Nonzero(x));

    /// <summary>
    /// Integer equivalent of MathF.Floor(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Floor(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorLog2Nonzero(int x) => ((sizeof(int) * 8) - 1) ^ Num0BitsAboveMS1Bit_Nonzero(x);

    /// <summary>
    /// Integer equivalent of MathF.Floor(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Floor(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long FloorLog2Nonzero(long x) => ((sizeof(long) * 8) - 1) ^ Num0BitsAboveMS1Bit_Nonzero(x);

    /// <summary>
    /// Integer equivalent of MathF.Floor(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Floor(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FloorLog2Nonzero(ulong x) => (ulong)(((sizeof(ulong) * 8) - 1) ^ Num0BitsAboveMS1Bit_Nonzero(x));

    /// <summary>
    /// Integer equivalent of MathF.Ceiling(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Ceil(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CeilLog2Nonzero(int x)
    {
        int floorLog2 = FloorLog2Nonzero(x);

        if ((x & (x - 1)) == 0)
        {
            return floorLog2;
        }

        return floorLog2 + 1;
    }

    /// <summary>
    /// Integer equivalent of MathF.Ceiling(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Ceil(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CeilLog2Nonzero(uint x)
    {
        uint floorLog2 = FloorLog2Nonzero(x);

        if ((x & (x - 1)) == 0)
        {
            return floorLog2;
        }

        return floorLog2 + 1;
    }

    /// <summary>
    /// Integer equivalent of MathF.Ceiling(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Ceil(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CeilLog2Nonzero(long x)
    {
        long floorLog2 = FloorLog2Nonzero(x);

        if ((x & (x - 1)) == 0)
        {
            return floorLog2;
        }

        return floorLog2 + 1;
    }

    /// <summary>
    /// Integer equivalent of MathF.Ceiling(MathF.Log2(x)).
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Ceil(Log2(x)) in integer form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CeilLog2Nonzero(ulong x)
    {
        ulong floorLog2 = FloorLog2Nonzero(x);

        if ((x & (x - 1)) == 0)
        {
            return floorLog2;
        }

        return floorLog2 + 1;
    }

    /// <summary>
    /// Computes the hypotenuse of x and y.
    /// </summary>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>Hypotenuse of x and y</returns>
    public static float Hypot(float x, float y) => Hypot<float>(x, y);

    /// <summary>
    /// Computes the hypotenuse of x and y.
    /// </summary>
    /// <param name="x">X</param>
    /// <param name="y">Y</param>
    /// <returns>Hypotenuse of x and y</returns>
    public static double Hypot(double x, double y) => Hypot<double>(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Hypot<T>(T x, T y)
        where T : INumber<T>, IRootFunctions<T>
    {
        x = T.Abs(x);
        y = T.Abs(y);

        if (x < y)
        {
            RuntimeUtility.Swap(ref x, ref y);
        }

        if (x == T.Zero)
        {
            return T.Zero;
        }

        T ratio = y / x;
        return x * T.Sqrt(T.One + (ratio * ratio));
    }
}
