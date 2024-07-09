// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="CieXyy"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class CieXyzAndCieXyyConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
    [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
    [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
    [InlineData(0, 0, 0, 0.538842, 0.000000, 0.000000)]
    public void Convert_Xyy_To_Xyz(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
    {
        CieXyy input = new(x, y, yl);
        CieXyz expected = new(xyzX, xyzY, xyzZ);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<CieXyy, CieXyz>(input);
        converter.Convert<CieXyy, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
    [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
    [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
    [InlineData(0.231809, 0, 0.077528, 0.749374, 0.000000, 0.000000)]
    public void Convert_Xyz_to_Xyy(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
    {
        CieXyz input = new(xyzX, xyzY, xyzZ);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<CieXyz, CieXyy>(input);
        converter.Convert<CieXyz, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
