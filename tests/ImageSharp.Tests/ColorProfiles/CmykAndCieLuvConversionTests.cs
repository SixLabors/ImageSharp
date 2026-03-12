// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Cmyk"/>-<see cref="CieLuv"/> conversions.
/// </summary>
public class CmykAndCieLuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 100, -1.937151E-05, -3.874302E-05)]
    [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 62.85024, -24.4844189, 54.8588524)]
    public void Convert_Cmyk_To_CieLuv(float c, float m, float y, float k, float l, float u, float v)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        CieLuv expected = new(l, u, v);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<Cmyk, CieLuv>(input);
        converter.Convert<Cmyk, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(100, -1.937151E-05, -3.874302E-05, 0, 5.96046448E-08, 0, 0)]
    [InlineData(62.85024, -24.4844189, 54.8588524, 0.2865809, 0, 0.797518551, 0.3498301)]
    public void Convert_CieLuv_To_Cmyk(float l, float u, float v, float c, float m, float y, float k)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<CieLuv, Cmyk>(input);
        converter.Convert<CieLuv, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
