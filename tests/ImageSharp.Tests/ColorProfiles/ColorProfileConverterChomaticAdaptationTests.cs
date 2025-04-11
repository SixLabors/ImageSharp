// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests chromatic adaptation within the <see cref="ColorProfileConverter"/>.
/// Test data generated using:
/// <see cref="http://www.brucelindbloom.com/index.html?ChromAdaptCalc.html"/>
/// <see cref="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </summary>
public class ColorProfileConverterChomaticAdaptationTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1, 1, 1)]
    [InlineData(0.206162, 0.260277, 0.746717, 0.220000, 0.130000, 0.780000)]
    public void Adapt_RGB_WideGamutRGB_To_sRGB(float r1, float g1, float b1, float r2, float g2, float b2)
    {
        // Arrange
        Rgb input = new(r1, g1, b1);
        Rgb expected = new(r2, g2, b2);
        ColorConversionOptions options = new()
        {
            SourceRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb,
            TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb
        };
        ColorProfileConverter converter = new(options);

        // Action
        Rgb actual = converter.Convert<Rgb, Rgb>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1, 1, 1)]
    [InlineData(0.220000, 0.130000, 0.780000, 0.206162, 0.260277, 0.746717)]
    public void Adapt_RGB_SRGB_To_WideGamutRGB(float r1, float g1, float b1, float r2, float g2, float b2)
    {
        // Arrange
        Rgb input = new(r1, g1, b1);
        Rgb expected = new(r2, g2, b2);
        ColorConversionOptions options = new()
        {
            SourceRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb,
            TargetRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb
        };
        ColorProfileConverter converter = new(options);

        // Action
        Rgb actual = converter.Convert<Rgb, Rgb>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(22, 33, 1, 22.269869, 32.841164, 1.633926)]
    public void Adapt_Lab_D65_To_D50(float l1, float a1, float b1, float l2, float a2, float b2)
    {
        // Arrange
        CieLab input = new(l1, a1, b1);
        CieLab expected = new(l2, a2, b2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50
        };
        ColorProfileConverter converter = new(options);

        // Action
        CieLab actual = converter.Convert<CieLab, CieLab>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.5, 0.5, 0.5, 0.510286, 0.501489, 0.378970)]
    public void Adapt_Xyz_D65_To_D50_Bradford(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        // Arrange
        CieXyz input = new(x1, y1, z1);
        CieXyz expected = new(x2, y2, z2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50,
            AdaptationMatrix = KnownChromaticAdaptationMatrices.Bradford
        };

        ColorProfileConverter converter = new(options);

        // Action
        CieXyz actual = converter.Convert<CieXyz, CieXyz>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
    public void Adapt_Xyz_D65_To_D50_XyzScaling(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        // Arrange
        CieXyz input = new(x1, y1, z1);
        CieXyz expected = new(x2, y2, z2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50,
            AdaptationMatrix = KnownChromaticAdaptationMatrices.XyzScaling
        };

        ColorProfileConverter converter = new(options);

        // Action
        CieXyz actual = converter.Convert<CieXyz, CieXyz>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(22, 33, 1, 22.28086, 33.0681534, 1.30099022)]
    public void Adapt_HunterLab_D65_To_D50(float l1, float a1, float b1, float l2, float a2, float b2)
    {
        // Arrange
        HunterLab input = new(l1, a1, b1);
        HunterLab expected = new(l2, a2, b2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50,
        };

        ColorProfileConverter converter = new(options);

        // Action
        HunterLab actual = converter.Convert<HunterLab, HunterLab>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(22, 33, 1, 22, 34.0843468, 359.009)]
    public void Adapt_CieLchuv_D65_To_D50_XyzScaling(float l1, float c1, float h1, float l2, float c2, float h2)
    {
        // Arrange
        CieLchuv input = new(l1, c1, h1);
        CieLchuv expected = new(l2, c2, h2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50,
            AdaptationMatrix = KnownChromaticAdaptationMatrices.XyzScaling
        };

        ColorProfileConverter converter = new(options);

        // Action
        CieLchuv actual = converter.Convert<CieLchuv, CieLchuv>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }

    [Theory]
    [InlineData(22, 33, 1, 22, 33, 0.9999999)]
    public void Adapt_CieLch_D65_To_D50_XyzScaling(float l1, float c1, float h1, float l2, float c2, float h2)
    {
        // Arrange
        CieLch input = new(l1, c1, h1);
        CieLch expected = new(l2, c2, h2);
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = KnownIlluminants.D65,
            TargetWhitePoint = KnownIlluminants.D50,
            AdaptationMatrix = KnownChromaticAdaptationMatrices.XyzScaling
        };

        ColorProfileConverter converter = new(options);

        // Action
        CieLch actual = converter.Convert<CieLch, CieLch>(input);

        // Assert
        Assert.Equal(expected, actual, Comparer);
    }
}
