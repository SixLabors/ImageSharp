// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="YccK"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated mathematically
/// </remarks>
public class RgbAndYccKConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.001F);

    [Theory]
    [InlineData(1, .5F, .5F, 0, 1, 1, 1)]
    [InlineData(0, .5F, .5F, 1, 0, 0, 0)]
    [InlineData(.5F, .5F, .5F, 0, .5F, .5F, .5F)]
    public void Convert_YccK_To_Rgb(float y, float cb, float cr, float k, float r, float g, float b)
    {
        // Arrange
        YccK input = new(y, cb, cr, k);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<YccK> inputSpan = new YccK[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<YccK, Rgb>(input);
        converter.Convert<YccK, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 1, .5F, .5F, 0)]
    [InlineData(0, 0, 0, 0, .5F, .5F, 1)]
    [InlineData(.5F, .5F, .5F, 1, .5F, .5F, .5F)]
    public void Convert_Rgb_To_YccK(float r, float g, float b, float y, float cb, float cr, float k)
    {
        // Multiple YccK representations can decode to the same RGB value.
        // For example, (Y=1.0, Cb=0.5, Cr=0.5, K=0.5) and (Y=0.5, Cb=0.5, Cr=0.5, K=0.0) both yield RGB (0.5, 0.5, 0.5).
        // This is expected because YccK is not a unique encoding — K modulates RGB after YCbCr decoding.
        // Round-tripping RGB -> YccK -> RGB is stable, but YccK -> RGB -> YccK is not injective.

        // Arrange
        Rgb input = new(r, g, b);
        YccK expected = new(y, cb, cr, k);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<YccK> actualSpan = new YccK[5];

        // Act
        YccK actual = converter.Convert<Rgb, YccK>(input);
        converter.Convert<Rgb, YccK>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
