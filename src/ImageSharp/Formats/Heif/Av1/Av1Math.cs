// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1Math
{
    public static int MostSignificantBit(uint value)
    {
        int log = 0;
        int i;

        Guard.IsTrue(value != 0, nameof(value), "Must have al least 1 bit set");

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

    public static uint Log2(uint n)
    {
        uint result = 0U;
        while ((n >>= 1) > 0)
        {
            result++;
        }

        return result;
    }

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
    /// Long Log 2
    /// This is a quick adaptation of a Number
    /// Leading Zeros(NLZ) algorithm to get the log2f of a 32-bit number
    /// </summary>
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

    public static uint Clip1(uint value, int bitDepth) =>
        Clip3(0, (1U << bitDepth) - 1, value);

    public static uint Clip3(uint x, uint y, uint z)
    {
        if (z < x)
        {
            return x;
        }

        if (z > y)
        {
            return y;
        }

        return z;
    }

    public static int Clip3(int x, int y, int z)
    {
        if (z < x)
        {
            return x;
        }

        if (z > y)
        {
            return y;
        }

        return z;
    }

    public static uint Round2(uint value, int n)
    {
        if (n == 0)
        {
            return value;
        }

        return (uint)((value + (1 << (n - 1))) >> n);
    }

    public static int Round2(int value, int n)
    {
        if (value < 0)
        {
            value = -value;
        }

        return (int)Round2((uint)value, n);
    }

    internal static int AlignPowerOf2(int value, int n)
    {
        int mask = (1 << n) - 1;
        return (value + mask) & ~mask;
    }

    internal static int RoundPowerOf2(int value, int n) => (value + ((1 << n) >> 1)) >> n;

    internal static int Clamp(int value, int low, int high)
        => value < low ? low : (value > high ? high : value);

    internal static long Clamp(long value, long low, long high)
        => value < low ? low : (value > high ? high : value);

    internal static int DivideLog2Floor(int value, int n)
        => value >> n;

    internal static int DivideLog2Ceiling(int value, int n)
        => (value + (1 << n) - 1) >> n;

    // Last 3 bits are the value of mod 8.
    internal static int Modulus8(int value) => value & 0x07;

    internal static int DivideBy8Floor(int value) => value >> 3;

    internal static int RoundPowerOf2Signed(int value, int n)
        => (value < 0) ? -RoundPowerOf2(-value, n) : RoundPowerOf2(value, n);

    internal static int RoundShift(long value, int bit)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(bit, 1, nameof(bit));
        return (int)((value + (1L << (bit - 1))) >> bit);
    }

    /// <summary>
    /// <paramref name="a"/> implies <paramref name="b"/>.
    /// </summary>
    internal static bool Implies(bool a, bool b) => !a || b;
}
