// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Cmyk"/>-<see cref="CieLch"/> conversions.
/// </summary>
public class CmykAndCieLchConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 62.85025, 64.77041, 118.2425)]
    public void Convert_Cmyk_To_CieLch(float c, float m, float y, float k, float l, float c2, float h)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        CieLch expected = new(l, c2, h);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<Cmyk, CieLch>(input);
        converter.Convert<Cmyk, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(100, 3.81656E-05, 218.6598, 0, 1.192093E-07, 0, 5.960464E-08)]
    [InlineData(62.85025, 64.77041, 118.2425, 0.286581, 0, 0.7975187, 0.34983)]
    public void Convert_CieLch_To_Cmyk(float l, float c2, float h, float c, float m, float y, float k)
    {
        // Arrange
        CieLch input = new(l, c2, h);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<CieLch, Cmyk>(input);
        converter.Convert<CieLch, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
