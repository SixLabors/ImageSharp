// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="CieXyy"/> conversions.
/// </summary>
public class CieLuvAndCieXyyConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 103.6901, 10.01514, 0.5646762, 0.2932749, 0.09037033)]
    public void Convert_CieLuv_to_CieXyy(float l, float u, float v, float x, float y, float yl)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        CieXyy expected = new(x, y, yl);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<CieLuv, CieXyy>(input);
        converter.Convert<CieLuv, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.5646762, 0.2932749, 0.09037033, 36.0555, 103.6901, 10.01514)]
    public void Convert_CieXyy_to_CieLuv(float x, float y, float yl, float l, float u, float v)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<CieXyy, CieLuv>(input);
        converter.Convert<CieXyy, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
