// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="Hsv"/> conversions.
/// </summary>
public class CieXyyAndHsvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.42770)]
    public void Convert_CieXyy_to_Hsv(float x, float y, float yl, float h, float s, float v)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        Hsv expected = new(h, s, v);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<Hsv> actualSpan = new Hsv[5];

        // Act
        Hsv actual = converter.Convert<CieXyy, Hsv>(input);
        converter.Convert<CieXyy, Hsv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(120, 1, 0.42770, 0.32114, 0.59787, 0.10976)]
    public void Convert_Hsv_to_CieXyy(float h, float s, float v, float x, float y, float yl)
    {
        // Arrange
        Hsv input = new(h, s, v);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Hsv> inputSpan = new Hsv[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<Hsv, CieXyy>(input);
        converter.Convert<Hsv, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
