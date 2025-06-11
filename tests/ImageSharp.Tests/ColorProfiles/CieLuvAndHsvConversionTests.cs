// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="Hsv"/> conversions.
/// </summary>
public class CieLuvAndHsvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 93.6901, 10.01514, 347.3767, 0.8314762, 0.6444615)]
    public void Convert_CieLuv_to_Hsv(float l, float u, float v, float h, float s, float v2)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        Hsv expected = new(h, s, v2);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<Hsv> actualSpan = new Hsv[5];

        // Act
        Hsv actual = converter.Convert<CieLuv, Hsv>(input);
        converter.Convert<CieLuv, Hsv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(347.3767, 0.8314762, 0.6444615, 36.0555, 93.69012, 10.01514)]
    public void Convert_Hsv_to_CieLuv(float h, float s, float v2, float l, float u, float v)
    {
        // Arrange
        Hsv input = new(h, s, v2);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<Hsv> inputSpan = new Hsv[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<Hsv, CieLuv>(input);
        converter.Convert<Hsv, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
