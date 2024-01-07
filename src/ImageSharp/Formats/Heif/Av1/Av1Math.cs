// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1Math
{
    public static uint MostSignificantBit(uint value) => value >> 31;

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

    internal static int Clamp(int value, int low, int high)
        => value < low ? low : (value > high ? high : value);
}
