// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="CieLch"/> conversions.
/// </summary>
public class CieXyzAndCieLchConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 97.50697, 161.235321, 143.157)]
    public void Convert_CieXyz_to_CieLch(float x, float y, float yl, float l, float c, float h)
    {
        // Arrange
        CieXyz input = new(x, y, yl);
        CieLch expected = new(l, c, h);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<CieXyz, CieLch>(input);
        converter.Convert<CieXyz, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(97.50697, 161.235321, 143.157, 0.3605551, 0.936901, 0.1001514)]
    public void Convert_CieLch_to_CieXyz(float l, float c, float h, float x, float y, float yl)
    {
        // Arrange
        CieLch input = new(l, c, h);
        CieXyz expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<CieLch, CieXyz>(input);
        converter.Convert<CieLch, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
