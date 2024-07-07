// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLch"/>-<see cref="CieXyy"/> conversions.
/// </summary>
public class CieLchAndCieXyyConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0003f);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(36.0555, 103.6901, 10.01514, 0.67641, 0.22770, 0.09037)]
    public void Convert_CieLch_to_CieXyy(float l, float c, float h, float x, float y, float yl)
    {
        // Arrange
        CieLch input = new(l, c, h);
        CieXyy expected = new(x, y, yl);
        ColorProfileConverter converter = new();

        Span<CieLch> inputSpan = new CieLch[5];
        inputSpan.Fill(input);

        Span<CieXyy> actualSpan = new CieXyy[5];

        // Act
        CieXyy actual = converter.Convert<CieLch, CieXyy>(input);
        converter.Convert<CieLch, CieXyy>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.67641, 0.22770, 0.09037, 36.05544, 103.691315, 10.012783)]
    public void Convert_CieXyy_to_CieLch(float x, float y, float yl, float l, float c, float h)
    {
        // Arrange
        CieXyy input = new(x, y, yl);
        CieLch expected = new(l, c, h);
        ColorProfileConverter converter = new();

        Span<CieXyy> inputSpan = new CieXyy[5];
        inputSpan.Fill(input);

        Span<CieLch> actualSpan = new CieLch[5];

        // Act
        CieLch actual = converter.Convert<CieXyy, CieLch>(input);
        converter.Convert<CieXyy, CieLch>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
