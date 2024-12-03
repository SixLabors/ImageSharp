// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="CieLab"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class CieXyzAndCieLabConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001f);

    [Theory]
    [InlineData(100, 0, 0, 0.95047, 1, 1.08883)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 431.0345, 0, 0.95047, 0, 0)]
    [InlineData(100, -431.0345, 172.4138, 0, 1, 0)]
    [InlineData(0, 0, -172.4138, 0, 0, 1.08883)]
    [InlineData(45.6398, 39.8753, 35.2091, 0.216938, 0.150041, 0.048850)]
    [InlineData(77.1234, -40.1235, 78.1120, 0.358530, 0.517372, 0.076273)]
    [InlineData(10, -400, 20, -0.08712, 0.01126, -0.00192)]
    public void Convert_Lab_to_Xyz(float l, float a, float b, float x, float y, float z)
    {
        // Arrange
        CieLab input = new(l, a, b);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);
        CieXyz expected = new(x, y, z);

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<CieLab, CieXyz>(input);
        converter.Convert<CieLab, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0.95047, 1, 1.08883, 100, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.95047, 0, 0, 0, 431.0345, 0)]
    [InlineData(0, 1, 0, 100, -431.0345, 172.4138)]
    [InlineData(0, 0, 1.08883, 0, 0, -172.4138)]
    [InlineData(0.216938, 0.150041, 0.048850, 45.6398, 39.8753, 35.2091)]
    public void Convert_Xyz_to_Lab(float x, float y, float z, float l, float a, float b)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);
        CieLab expected = new(l, a, b);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<CieXyz, CieLab>(input);
        converter.Convert<CieXyz, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
