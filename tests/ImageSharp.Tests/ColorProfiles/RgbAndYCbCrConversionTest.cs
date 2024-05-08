// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="YCbCr"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated mathematically
/// </remarks>
public class RgbAndYCbCrConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.001F);

    [Theory]
    [InlineData(255, 128, 128, 1, 1, 1)]
    [InlineData(0, 128, 128, 0, 0, 0)]
    [InlineData(128, 128, 128, 0.502, 0.502, 0.502)]
    public void Convert_YCbCr_To_Rgb(float y, float cb, float cr, float r, float g, float b)
    {
        // Arrange
        YCbCr input = new(y, cb, cr);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<YCbCr, Rgb>(input);
        converter.Convert<YCbCr, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 128, 128)]
    [InlineData(1, 1, 1, 255, 128, 128)]
    [InlineData(0.5, 0.5, 0.5, 127.5, 128, 128)]
    [InlineData(1, 0, 0, 76.245, 84.972, 255)]
    public void Convert_Rgb_To_YCbCr(float r, float g, float b, float y, float cb, float cr)
    {
        // Arrange
        Rgb input = new(r, g, b);
        YCbCr expected = new(y, cb, cr);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<Rgb, YCbCr>(input);
        converter.Convert<Rgb, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
