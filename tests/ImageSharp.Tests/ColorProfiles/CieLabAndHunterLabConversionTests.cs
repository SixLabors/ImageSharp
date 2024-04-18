// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="HunterLab"/> conversions.
/// </summary>
public class CieLabAndHunterLabConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(27.51646, 556.9392, -0.03974226, 33.074177, 281.48329, -0.06948)]
    public void Convert_HunterLab_to_CieLab(float l2, float a2, float b2, float l, float a, float b)
    {
        // Arrange
        HunterLab input = new(l2, a2, b2);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<HunterLab> inputSpan = new HunterLab[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<HunterLab, CieLab>(input);
        converter.Convert<HunterLab, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(33.074177, 281.48329, -0.06948, 27.51646, 556.9392, -0.03974226)]
    public void Convert_CieLab_to_HunterLab(float l, float a, float b, float l2, float a2, float b2)
    {
        // Arrange
        CieLab input = new(l, a, b);
        HunterLab expected = new(l2, a2, b2);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<HunterLab> actualSpan = new HunterLab[5];

        // Act
        HunterLab actual = converter.Convert<CieLab, HunterLab>(input);
        converter.Convert<CieLab, HunterLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
