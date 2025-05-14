// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="CieLchuv"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class CieLchuvAndCieLuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(54.2917, 106.8391, 40.8526, 54.2917, 80.8125, 69.8851)]
    [InlineData(100, 0, 0, 100, 0, 0)]
    [InlineData(100, 50, 180, 100, -50, 0)]
    [InlineData(10, 36.0555, 56.3099, 10, 20, 30)]
    [InlineData(10, 36.0555, 123.6901, 10, -20, 30)]
    [InlineData(10, 36.0555, 303.6901, 10, 20, -30)]
    [InlineData(10, 36.0555, 236.3099, 10, -20, -30)]
    public void Convert_CieLchuv_to_CieLuv(float l, float c, float h, float l2, float u, float v)
    {
        // Arrange
        CieLchuv input = new(l, c, h);
        CieLuv expected = new(l2, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLchuv> inputSpan = new CieLchuv[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<CieLchuv, CieLuv>(input);
        converter.Convert<CieLchuv, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(54.2917, 80.8125, 69.8851, 54.2917, 106.8391, 40.8526)]
    [InlineData(100, 0, 0, 100, 0, 0)]
    [InlineData(100, -50, 0, 100, 50, 180)]
    [InlineData(10, 20, 30, 10, 36.0555, 56.3099)]
    [InlineData(10, -20, 30, 10, 36.0555, 123.6901)]
    [InlineData(10, 20, -30, 10, 36.0555, 303.6901)]
    [InlineData(10, -20, -30, 10, 36.0555, 236.3099)]
    [InlineData(37.3511, 24.1720, 16.0684, 37.3511, 29.0255, 33.6141)]
    public void Convert_CieLuv_to_CieLchuv(float l, float u, float v, float l2, float c, float h)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        CieLchuv expected = new(l2, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<CieLchuv> actualSpan = new CieLchuv[5];

        // Act
        CieLchuv actual = converter.Convert<CieLuv, CieLchuv>(input);
        converter.Convert<CieLuv, CieLchuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
