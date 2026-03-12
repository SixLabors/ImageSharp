// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLuv"/>-<see cref="Lms"/> conversions.
/// </summary>
public class CieLuvAndLmsConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 93.6901, 10.01514, 0.164352, 0.03267485, 0.0483408)]
    public void Convert_CieLuv_to_Lms(float l, float u, float v, float l2, float m, float s)
    {
        // Arrange
        CieLuv input = new(l, u, v);
        Lms expected = new(l2, m, s);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<CieLuv> inputSpan = new CieLuv[5];
        inputSpan.Fill(input);

        Span<Lms> actualSpan = new Lms[5];

        // Act
        Lms actual = converter.Convert<CieLuv, Lms>(input);
        converter.Convert<CieLuv, Lms>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.164352, 0.03267485, 0.0483408, 36.0555, 93.69009, 10.01514)]
    public void Convert_Lms_to_CieLuv(float l2, float m, float s, float l, float u, float v)
    {
        // Arrange
        Lms input = new(l2, m, s);
        CieLuv expected = new(l, u, v);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetWhitePoint = KnownIlluminants.D65 };
        ColorProfileConverter converter = new(options);

        Span<Lms> inputSpan = new Lms[5];
        inputSpan.Fill(input);

        Span<CieLuv> actualSpan = new CieLuv[5];

        // Act
        CieLuv actual = converter.Convert<Lms, CieLuv>(input);
        converter.Convert<Lms, CieLuv>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
