// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyy"/>-<see cref="YCbCr"/> conversions.
/// </summary>
public class CieXyyAndYCbCrConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002f);

    [Theory]
    [InlineData(0, 0, 0, 0, 128, 128)]
    [InlineData(0.360555, 0.936901, 0.1001514, 64.0204849, 91.87107, 82.33627)]
    public void Convert_CieXyy_to_YCbCr(float x, float y, float yl, float y2, float cb, float cr)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        YCbCr expected = new(y2, cb, cr);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<YCbCr> actualSpan = new YCbCr[5];

        // Act
        YCbCr actual = converter.Convert<CieXyy, YCbCr>(input);
        converter.Convert<CieXyy, YCbCr>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 128, 128, 0, 0, 0)]
    [InlineData(64.0204849, 91.87107, 82.33627, 0.32114, 0.59787, 0.10976)]
    public void Convert_YCbCr_to_CieXyy(float y2, float cb, float cr, float x, float y, float yl)
    {
        // Arrange
        YCbCr input = new(y2, cb, cr);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<YCbCr> inputSpan = new YCbCr[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<YCbCr, CieXyy>(input);
        converter.Convert<YCbCr, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
