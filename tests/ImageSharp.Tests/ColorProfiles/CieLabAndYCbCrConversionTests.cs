// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="YCbCr"/> conversions.
/// </summary>
public class CieLabAndYCbCrConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(1, .5F, .5F, 100, 0, 0)]
    [InlineData(0, .5F, .5F, 0, 0, 0)]
    [InlineData(.5F, .5F, .5F, 53.38897F, 0, 0)]
    public void Convert_YCbCr_to_CieLab(float y, float cb, float cr, float l, float a, float b)
    {
        // Arrange
        YCbCr input = new(y, cb, cr);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<YCbCr, CieLab>(input);
        converter.Convert<YCbCr, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(100, 0, 0, 1, .5F, .5F)]
    [InlineData(0, 0, 0, 0, .5F, .5F)]
    [InlineData(53.38897F, 0, 0, .5F, .5F, .5F)]
    public void Convert_CieLab_to_YCbCr(float l, float a, float b, float y, float cb, float cr)
    {
        // Arrange
        CieLab input = new(l, a, b);
        YCbCr expected = new(y, cb, cr);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<CieLab, YCbCr>(input);
        converter.Convert<CieLab, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
