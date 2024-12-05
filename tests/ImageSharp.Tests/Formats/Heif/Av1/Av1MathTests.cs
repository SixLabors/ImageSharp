// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1MathTests
{
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
}
