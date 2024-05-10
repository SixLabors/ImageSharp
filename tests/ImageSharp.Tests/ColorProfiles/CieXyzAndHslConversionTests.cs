// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="Hsl"/> conversions.
/// </summary>
public class CieXyzAndHslConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.5)]
    public void Convert_CieXyz_to_Hsl(float x, float y, float yl, float h, float s, float l)
    {
        // Arrange
        CieXyz input = new(x, y, yl);
        Hsl expected = new(h, s, l);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<Hsl> actualSpan = new Hsl[5];

        // Act
        Hsl actual = converter.Convert<CieXyz, Hsl>(input);
        converter.Convert<CieXyz, Hsl>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(120, 1, 0.5, 0.38506496, 0.716878653, 0.09710451)]
    public void Convert_Hsl_to_CieXyz(float h, float s, float l, float x, float y, float yl)
    {
        // Arrange
        Hsl input = new(h, s, l);
        CieXyz expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Hsl> inputSpan = new Hsl[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<Hsl, CieXyz>(input);
        converter.Convert<Hsl, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
