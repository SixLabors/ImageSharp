// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="Lms"/> conversions.
/// </summary>
public class CieXyyAndLmsConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 0.06631134, 0.1415282, -0.03809926)]
    public void Convert_CieXyy_to_Lms(float x, float y, float yl, float l, float m, float s)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        Lms expected = new(l, m, s);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<Lms> actualSpan = new Lms[5];

        // Act
        Lms actual = converter.Convert<CieXyy, Lms>(input);
        converter.Convert<CieXyy, Lms>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.06631134, 0.1415282, -0.03809926, 0.360555, 0.936901, 0.1001514)]
    public void Convert_Lms_to_CieXyy(float l, float m, float s, float x, float y, float yl)
    {
        // Arrange
        Lms input = new(l, m, s);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<Lms> inputSpan = new Lms[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<Lms, CieXyy>(input);
        converter.Convert<Lms, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
