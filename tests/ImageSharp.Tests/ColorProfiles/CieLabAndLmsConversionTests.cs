// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Lms"/> conversions.
/// </summary>
public class CieLabAndLmsConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.8303261, -0.5776886, 0.1133359, 30.66193, 291.57209, -11.25262)]
    public void Convert_Lms_to_CieLab(float l2, float m, float s, float l, float a, float b)
    {
        // Arrange
        Lms input = new(l2, m, s);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Lms> inputSpan = new Lms[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Lms, CieLab>(input);
        converter.Convert<Lms, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(30.66193, 291.57209, -11.25262, 0.8303261, -0.5776886, 0.1133359)]
    public void Convert_CieLab_to_Lms(float l, float a, float b, float l2, float m, float s)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Lms expected = new(l2, m, s);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Lms> actualSpan = new Lms[5];

        // Act
        Lms actual = converter.Convert<CieLab, Lms>(input);
        converter.Convert<CieLab, Lms>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
