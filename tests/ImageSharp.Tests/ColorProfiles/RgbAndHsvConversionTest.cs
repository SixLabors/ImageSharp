// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="Hsv"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.colorhexa.com"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class RgbAndHsvConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 1, 1, 1)]
    [InlineData(360, 1, 1, 1, 0, 0)]
    [InlineData(0, 1, 1, 1, 0, 0)]
    [InlineData(120, 1, 1, 0, 1, 0)]
    [InlineData(240, 1, 1, 0, 0, 1)]
    public void Convert_Hsv_To_Rgb(float h, float s, float v, float r, float g, float b)
    {
        // Arrange
        Hsv input = new(h, s, v);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<Hsv> inputSpan = new Hsv[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<Hsv, Rgb>(input);
        converter.Convert<Hsv, Rgb>(inputSpan, actualSpan);

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
    [InlineData(1, 0, 0, 0, 1, 1)]
    [InlineData(0, 1, 0, 120, 1, 1)]
    [InlineData(0, 0, 1, 240, 1, 1)]
    public void Convert_Rgb_To_Hsv(float r, float g, float b, float h, float s, float v)
    {
        // Arrange
        Rgb input = new(r, g, b);
        Hsv expected = new(h, s, v);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<Hsv> actualSpan = new Hsv[5];

        // Act
        Hsv actual = converter.Convert<Rgb, Hsv>(input);
        converter.Convert<Rgb, Hsv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
