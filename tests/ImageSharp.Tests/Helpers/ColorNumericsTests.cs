// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class ColorNumericsTests
{
    /// <summary>
    /// Grayscale luminance retains the source value even when it is outside the unit interval.
    /// </summary>
    /// <param name="value">The grayscale component value.</param>
    [Theory]
    [InlineData(-2F)]
    [InlineData(0.5F)]
    [InlineData(2F)]
    public void GetBT709Luminance_PreservesUnboundedGrayscale(float value)
    {
        Vector4 vector = new(value, value, value, 1F);

        Assert.Equal(value, ColorNumerics.GetBT709Luminance(vector));
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
