// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="CieLuv"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
public class CieXyzAndCieLuvConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.000493, 0.000111, 0, 0.10026589, 0.9332349, -0.00704865158)]
    [InlineData(0.569310, 0.407494, 0.365843, 70.0000, 86.3524, 2.8240)]
    [InlineData(0.012191, 0.011260, 0.025939, 9.9998, -1.2343, -9.9999)]
    [InlineData(0.950470, 1.000000, 1.088830, 100, 0, 0)]
    [InlineData(0.001255, 0.001107, 0.000137, 0.9999, 0.9998, 1.0004)]
    public void Convert_Xyz_To_Luv(float x, float y, float z, float l, float u, float v)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        CieLuv expected = new(l, u, v);

        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<CieXyz, CieLuv>(input);
        converter.Convert<CieXyz, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 100, 50, 0, 0, 0)]
    [InlineData(0.1, 100, 50, 0.000493, 0.000111, -0.000709)]
    [InlineData(70.0000, 86.3525, 2.8240, 0.569310, 0.407494, 0.365843)]
    [InlineData(10.0000, -1.2345, -10.0000, 0.012191, 0.011260, 0.025939)]
    [InlineData(100, 0, 0, 0.950470, 1.000000, 1.088830)]
    [InlineData(1, 1, 1, 0.001255, 0.001107, 0.000137)]
    public void Convert_Luv_To_Xyz(float l, float u, float v, float x, float y, float z)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        CieXyz expected = new(x, y, z);

        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<CieLuv, CieXyz>(input);
        converter.Convert<CieLuv, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
