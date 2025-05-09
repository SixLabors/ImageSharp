// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="Y"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated mathematically
/// </remarks>
public class RbgAndYConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.001F);

    [Theory]
    [InlineData(0F, 0F, 0F, 0F)]
    [InlineData(0.5F, 0.5F, 0.5F, 0.5F)]
    [InlineData(1F, 1F, 1F, 1F)]
    public void Convert_Y_To_Rgb(float y, float r, float g, float b)
    {
        // Arrange
        Y input = new(y);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<Y> inputSpan = new Y[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<Y, Rgb>(input);
        converter.Convert<Y, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0F, 0F, 0F, 0F)]
    [InlineData(0.5F, 0.5F, 0.5F, 0.5F)]
    [InlineData(1F, 1F, 1F, 1F)]
    public void Convert_Rgb_To_Y_BT601(float r, float g, float b, float y)
    {
        ColorConversionOptions options = new()
        {
            YCoefficients = KnownYCoefficients.BT601
        };

        Convert_Rgb_To_Y_Core(r, g, b, y, options);
    }

    [Theory]
    [InlineData(0F, 0F, 0F, 0F)]
    [InlineData(0.5F, 0.5F, 0.5F, 0.5F)]
    [InlineData(1F, 1F, 1F, 1F)]
    public void Convert_Rgb_To_Y_BT709(float r, float g, float b, float y)
    {
        ColorConversionOptions options = new()
        {
            YCoefficients = KnownYCoefficients.BT709
        };

        Convert_Rgb_To_Y_Core(r, g, b, y, options);
    }

    [Theory]
    [InlineData(0F, 0F, 0F, 0F)]
    [InlineData(0.5F, 0.5F, 0.5F, 0.49999997F)]
    [InlineData(1F, 1F, 1F, 0.99999994F)]
    public void Convert_Rgb_To_Y_BT2020(float r, float g, float b, float y)
    {
        ColorConversionOptions options = new()
        {
            YCoefficients = KnownYCoefficients.BT2020
        };

        Convert_Rgb_To_Y_Core(r, g, b, y, options);
    }

    private static void Convert_Rgb_To_Y_Core(float r, float g, float b, float y, ColorConversionOptions options)
    {
        // Arrange
        Rgb input = new(r, g, b);
        Y expected = new(y);
        ColorProfileConverter converter = new(options);

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<Y> actualSpan = new Y[5];

        // Act
        Y actual = converter.Convert<Rgb, Y>(input);
        converter.Convert<Rgb, Y>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
