// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Hsv"/> conversions.
/// </summary>
public class CieLabAndHsvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(336.9393, 1, 0.9999999, 55.063, 82.54871, 23.16504)]
    public void Convert_Hsv_to_CieLab(float h, float s, float v, float l, float a, float b)
    {
        // Arrange
        Hsv input = new(h, s, v);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Hsv> inputSpan = new Hsv[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Hsv, CieLab>(input);
        converter.Convert<Hsv, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(55.063, 82.54871, 23.16504, 336.9393, 1, 0.9999999)]
    public void Convert_CieLab_to_Hsv(float l, float a, float b, float h, float s, float v)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Hsv expected = new(h, s, v);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Hsv> actualSpan = new Hsv[5];

        // Act
        Hsv actual = converter.Convert<CieLab, Hsv>(input);
        converter.Convert<CieLab, Hsv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
