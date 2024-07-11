// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="Hsv"/> conversions.
/// </summary>
public class CieXyzAndHsvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.9999999)]
    public void Convert_CieXyz_to_Hsv(float x, float y, float yl, float h, float s, float v)
    {
        // Arrange
        CieXyz input = new(x, y, yl);
        Hsv expected = new(h, s, v);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<Hsv> actualSpan = new Hsv[5];

        // Act
        Hsv actual = converter.Convert<CieXyz, Hsv>(input);
        converter.Convert<CieXyz, Hsv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(120, 1, 0.9999999, 0.3850648, 0.7168785, 0.09710446)]
    public void Convert_Hsv_to_CieXyz(float h, float s, float v, float x, float y, float yl)
    {
        // Arrange
        Hsv input = new(h, s, v);
        CieXyz expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Hsv> inputSpan = new Hsv[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<Hsv, CieXyz>(input);
        converter.Convert<Hsv, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
