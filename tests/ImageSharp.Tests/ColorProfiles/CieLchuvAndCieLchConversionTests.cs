// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLchuv"/>-<see cref="CieLch"/> conversions.
/// </summary>
public class CieLchuvAndCieLchConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.73742, 64.79149, 30.1786, 36.0555, 103.6901, 10.01513)]
    public void Convert_CieLch_To_CieLchuv(float l2, float c2, float h2, float l, float c, float h)
    {
        // Arrange
        CieLch input = new(l2, c2, h2);
        CieLchuv expected = new(l, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<CieLchuv> actualSpan = new CieLchuv[5];

        // Act
        CieLchuv actual = converter.Convert<CieLch, CieLchuv>(input);
        converter.Convert<CieLch, CieLchuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(36.0555, 103.6901, 10.01514, 36.73742, 64.79149, 30.1786)]
    public void Convert_CieLchuv_To_CieLch(float l, float c, float h, float l2, float c2, float h2)
    {
        // Arrange
        CieLchuv input = new(l, c, h);
        CieLch expected = new(l2, c2, h2);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLchuv> inputSpan = new CieLchuv[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<CieLchuv, CieLch>(input);
        converter.Convert<CieLchuv, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
