// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="CieLch"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class CieLabAndCieLchConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(54.2917, 106.8391, 40.8526, 54.2917, 80.8125, 69.8851)]
    [InlineData(100, 0, 0, 100, 0, 0)]
    [InlineData(100, 50, 180, 100, -50, 0)]
    [InlineData(10, 36.0555, 56.3099, 10, 20, 30)]
    [InlineData(10, 36.0555, 123.6901, 10, -20, 30)]
    [InlineData(10, 36.0555, 303.6901, 10, 20, -30)]
    [InlineData(10, 36.0555, 236.3099, 10, -20, -30)]
    public void Convert_Lch_to_Lab(float l, float c, float h, float l2, float a, float b)
    {
        // Arrange
        CieLch input = new(l, c, h);
        CieLab expected = new(l2, a, b);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<CieLch, CieLab>(input);
        converter.Convert<CieLch, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(54.2917, 80.8125, 69.8851, 54.2917, 106.8391, 40.8526)]
    [InlineData(100, 0, 0, 100, 0, 0)]
    [InlineData(100, -50, 0, 100, 50, 180)]
    [InlineData(10, 20, 30, 10, 36.0555, 56.3099)]
    [InlineData(10, -20, 30, 10, 36.0555, 123.6901)]
    [InlineData(10, 20, -30, 10, 36.0555, 303.6901)]
    [InlineData(10, -20, -30, 10, 36.0555, 236.3099)]
    public void Convert_Lab_to_Lch(float l, float a, float b, float l2, float c, float h)
    {
        // Arrange
        CieLab input = new(l, a, b);
        CieLch expected = new(l2, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<CieLab, CieLch>(input);
        converter.Convert<CieLab, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
