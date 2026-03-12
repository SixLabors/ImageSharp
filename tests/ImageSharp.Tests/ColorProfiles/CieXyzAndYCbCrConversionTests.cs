// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="YCbCr"/> conversions.
/// </summary>
public class CieXyzAndYCbCrConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, .5F, .5F)]
    [InlineData(.206382737F, .214041144F, .176628917F, .5F, .5F, .5F)]
    public void Convert_CieXyz_to_YCbCr(float x, float y, float z, float y2, float cb, float cr)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        YCbCr expected = new(y2, cb, cr);
        ColorProfileConverter converter = new();

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<CieXyz, YCbCr>(input);
        converter.Convert<CieXyz, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, .5F, .5F, 0, 0, 0)]
    [InlineData(.5F, .5F, .5F, .206382737F, .214041144F, .176628917F)]
    public void Convert_YCbCr_to_CieXyz(float y2, float cb, float cr, float x, float y, float z)
    {
        // Arrange
        YCbCr input = new(y2, cb, cr);
        CieXyz expected = new(x, y, z);
        ColorProfileConverter converter = new();

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<YCbCr, CieXyz>(input);
        converter.Convert<YCbCr, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
