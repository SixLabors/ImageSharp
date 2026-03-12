// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLch"/>-<see cref="CieLuv"/> conversions.
/// </summary>
public class CieLchAndCieLuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 103.6901, 10.01514, 34.89777, 187.6642, -7.181467)]
    public void Convert_CieLch_to_CieLuv(float l, float c, float h, float l2, float u, float v)
    {
        // Arrange
        CieLch input = new(l, c, h);
        CieLuv expected = new(l2, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<CieLch, CieLuv>(input);
        converter.Convert<CieLch, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(34.89777, 187.6642, -7.181467, 36.0555, 103.6901, 10.01514)]
    public void Convert_CieLuv_to_CieLch(float l2, float u, float v, float l, float c, float h)
    {
        // Arrange
        CieLuv input = new(l2, u, v);
        CieLch expected = new(l, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<CieLuv, CieLch>(input);
        converter.Convert<CieLuv, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
