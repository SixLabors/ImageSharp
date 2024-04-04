// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Conversion;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="CieXyy"/> conversions.
/// </summary>
public class CieLabAndCieXyyConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.8644734, 0.06098868, 0.06509002, 30.6619, 291.5721, -11.2526)]
    public void Convert_CieXyy_To_CieLab(float x, float y, float yl, float l, float a, float b)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<CieXyy, CieLab>(input);
        converter.Convert<CieXyy, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(30.6619, 291.5721, -11.2526, 0.8644734, 0.06098868, 0.06509002)]
    public void Convert_CieLab_To_CieXyy(float l, float a, float b, float x, float y, float yl)
    {
        // Arrange
        CieLab input = new(l, a, b);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<CieLab, CieXyy>(input);
        converter.Convert<CieLab, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
