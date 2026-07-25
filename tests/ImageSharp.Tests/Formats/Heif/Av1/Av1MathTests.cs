// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1MathTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    public void TestMostSignificantBit(uint value, int expected)
    {
        int actual = Av1Math.MostSignificantBit(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    public void TestLog2(int value, int expected)
    {
        int actual = Av1Math.Log2(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    public void TestFloorLog2(uint value, uint expected)
    {
        uint actual = Av1Math.FloorLog2(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    public void TestLog2_32(uint value, uint expected)
    {
        uint actual = Av1Math.Log2_32(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    public void TestLog2Ceiling(uint value, uint expected)
    {
        uint actual = Av1Math.CeilLog2(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(4, 2, 1)]
    [InlineData(4, 3, 0)]
    [InlineData(5, 3, 0)]
    [InlineData(8, 3, 1)]
    [InlineData(9, 3, 1)]
    [InlineData(9, 0, 1)]
    [InlineData(8, 0, 0)]
    public void TestGetBitSet(int value, int n, int expected)
    {
        int actual = Av1Math.GetBit(value, n);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(4, 2, 4)]
    [InlineData(0, 2, 4)]
    [InlineData(0, 3, 8)]
    [InlineData(4, 3, 12)]
    public void TestSetBitSet(int value, int n, int expected)
    {
        int actual = value;
        Av1Math.SetBit(ref actual, n);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(255, 4, 4)]
    [InlineData(255, -1, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 256, 255)]
    [InlineData(255, 1000, 255)]
    public void TestClip3(int max, int value, int expected)
    {
        int actual = Av1Math.Clip3(0, max, value);
        Assert.Equal(expected, actual);
    }
}
