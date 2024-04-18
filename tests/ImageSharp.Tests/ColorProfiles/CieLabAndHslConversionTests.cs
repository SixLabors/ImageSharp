// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Hsl"/> conversions.
/// </summary>
public class CieLabAndHslConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(336.9393, 1, 0.5, 55.063, 82.54868, 23.16508)]
    public void Convert_Hsl_to_CieLab(float h, float s, float ll, float l, float a, float b)
    {
        // Arrange
        Hsl input = new(h, s, ll);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Hsl> inputSpan = new Hsl[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Hsl, CieLab>(input);
        converter.Convert<Hsl, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(55.063, 82.54868, 23.16508, 336.9393, 1, 0.5)]
    public void Convert_CieLab_to_Hsl(float l, float a, float b, float h, float s, float ll)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Hsl expected = new(h, s, ll);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Hsl> actualSpan = new Hsl[5];

        // Act
        Hsl actual = converter.Convert<CieLab, Hsl>(input);
        converter.Convert<CieLab, Hsl>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
