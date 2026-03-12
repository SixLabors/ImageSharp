// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Cmyk"/> conversions.
/// </summary>
public class CieLabAndCmykConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    [InlineData(0, 1, 0.6156551, 5.960464E-08, 55.063, 82.54871, 23.16506)]
    public void Convert_Cmyk_to_CieLab(float c, float m, float y, float k, float l, float a, float b)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Cmyk, CieLab>(input);
        converter.Convert<Cmyk, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 1)]
    [InlineData(36.0555, 303.6901, 10.01514, 0, 1, 0.597665966, 0)]
    public void Convert_CieLab_to_Cmyk(float l, float a, float b, float c, float m, float y, float k)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<CieLab, Cmyk>(input);
        converter.Convert<CieLab, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
