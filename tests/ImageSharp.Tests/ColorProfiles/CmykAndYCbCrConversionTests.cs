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
    [InlineData(0, 0, 0, 1, 0, .5F, .5F)]
    [InlineData(0, 0, 0, 0, 1, .5F, .5F)]
    [InlineData(0, .8570679F, .49999997F, 0, .439901F, .5339159F, .899500132F)]
    public void Convert_Cmyk_To_YCbCr(float c, float m, float y, float k, float y2, float cb, float cr)
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
    [InlineData(0, .5F, .5F, 0, 0, 0, 1)]
    [InlineData(1, .5F, .5F, 0, 0, 0, 0)]
    [InlineData(.5F, .5F, 1F, 0, .8570679F, .49999997F, 0)]
    public void Convert_YCbCr_To_Cmyk(float y2, float cb, float cr, float c, float m, float y, float k)
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
