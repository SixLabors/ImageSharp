// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="Hsl"/> conversions.
/// </summary>
public class CieLuvAndHslConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 93.6901, 10.01514, 347.3767, 0.7115612, 0.3765343)]
    public void Convert_CieLuv_to_Hsl(float l, float u, float v, float h, float s, float l2)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        Hsl expected = new(h, s, l2);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<Hsl> actualSpan = new Hsl[5];

        // Act
        Hsl actual = converter.Convert<CieLuv, Hsl>(input);
        converter.Convert<CieLuv, Hsl>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(347.3767, 0.7115612, 0.3765343, 36.0555, 93.69012, 10.01514)]
    public void Convert_Hsl_to_CieLuv(float h, float s, float l2, float l, float u, float v)
    {
        // Arrange
        Hsl input = new(h, s, l2);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<Hsl> inputSpan = new Hsl[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<Hsl, CieLuv>(input);
        converter.Convert<Hsl, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
