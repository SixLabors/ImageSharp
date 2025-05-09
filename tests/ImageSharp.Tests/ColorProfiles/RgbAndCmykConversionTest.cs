// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="Cmyk"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.colorhexa.com"/>
/// <see href="http://www.rapidtables.com/convert/color/cmyk-to-rgb.htm"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class RgbAndCmykConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(1, 1, 1, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 1, 1, 1)]
    [InlineData(0, 0.84, 0.037, 0.365, 0.635, 0.1016, 0.6115)]
    public void Convert_Cmyk_To_Rgb(float c, float m, float y, float k, float r, float g, float b)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<Cmyk, Rgb>(input);
        converter.Convert<Cmyk, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 1)]
    [InlineData(0.635, 0.1016, 0.6115, 0, 0.84, 0.037, 0.365)]
    public void Convert_Rgb_To_Cmyk(float r, float g, float b, float c, float m, float y, float k)
    {
        // Arrange
        Rgb input = new(r, g, b);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<Rgb, Cmyk>(input);
        converter.Convert<Rgb, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
