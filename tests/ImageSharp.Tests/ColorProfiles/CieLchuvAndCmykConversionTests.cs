// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLchuv"/>-<see cref="Cmyk"/> conversions.
/// </summary>
public class CieLchuvAndCmykConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    [InlineData(0, 0.8576171, 0.7693201, 0.3440427, 36.0555, 103.6901, 10.01514)]
    public void Convert_Cmyk_to_CieLchuv(float c2, float m, float y, float k, float l, float c, float h)
    {
        // Arrange
        Cmyk input = new(c2, m, y, k);
        CieLchuv expected = new(l, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<CieLchuv> actualSpan = new CieLchuv[5];

        // Act
        CieLchuv actual = converter.Convert<Cmyk, CieLchuv>(input);
        converter.Convert<Cmyk, CieLchuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 1)]
    [InlineData(36.0555, 103.6901, 10.01514, 0, 0.8576171, 0.7693201, 0.3440427)]
    public void Convert_CieLchuv_to_Cmyk(float l, float c, float h, float c2, float m, float y, float k)
    {
        // Arrange
        CieLchuv input = new(l, c, h);
        Cmyk expected = new(c2, m, y, k);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLchuv> inputSpan = new CieLchuv[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<CieLchuv, Cmyk>(input);
        converter.Convert<CieLchuv, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
