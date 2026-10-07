// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="CieLab"/>-<see cref="Rgb"/> conversions.
/// </summary>
public class CieLabAndRgbConversionTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0002F);
    private static readonly ColorProfileConverter Converter = new();

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0.9999999, 0, 0.384345, 55.063, 82.54871, 23.16505)]
    public void Convert_Rgb_to_CieLab(float r, float g, float b2, float l, float a, float b)
    {
        // Arrange
        Rgb input = new(r, g, b2);
        CieLab expected = new(l, a, b);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<CieLab> actualSpan = new CieLab[5];

        // Act
        CieLab actual = converter.Convert<Rgb, CieLab>(input);
        converter.Convert<Rgb, CieLab>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(55.063, 82.54871, 23.16505, 0.9999999, 0, 0.384345)]
    public void Convert_CieLab_to_Rgb(float l, float a, float b, float r, float g, float b2)
    {
        // Arrange
        CieLab input = new(l, a, b);
        Rgb expected = new(r, g, b2);
        ColorProfileConverter converter = new();

        Span<CieLab> inputSpan = new CieLab[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<CieLab, Rgb>(input);
        converter.Convert<CieLab, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    /// <summary>
    /// Verifies mixed Lab samples through vector groups and a scalar tail with configured white and RGB space.
    /// </summary>
    [Fact]
    public void Convert_CieLab_to_Rgb_BulkMatchesScalarWithCustomOptions() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCieLabBulkWithCustomOptions, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares the mixed Lab row after disabled SIMD features are selected in separate processes before JIT compilation.
    /// </summary>
    private static void RunCieLabBulkWithCustomOptions()
    {
        CieLab[] source = new CieLab[31];
        Rgb[] destination = new Rgb[source.Length];
        ColorConversionOptions options = new() { SourceWhitePoint = KnownIlluminants.D65, TargetRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb };
        ColorProfileConverter converter = new(options);
        ReadOnlySpan<CieLab> samples = [new(0F, 0F, 0F), new(1F, 1F, -1F), new(7.9F, -40F, 35F), new(8.1F, 40F, -35F), new(25F, -128F, 127F), new(50F, 64F, -64F), new(75F, -32F, 96F), new(100F, 0F, 0F), new(12.5F, 12F, 24F), new(87.5F, -73F, -19F), new(33F, 83F, -45F)];

        for (int i = 0; i < source.Length; i++)
        {
            // The eleven-sample period shifts across 16-lane groups; values straddle L*=8 and signed chroma axes.
            source[i] = samples[i % samples.Length];
        }

        converter.Convert<CieLab, Rgb>(source, destination);

        for (int i = 0; i < source.Length; i++)
        {
            Rgb expected = converter.Convert<CieLab, Rgb>(source[i]);
            Assert.Equal(expected, destination[i], Comparer);
        }
    }
}
