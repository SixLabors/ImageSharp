// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="CieLuv"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
public class CieLabAndCieLuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(10, 36.0555, 303.6901, 10.6006718, -17.24077, 82.8835)]
    public void Convert_CieLuv_To_CieLab(float l, float u, float v, float l2, float a, float b)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        CieLab expected = new(l2, a, b);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<CieLuv, CieLab>(input);
        converter.Convert<CieLuv, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(10.0151367, -23.9644356, 17.0226, 10.0000038, -12.830183, 15.1829338)]
    public void Convert_CieLab_To_CieLuv(float l, float a, float b, float l2, float u, float v)
    {
        // Arrange
        CieLab input = new(l, a, b);
        CieLuv expected = new(l2, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<CieLab, CieLuv>(input);
        converter.Convert<CieLab, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
