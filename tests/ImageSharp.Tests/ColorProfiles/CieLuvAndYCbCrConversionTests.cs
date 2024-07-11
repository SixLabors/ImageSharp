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
    [InlineData(0, 0, 0, 0, 128, 128)]
    [InlineData(36.0555, 93.6901, 10.01514, 71.8283, 119.3174, 193.9839)]
    public void Convert_CieLuv_to_YCbCr(float l, float u, float v, float y, float cb, float cr)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        YCbCr expected = new(y, cb, cr);
        ColorConversionOptions options = new() { WhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
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
    [InlineData(0, 128, 128, 0, 0, 0)]
    [InlineData(71.8283, 119.3174, 193.9839, 36.00565, 93.44593, 10.2234)]
    public void Convert_YCbCr_to_CieLuv(float y, float cb, float cr, float l, float u, float v)
    {
        // Arrange
        YCbCr input = new(y, cb, cr);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { WhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
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
