// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="HunterLab"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
public class CieXyzAndHunterLabConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.360555, 0.936901, 0.1001514, 96.79365, -100.951096, 49.35507)]
    public void Convert_Xyz_To_HunterLab(float x, float y, float z, float l, float a, float b)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        HunterLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<HunterLab> actualSpan = new HunterLab[5];

        // Act
        HunterLab actual = converter.Convert<CieXyz, HunterLab>(input);
        converter.Convert<CieXyz, HunterLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(31.6467056, -33.00599, 25.67032, 0.0385420471, 0.10015139, -0.0317969956)]
    public void Convert_HunterLab_To_Xyz(float l, float a, float b, float x, float y, float z)
    {
        // Arrange
        HunterLab input = new(l, a, b);
        CieXyz expected = new(x, y, z);
        ColorProfileConverter converter = new();

        Span<HunterLab> inputSpan = new HunterLab[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<HunterLab, CieXyz>(input);
        converter.Convert<HunterLab, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
