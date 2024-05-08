// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Cmyk"/>-<see cref="YCbCr"/> conversions.
/// </summary>
public class CmykAndYCbCrConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 255, 128, 128)]
    [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 136.5134, 69.90555, 114.9948)]
    public void Convert_Cmyk_to_YCbCr(float c, float m, float y, float k, float y2, float cb, float cr)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        YCbCr expected = new(y2, cb, cr);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<Cmyk, YCbCr>(input);
        converter.Convert<Cmyk, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(255, 128, 128, 0, 0, 0, 5.960464E-08)]
    [InlineData(136.5134, 69.90555, 114.9948, 0.2891567, 0, 0.7951807, 0.3490196)]
    public void Convert_YCbCr_to_Cmyk(float y2, float cb, float cr, float c, float m, float y, float k)
    {
        // Arrange
        YCbCr input = new(y2, cb, cr);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<YCbCr, Cmyk>(input);
        converter.Convert<YCbCr, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
