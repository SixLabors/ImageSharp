// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests public RGB and XYB conversion with selected transform coefficients.
/// </summary>
public class RgbAndXybConversionTest
{
    private static readonly ApproximateColorProfileComparer ComponentComparer = new(.000001F);

    // The public converter includes the existing RGB/XYZ working-space transforms. Use the same
    // bound as YCbCrTests for that path, while checking XYB's component equations more closely below.
    private static readonly ApproximateColorProfileComparer ConverterComparer = new(.0001F);

    /// <summary>
    /// Verifies that an uninitialized value cannot introduce a zero divisor during transform preparation.
    /// </summary>
    [Fact]
    public void OptionsRejectUninitializedTransform()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ColorConversionOptions { XybTransform = default });

    /// <summary>
    /// Verifies scalar and bulk conversion in both directions with every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void SelectedTransformAppliesInBothDirections()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifySelectedTransform,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the public converter against exact cube values and a non-symmetric channel permutation.
    /// </summary>
    private static void VerifySelectedTransform()
    {
        // These row-major matrices rotate RGB to G/B/R and reverse that rotation. Their asymmetry
        // exposes a missing transpose. Zero bias makes the expected cube roots independent of profile coefficients.
        Matrix4x4 forward = new(0F, 1F, 0F, 0F, 0F, 0F, 1F, 0F, 1F, 0F, 0F, 0F, 0F, 0F, 0F, 1F);
        Matrix4x4 inverse = new(0F, 0F, 1F, 0F, 1F, 0F, 0F, 0F, 0F, 1F, 0F, 0F, 0F, 0F, 0F, 1F);
        GammaWorkingSpace linear = new(1F, KnownRgbWorkingSpaces.SRgb.WhitePoint, KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);
        ReadOnlySpan<XybTransform> transforms =
        [
            new(forward, inverse, Vector3.Zero, 2040F),
            new(inverse, Vector3.Zero, 2040F)
        ];

        // 2040 nits is eight times the ISO reference intensity. Absorbance is multiplied by eight,
        // so its cube root is multiplied by two. All input components are exact cubes of binary fractions.
        ReadOnlySpan<Rgb> samples = [new(1F, .125F, .001953125F), new(.001953125F, 1F, .125F), new(.125F, .001953125F, 1F)];
        ReadOnlySpan<Xyb> expected = [new(.375F, .625F, 2F), new(.5F, 1.5F, .25F), new(-.875F, 1.125F, 1F)];
        Rgb[] source = new Rgb[33];
        Xyb[] encoded = new Xyb[source.Length];
        Rgb[] decoded = new Rgb[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = samples[i % samples.Length];
        }

        foreach (XybTransform transform in transforms)
        {
            ColorConversionOptions options = new() { XybTransform = transform, SourceRgbWorkingSpace = linear, TargetRgbWorkingSpace = linear };
            ColorProfileConverter converter = new(options);

            // Check the public profile contract directly so RGB/XYZ rounding cannot obscure the
            // component arithmetic. Thirty-three colors cross complete SIMD groups and a scalar tail.
            Xyb.FromProfileConnectionSpace(options, source, encoded);
            Xyb.ToProfileConnectionSpace(options, encoded, decoded);

            for (int i = 0; i < source.Length; i++)
            {
                Xyb expectedColor = expected[i % expected.Length];

                Assert.Equal(expectedColor, Xyb.FromProfileConnectingSpace(options, in source[i]), ComponentComparer);
                Assert.Equal(expectedColor, encoded[i], ComponentComparer);
                Assert.Equal(source[i], expectedColor.ToProfileConnectingSpace(options), ComponentComparer);
                Assert.Equal(source[i], decoded[i], ComponentComparer);
            }

            // The generic converter must select those same coefficients. Its complete working-space
            // path also passes through XYZ, which has the separately established conversion bound.
            converter.Convert<Rgb, Xyb>(source, encoded);
            converter.Convert<Xyb, Rgb>(encoded, decoded);

            for (int i = 0; i < source.Length; i++)
            {
                Xyb expectedColor = expected[i % expected.Length];

                Assert.Equal(expectedColor, converter.Convert<Rgb, Xyb>(source[i]), ConverterComparer);
                Assert.Equal(expectedColor, encoded[i], ConverterComparer);
                Assert.Equal(source[i], converter.Convert<Xyb, Rgb>(expectedColor), ConverterComparer);
                Assert.Equal(source[i], decoded[i], ConverterComparer);
            }
        }
    }
}
