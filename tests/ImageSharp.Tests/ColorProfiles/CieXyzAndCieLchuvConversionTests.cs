// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="CieLchuv"/> conversions.
/// </summary>
public class CieXyzAndCieLchuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0.360555, 0.936901, 0.1001514, 97.50697, 177.345169, 142.601547)]
    public void Convert_CieXyz_to_CieLchuv(float x, float y, float yl, float l, float c, float h)
    {
        // Arrange
        CieXyz input = new(x, y, yl);
        CieLchuv expected = new(l, c, h);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieLchuv> actualSpan = new CieLchuv[5];

        // Act
        CieLchuv actual = converter.Convert<CieXyz, CieLchuv>(input);
        converter.Convert<CieXyz, CieLchuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(97.50697, 177.345169, 142.601547, 0.360555, 0.936901, 0.1001514)]
    public void Convert_CieLchuv_to_CieXyz(float l, float c, float h, float x, float y, float yl)
    {
        // Arrange
        CieLchuv input = new(l, c, h);
        CieXyz expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<CieLchuv> inputSpan = new CieLchuv[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<CieLchuv, CieXyz>(input);
        converter.Convert<CieLchuv, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
