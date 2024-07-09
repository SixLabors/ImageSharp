// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="Hsl"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.colorhexa.com"/>
/// <see href="http://www.rapidtables.com/convert/color/hsl-to-rgb"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class RgbAndHslConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 1, 1, 1, 1, 1)]
    [InlineData(360, 1, 1, 1, 1, 1)]
    [InlineData(0, 1, .5F, 1, 0, 0)]
    [InlineData(120, 1, .5F, 0, 1, 0)]
    [InlineData(240, 1, .5F, 0, 0, 1)]
    public void Convert_Hsl_To_Rgb(float h, float s, float l, float r, float g, float b)
    {
        // Arrange
        Hsl input = new(h, s, l);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<Hsl> inputSpan = new Hsl[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<Hsl, Rgb>(input);
        converter.Convert<Hsl, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 1, 1, 0, 0, 1)]
    [InlineData(1, 0, 0, 0, 1, .5F)]
    [InlineData(0, 1, 0, 120, 1, .5F)]
    [InlineData(0, 0, 1, 240, 1, .5F)]
    [InlineData(0.7, 0.8, 0.6, 90, 0.3333, 0.7F)]
    public void Convert_Rgb_To_Hsl(float r, float g, float b, float h, float s, float l)
    {
        // Arrange
        Rgb input = new(r, g, b);
        Hsl expected = new(h, s, l);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<Hsl> actualSpan = new Hsl[5];

        // Act
        Hsl actual = converter.Convert<Rgb, Hsl>(input);
        converter.Convert<Rgb, Hsl>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
