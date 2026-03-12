// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="Rgb"/> conversions.
/// </summary>
public class CieXyyAndRgbConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 0, 0.4277014, 0)]
    public void Convert_CieXyy_to_Rgb(float x, float y, float yl, float r, float g, float b)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<CieXyy, Rgb>(input);
        converter.Convert<CieXyy, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 0.4277014, 0, 0.32114, 0.59787, 0.10976)]
    public void Convert_Rgb_to_CieXyy(float r, float g, float b, float x, float y, float yl)
    {
        // Arrange
        Rgb input = new(r, g, b);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<Rgb, CieXyy>(input);
        converter.Convert<Rgb, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
