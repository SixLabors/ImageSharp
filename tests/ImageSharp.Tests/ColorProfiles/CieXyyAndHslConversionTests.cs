// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="Hsl"/> conversions.
/// </summary>
public class CieXyyAndHslConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.2138507)]
    public void Convert_CieXyy_to_Hsl(float x, float y, float yl, float h, float s, float l)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        Hsl expected = new(h, s, l);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<Hsl> actualSpan = new Hsl[5];

        // Act
        Hsl actual = converter.Convert<CieXyy, Hsl>(input);
        converter.Convert<CieXyy, Hsl>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(120, 1, 0.2138507, 0.32114, 0.59787, 0.10976)]
    public void Convert_Hsl_to_CieXyy(float h, float s, float l, float x, float y, float yl)
    {
        // Arrange
        Hsl input = new(h, s, l);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Hsl> inputSpan = new Hsl[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<Hsl, CieXyy>(input);
        converter.Convert<Hsl, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
