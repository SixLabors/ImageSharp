// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="HunterLab"/> conversions.
/// </summary>
public class CieLuvAndHunterLabConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 93.6901, 10.01514, 30.59289, 48.55542, 9.80487)]
    public void Convert_CieLuv_To_HunterLab(float l, float u, float v, float l2, float a, float b)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        HunterLab expected = new(l2, a, b);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<HunterLab> actualSpan = new HunterLab[5];

        // Act
        HunterLab actual = converter.Convert<CieLuv, HunterLab>(input);
        converter.Convert<CieLuv, HunterLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(30.59289, 48.55542, 9.80487, 36.0555, 93.6901, 10.01514)]
    public void Convert_HunterLab_To_CieLuv(float l2, float a, float b, float l, float u, float v)
    {
        // Arrange
        HunterLab input = new(l2, a, b);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<HunterLab> inputSpan = new HunterLab[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<HunterLab, CieLuv>(input);
        converter.Convert<HunterLab, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
