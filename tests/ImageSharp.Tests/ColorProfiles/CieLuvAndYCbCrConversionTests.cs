// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="YCbCr"/> conversions.
/// </summary>
public class CieLuvAndYCbCrConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(100, 0, 0, 1, .5F, .5F)]
    [InlineData(0, 0, 0, 0, .5F, .5F)]
    [InlineData(53.38897F, 0, 0, .5F, .5F, .5F)]
    public void Convert_CieLuv_to_YCbCr(float l, float u, float v, float y, float cb, float cr)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        YCbCr expected = new(y, cb, cr);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<CieLuv, YCbCr>(input);
        converter.Convert<CieLuv, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, .5F, .5F, 100, 0, 0)]
    [InlineData(0, .5F, .5F, 0, 0, 0)]
    [InlineData(.5F, .5F, .5F, 53.38897F, 0, 0)]
    public void Convert_YCbCr_to_CieLuv(float y, float cb, float cr, float l, float u, float v)
    {
        // Arrange
        YCbCr input = new(y, cb, cr);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<YCbCr, CieLuv>(input);
        converter.Convert<YCbCr, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
