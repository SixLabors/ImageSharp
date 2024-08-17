// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Rgb"/> conversions.
/// </summary>
public class CieLabAndRgbConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);
    private static readonly ColorProfileConverter Converter = new();

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.9999999, 0, 0.384345, 55.063, 82.54871, 23.16505)]
    public void Convert_Rgb_to_CieLab(float r, float g, float b2, float l, float a, float b)
    {
        // Arrange
        Rgb input = new(r, g, b2);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Rgb, CieLab>(input);
        converter.Convert<Rgb, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(55.063, 82.54871, 23.16505, 0.9999999, 0, 0.384345)]
    public void Convert_CieLab_to_Rgb(float l, float a, float b, float r, float g, float b2)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Rgb expected = new(r, g, b2);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<CieLab, Rgb>(input);
        converter.Convert<CieLab, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
