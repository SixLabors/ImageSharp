// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests <see cref="Rgb"/>-<see cref="Cmyk"/> conversions.
/// </summary>
/// <remarks>
/// Test data generated using:
/// <see href="http://www.colorhexa.com"/>
/// <see href="http://www.rapidtables.com/convert/color/cmyk-to-rgb.htm"/>
/// </remarks>
[Trait("Color", "Conversion")]
public class RgbAndCmykConversionTest
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Theory]
    [InlineData(1, 1, 1, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 1, 1, 1)]
    [InlineData(0, 0.84, 0.037, 0.365, 0.635, 0.1016, 0.6115)]
    public void Convert_Cmyk_To_Rgb(float c, float m, float y, float k, float r, float g, float b)
    {
        // Arrange
        Cmyk input = new(c, m, y, k);
        Rgb expected = new(r, g, b);
        ColorProfileConverter converter = new();

        Span<Cmyk> inputSpan = new Cmyk[5];
        inputSpan.Fill(input);

        Span<Rgb> actualSpan = new Rgb[5];

        // Act
        Rgb actual = converter.Convert<Cmyk, Rgb>(input);
        converter.Convert<Cmyk, Rgb>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 1)]
    [InlineData(0.635, 0.1016, 0.6115, 0, 0.84, 0.037, 0.365)]
    public void Convert_Rgb_To_Cmyk(float r, float g, float b, float c, float m, float y, float k)
    {
        // Arrange
        Rgb input = new(r, g, b);
        Cmyk expected = new(c, m, y, k);
        ColorProfileConverter converter = new();

        Span<Rgb> inputSpan = new Rgb[5];
        inputSpan.Fill(input);

        Span<Cmyk> actualSpan = new Cmyk[5];

        // Act
        Cmyk actual = converter.Convert<Rgb, Cmyk>(input);
        converter.Convert<Rgb, Cmyk>(inputSpan, actualSpan);

        // Assert
        Assert.Equal(expected, actual, Comparer);

        for (int i = 0; i < actualSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    /// <summary>
    /// Verifies mixed CMYK samples through all RGB and XYZ profile stages with configured working spaces.
    /// </summary>
    [Fact]
    public void Convert_Cmyk_To_Rgb_BulkMatchesScalarWithCustomWorkingSpaces() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        RunCmykBulkWithCustomWorkingSpaces,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares the mixed CMYK row after disabled SIMD features are selected in separate processes before JIT compilation.
    /// </summary>
    private static void RunCmykBulkWithCustomWorkingSpaces()
    {
        Cmyk[] source = new Cmyk[31];
        Rgb[] destination = new Rgb[source.Length];
        ColorConversionOptions options = new()
        {
            SourceRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb,
            TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb
        };

        ColorProfileConverter converter = new(options);
        ReadOnlySpan<Cmyk> samples =
        [
            new(0F, 0F, 0F, 0F),
            new(1F, 1F, 1F, 1F),
            new(.13F, .74F, .21F, .09F),
            new(.91F, .05F, .62F, .33F),
            new(.42F, .88F, .04F, .57F),
            new(.08F, .19F, .93F, .71F),
            new(.67F, .37F, .81F, .12F),
            new(.26F, .53F, .16F, .94F),
            new(.79F, .11F, .48F, .24F),
            new(.34F, .96F, .72F, .46F),
            new(.55F, .28F, .39F, .83F)
        ];

        for (int i = 0; i < source.Length; i++)
        {
            // Eleven unequal colors shift relative to each sixteen-lane group and scalar tail.
            source[i] = samples[i % samples.Length];
        }

        converter.Convert<Cmyk, Rgb>(source, destination);

        for (int i = 0; i < source.Length; i++)
        {
            Rgb expected = converter.Convert<Cmyk, Rgb>(source[i]);
            Assert.Equal(expected, destination[i], Comparer);
        }
    }
}
