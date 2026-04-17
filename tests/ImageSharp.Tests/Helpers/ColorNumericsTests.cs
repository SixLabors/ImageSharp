// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class ColorNumericsTests
{
    [Theory]
    [InlineData(0.2f, 0.7f, 0.1f, 256, 140)]
    [InlineData(0.5f, 0.5f, 0.5f, 256, 128)]
    [InlineData(0.5f, 0.5f, 0.5f, 65536, 32768)]
    [InlineData(0.2f, 0.7f, 0.1f, 65536, 36069)]
    public void GetBT709Luminance_WithVector4(float x, float y, float z, int luminanceLevels, int expected)
    {
        // arrange
        Vector4 vector = new(x, y, z, 0.0f);

        // act
        int actual = ColorNumerics.GetBT709Luminance(ref vector, luminanceLevels);

        // assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData((ushort)0, (byte)0)]
    [InlineData((ushort)128, (byte)0)]
    [InlineData((ushort)129, (byte)1)]
    [InlineData((ushort)257, (byte)1)]
    [InlineData((ushort)32896, (byte)128)]
    [InlineData(ushort.MaxValue, byte.MaxValue)]
    public void From16BitTo8Bit_ReturnsExpectedValue(ushort component, byte expected)
    {
        byte actual = ColorNumerics.From16BitTo8Bit(component);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void From16BitTo8Bit_RoundTripsAllExpanded8BitValues()
    {
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            byte expected = (byte)i;
            ushort component = ColorNumerics.From8BitTo16Bit(expected);

            byte actual = ColorNumerics.From16BitTo8Bit(component);

            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0U, (byte)0)]
    [InlineData(8421504U, (byte)0)]
    [InlineData(8421505U, (byte)1)]
    [InlineData(16843009U, (byte)1)]
    [InlineData(2155905152U, (byte)128)]
    [InlineData(uint.MaxValue, byte.MaxValue)]
    public void From32BitTo8Bit_ReturnsExpectedValue(uint component, byte expected)
    {
        byte actual = ColorNumerics.From32BitTo8Bit(component);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void From32BitTo8Bit_RoundTripsAllExpanded8BitValues()
    {
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            byte expected = (byte)i;
            uint component = ColorNumerics.From8BitTo32Bit(expected);

            byte actual = ColorNumerics.From32BitTo8Bit(component);

            Assert.Equal(expected, actual);
        }
    }
}
