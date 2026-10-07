// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieXyz"/>-<see cref="Rgb"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
/// </remarks>
public class RgbAndCieXyzConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    /// <summary>
    /// Verifies mixed RGB/XYZ values through wide groups and tails using the selected working space.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    public void BulkRgbAndXyzConversionSupportsMixedInPlaceValues(int count) => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMixedRgbAndXyzConversion, count, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares aliased RGB/XYZ rows after disabled SIMD features are selected in separate processes before JIT compilation.
    /// </summary>
    /// <param name="serialized">The serialized row length.</param>
    private static void RunMixedRgbAndXyzConversion(string serialized)
    {
        int count = FeatureTestRunner.Deserialize<int>(serialized);
        ColorConversionOptions options = new() { SourceRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb, TargetRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb };
        Rgb[] rgbSource = new Rgb[count];
        CieXyz[] xyzSource = new CieXyz[count];
        CieXyz[] xyzExpected = new CieXyz[count];
        Rgb[] rgbExpected = new Rgb[count];
        ReadOnlySpan<Rgb> rgbSamples = [new(0F, 0F, 0F), new(1F, 1F, 1F), new(.12F, .78F, .31F), new(.87F, .04F, .62F), new(.34F, .56F, .91F), new(.02F, .45F, .73F), new(.68F, .23F, .15F), new(.41F, .93F, .08F), new(.99F, .38F, .52F), new(.27F, .11F, .84F), new(.75F, .65F, .43F)];
        ReadOnlySpan<CieXyz> xyzSamples = [new(0F, 0F, 0F), new(.96F, 1F, .82F), new(.03F, .47F, .19F), new(.72F, .08F, .51F), new(.14F, .26F, .91F), new(.88F, .62F, .05F), new(.35F, .13F, .74F), new(.57F, .95F, .28F), new(.21F, .39F, .65F), new(.79F, .54F, .12F), new(.46F, .71F, .83F)];

        for (int i = 0; i < count; i++)
        {
            // An eleven-sample period distinguishes lanes across 16-wide and remainder groups.
            rgbSource[i] = rgbSamples[i % rgbSamples.Length];
            xyzSource[i] = xyzSamples[i % xyzSamples.Length];
            xyzExpected[i] = rgbSource[i].ToProfileConnectingSpace(options);
            rgbExpected[i] = Rgb.FromProfileConnectingSpace(options, in xyzSource[i]);
        }

        // Both three-float color types alias their input arrays, exposing lane-order and group-boundary mistakes.
        Span<CieXyz> actualXyz = MemoryMarshal.Cast<Rgb, CieXyz>(rgbSource.AsSpan());
        Rgb.ToProfileConnectionSpace(options, rgbSource, actualXyz);
        Span<Rgb> actualRgb = MemoryMarshal.Cast<CieXyz, Rgb>(xyzSource.AsSpan());
        Rgb.FromProfileConnectionSpace(options, xyzSource, actualRgb);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(xyzExpected[i], actualXyz[i], Comparer);
            Assert.Equal(rgbExpected[i], actualRgb[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0.96422, 1.00000, 0.82521, 1, 1, 1)]
    [InlineData(0.00000, 1.00000, 0.00000, 0, 1, 0)]
    [InlineData(0.96422, 0.00000, 0.00000, 1, 0, 0.292064)]
    [InlineData(0.00000, 0.00000, 0.82521, 0, 0.181415, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.297676, 0.267854, 0.045504, 0.720315, 0.509999, 0.168112)]
    public void Convert_XYZ_D50_To_SRGB(float x, float y, float z, float r, float g, float b)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D50, TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb };
        ColorProfileConverter converter = new(options);
        Rgb expected = new(r, g, b);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<CieXyz, Rgb>(input);
        converter.Convert<CieXyz, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0.950470, 1.000000, 1.088830, 1, 1, 1)]
    [InlineData(0, 1.000000, 0, 0, 1, 0)]
    [InlineData(0.950470, 0, 0, 1, 0, 0.254967)]
    [InlineData(0, 0, 1.088830, 0, 0.235458, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.297676, 0.267854, 0.045504, 0.754903, 0.501961, 0.099998)]
    public void Convert_XYZ_D65_To_SRGB(float x, float y, float z, float r, float g, float b)
    {
        // Arrange
        CieXyz input = new(x, y, z);
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb };
        ColorProfileConverter converter = new(options);
        Rgb expected = new(r, g, b);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<CieXyz, Rgb>(input);
        converter.Convert<CieXyz, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 0.964220, 1.000000, 0.825210)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 0, 0, 0.436075, 0.222504, 0.013932)]
    [InlineData(0, 1, 0, 0.385065, 0.716879, 0.0971045)]
    [InlineData(0, 0, 1, 0.143080, 0.060617, 0.714173)]
    [InlineData(0.754902, 0.501961, 0.100000, 0.315757, 0.273323, 0.035506)]
    public void Convert_SRGB_To_XYZ_D50(float r, float g, float b, float x, float y, float z)
    {
        // Arrange
        Rgb input = new(r, g, b);
        ColorConversionOptions options = new() { TargetWhitePoint = KnownIlluminants.D50, SourceRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb };
        ColorProfileConverter converter = new(options);
        CieXyz expected = new(x, y, z);

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<Rgb, CieXyz>(input);
        converter.Convert<Rgb, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 0.950470, 1.000000, 1.088830)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 0, 0, 0.412456, 0.212673, 0.019334)]
    [InlineData(0, 1, 0, 0.357576, 0.715152, 0.119192)]
    [InlineData(0, 0, 1, 0.1804375, 0.072175, 0.950304)]
    [InlineData(0.754902, 0.501961, 0.100000, 0.297676, 0.267854, 0.045504)]
    public void Convert_SRGB_To_XYZ_D65(float r, float g, float b, float x, float y, float z)
    {
        // Arrange
        Rgb input = new(r, g, b);
        ColorConversionOptions options = new() { TargetWhitePoint = KnownIlluminants.D65, SourceRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb };
        ColorProfileConverter converter = new(options);
        CieXyz expected = new(x, y, z);

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        // Act
        CieXyz actual = converter.Convert<Rgb, CieXyz>(input);
        converter.Convert<Rgb, CieXyz>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }
}
