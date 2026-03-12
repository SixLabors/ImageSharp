// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="CieLchuv"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
public class CieLabAndCieLchuvConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(30.66194, 200, 352.7564, 31.95653, 116.8745, 2.388602)]
    public void Convert_Lchuv_To_Lab(float l, float c, float h, float l2, float a, float b)
    {
        // Arrange
        CieLchuv input = new(l, c, h);
        CieLab expected = new(l2, a, b);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D50 };
        ColorProfileConverter converter = new(options);

        Span<CieLchuv> inputSpan = new CieLchuv[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<CieLchuv, CieLab>(input);
        converter.Convert<CieLchuv, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 303.6901, 10.01514, 29.4713573, 200, 352.6346)]
    public void Convert_Lab_To_Lchuv(float l, float a, float b, float l2, float c, float h)
    {
        // Arrange
        CieLab input = new(l, a, b);
        CieLchuv expected = new(l2, c, h);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<CieLchuv> actualSpan = new CieLchuv[5];

        // Act
        CieLchuv actual = converter.Convert<CieLab, CieLchuv>(input);
        converter.Convert<CieLab, CieLchuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
