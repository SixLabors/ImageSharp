// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="HunterLab"/> conversions.
/// </summary>
public class CieXyyAndHunterLabConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 31.6467056, -33.00599, 25.67032)]
    public void Convert_CieXyy_to_HunterLab(float x, float y, float yl, float l, float a, float b)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        HunterLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<HunterLab> actualSpan = new HunterLab[5];

        // Act
        HunterLab actual = converter.Convert<CieXyy, HunterLab>(input);
        converter.Convert<CieXyy, HunterLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(31.6467056, -33.00599, 25.67032, 0.360555, 0.936901, 0.1001514)]
    public void Convert_HunterLab_to_CieXyy(float l, float a, float b, float x, float y, float yl)
    {
        // Arrange
        HunterLab input = new(l, a, b);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<HunterLab> inputSpan = new HunterLab[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<HunterLab, CieXyy>(input);
        converter.Convert<HunterLab, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
