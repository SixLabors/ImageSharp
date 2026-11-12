// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
    /// Rejects transforms whose intensity cannot produce finite normalization factors.
    /// </summary>
    [Fact]
    public void RejectsUnusableTransformNormalization()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorConversionOptions { XybTransform = default });

        // A positive finite intensity is still invalid when 255/intensity overflows before matrix preparation.
        Assert.Throws<ArgumentOutOfRangeException>(() => new XybTransform(Matrix4x4.Identity, Vector3.Zero, float.Epsilon));
    }

    /// <summary>
    /// Verifies scalar and bulk conversion in both directions with every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void SelectedTransformAppliesInBothDirections()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifySelectedTransform,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks native linear-sRGB forward values at the standard and HDR intensity targets.
    /// </summary>
    [Fact]
    public void LinearForwardMatchesNative()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyNativeLinearForward,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks native inverse values with ISO coefficients and custom coefficients, biases and intensity.
    /// </summary>
    [Fact]
    public void LinearInverseMatchesNative()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyNativeLinearInverse,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks public conversion between XYB and the native Display P3 and ProPhoto encodings.
    /// </summary>
    [Fact]
    public void WorkingSpaceConversionsMatchNative()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyNativeWorkingSpaces,
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

        // These primaries and white produce exactly representable RGB/XYZ matrices. They keep RGB/XYZ
        // rounding out of the binary cube checks, so a failure identifies the selected XYB transform.
        CieXyz white = new(.25F, 1F, .25F);
        RgbPrimariesChromaticityCoordinates primaries = new(new(.5F, .5F), new(0F, 1F), new(0F, .5F));
        GammaWorkingSpace linear = new(1F, white, primaries);
        SRgbWorkingSpace nonlinearWorkingSpace = new(white, primaries);
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
            ColorProfileConverter nonlinear = new(new ColorConversionOptions
            {
                XybTransform = transform,
                SourceRgbWorkingSpace = nonlinearWorkingSpace,
                TargetRgbWorkingSpace = nonlinearWorkingSpace
            });

            // RGB is a genuine linear endpoint. The converter owns RGB-to-XYZ and XYZ-to-RGB conversion;
            // XYB owns only its transform to and from the XYZ connection space. No RGB transfer runs inside XYB.
            converter.Convert(source, encoded);
            converter.Convert(encoded, decoded);

            // Thirty-three colors cross complete SIMD groups and a scalar tail. The binary cube expectations
            // remain independent of the implementation, including the selected transform and intensity scale.
            for (int i = 0; i < source.Length; i++)
            {
                Xyb expectedColor = expected[i % expected.Length];

                Assert.Equal(expectedColor, converter.Convert<Rgb, Xyb>(source[i]), ComponentComparer);
                Assert.Equal(expectedColor, encoded[i], ComponentComparer);
                Assert.Equal(source[i], converter.Convert<Xyb, Rgb>(expectedColor), ComponentComparer);
                Assert.Equal(source[i], decoded[i], ComponentComparer);

                // XYZ and XYB use linear matrix coordinates. The two converters have identical primaries
                // and white points, so changing only their RGB transfer curves must not change either result.
                CieXyz xyz = converter.Convert<Xyb, CieXyz>(expectedColor);
                Assert.Equal(xyz, nonlinear.Convert<Xyb, CieXyz>(expectedColor));
                Assert.Equal(converter.Convert<CieXyz, Xyb>(xyz), nonlinear.Convert<CieXyz, Xyb>(xyz));
            }
        }
    }

    /// <summary>
    /// Checks scalar and bulk forward conversion from a linear RGB endpoint through the public converter.
    /// </summary>
    /// <remarks>
    /// Expected values come from libjxl v0.13.0, revision 91837342b24034078780b687f57f670a7def8153,
    /// lib/jxl/enc_xyb.cc, ToXYB with a linear-sRGB encoding. The numeric source includes black, fractional,
    /// HDR and negative components. Decimal literals retain the recorded single-precision values.
    /// </remarks>
    private static void VerifyNativeLinearForward()
    {
        ReadOnlySpan<Rgb> samples =
        [
            new(0F, 0F, 0F),
            new(1F, 0F, 0F),
            new(.12F, .75F, .33F),
            new(1.5F, 0.25F, 0.125F),
            new(-.025F, .4F, .8F)
        ];

        ReadOnlySpan<Xyb> expected255 =
        [
            new(0F, -1.23873107e-08F, -1.23873107e-08F),
            new(0.0281000584F, 0.488188207F, 0.471658945F),
            new(-0.0108984411F, 0.665244937F, 0.561094284F),
            new(0.0211055875F, 0.675203204F, 0.631933689F),
            new(-0.0105505288F, 0.52955687F, 0.648738146F)
        ];

        ReadOnlySpan<Xyb> expected1000 =
        [
            new(0F, -1.23873107e-08F, -1.23873107e-08F),
            new(0.0446289778F, 0.856217325F, 0.82997936F),
            new(-0.0172451138F, 1.13683414F, 0.971904337F),
            new(0.0333922505F, 1.15258765F, 1.08411407F),
            new(-0.0167357326F, 0.921895087F, 1.11071324F)
        ];

        GammaWorkingSpace linear = CreateNativeLinearSrgbWorkingSpace();
        XybTransform iso = KnownXybTransforms.Iso18181;
        Rgb[] source = new Rgb[33];
        Xyb[] destination = new Xyb[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = samples[i % samples.Length];
        }

        foreach (float intensity in new[] { 255F, 1000F })
        {
            ColorConversionOptions options = new()
            {
                XybTransform = new(iso.Forward, iso.Inverse, iso.Biases, intensity),
                SourceRgbWorkingSpace = linear,
                TargetRgbWorkingSpace = linear
            };

            ColorProfileConverter converter = new(options);
            ReadOnlySpan<Xyb> expected = intensity == 255F ? expected255 : expected1000;
            converter.Convert(source, destination);

            // Thirty-three samples cover complete SIMD groups and their scalar remainder.
            for (int i = 0; i < source.Length; i++)
            {
                Xyb expectedColor = expected[i % expected.Length];

                Assert.Equal(expectedColor, converter.Convert<Rgb, Xyb>(source[i]), ComponentComparer);
                Assert.Equal(expectedColor, destination[i], ComponentComparer);
            }
        }
    }

    /// <summary>
    /// Checks native inverse output before its transfer function, including negative and HDR RGB values.
    /// </summary>
    /// <remarks>
    /// Expected values come from the same libjxl revision as the forward test: OutputEncodingInfo.SetFromMetadata
    /// in lib/jxl/dec_xyb.cc and GetXYBStage in lib/jxl/render_pipeline/stage_xyb.cc. The custom row-major inverse
    /// changes coefficients [0,2], [1,0] and [2,1] and supplies three distinct nonzero biases at 1000 nits.
    /// These are recorded native linear outputs, not results of an ImageSharp round trip.
    /// </remarks>
    private static void VerifyNativeLinearInverse()
    {
        ReadOnlySpan<Xyb> isoInput255 =
        [
            new(0F, -1.23873107e-08F, -1.23873107e-08F),
            new(-0.0117952526F, 0.55363369F, 0.390473425F),
            new(0.0365243256F, 0.734931588F, 0.705681145F),
            new(-0.00589986145F, 0.360335231F, 0.557870984F)
        ];

        ReadOnlySpan<Rgb> isoExpected255 =
        [
            new(-4.90771979e-10F, -4.90772312e-10F, -4.90772256e-10F),
            new(0.0134093408F, 0.522522271F, 0.0889821425F),
            new(2.53699708F, 0.0508750156F, 0.0143490797F),
            new(-0.00193478214F, 0.132868186F, 0.603827357F)
        ];

        ReadOnlySpan<Rgb> isoUnfused255 =
        [
            new(-4.65660954e-10F, -4.65661287e-10F, -4.65661232e-10F),
            new(.0134090427F, .52252245F, .0889821947F),
            new(2.53699732F, .050875105F, .014349103F),
            new(-.00193473324F, .132868171F, .603827417F)
        ];

        ReadOnlySpan<Xyb> isoInput1000 =
        [
            new(0F, -1.23873107e-08F, -1.23873107e-08F),
            new(-0.018699348F, 0.960075319F, 0.700735092F),
            new(0.0577517152F, 1.247051F, 1.20080388F),
            new(-0.00943350792F, 0.652598679F, 0.966794968F)
        ];

        ReadOnlySpan<Rgb> isoExpected1000 =
        [
            new(-1.25146699e-10F, -1.25146934e-10F, -1.25146921e-10F),
            new(0.0134108933F, 0.522521615F, 0.088981688F),
            new(2.53699231F, 0.0508760251F, 0.0143495537F),
            new(-0.00193583092F, 0.132868484F, 0.603827715F)
        ];

        ReadOnlySpan<Rgb> isoUnfused1000 =
        [
            new(-1.18743446e-10F, -1.18743612e-10F, -1.18743612e-10F),
            new(.0134109315F, .522521615F, .088981688F),
            new(2.5369935F, .0508758947F, .0143493414F),
            new(-.00193568319F, .132868439F, .603827596F)
        ];

        ReadOnlySpan<Xyb> customInput =
        [
            new(0F, 0F, 0F),
            new(.01F, .4F, .3F),
            new(-.03F, .2F, .5F),
            new(.1F, .75F, .6F),
            new(-.08F, 1.25F, 1.5F),
            new(.025F, -.15F, .2F)
        ];

        ReadOnlySpan<Rgb> customExpected =
        [
            new(-3.22573163e-10F, 6.44684696e-12F, 3.21785876e-10F),
            new(0.167930841F, -0.00696920045F, -0.0196079407F),
            new(-0.0130267711F, 0.00836760085F, 0.155328035F),
            new(1.71580946F, -0.376563728F, -0.390608639F),
            new(-1.06955564F, 1.25822961F, 2.35506964F),
            new(-0.00473563327F, -0.00105543481F, 0.0268179663F)
        ];

        ReadOnlySpan<Rgb> customUnfused =
        [
            new(-7.1397177e-10F, 1.1935387e-10F, 5.0074994e-10F),
            new(.16793093F, -.00696922F, -.019607946F),
            new(-.013026767F, .008367599F, .15532804F),
            new(1.7158095F, -.3765637F, -.39060864F),
            new(-1.0695548F, 1.2582294F, 2.3550694F),
            new(-.0047356333F, -.0010554346F, .026817964F)
        ];

        // Apply the same three coefficient changes as the native case. Standard coefficients remain owned by the ISO transform.
        XybTransform iso = KnownXybTransforms.Iso18181;
        Matrix4x4 customInverse = iso.Inverse;
        customInverse.M13 += .25F;
        customInverse.M21 -= .125F;
        customInverse.M32 += .0625F;

        VerifyNativeInverse(iso, isoInput255, isoExpected255, isoUnfused255);
        VerifyNativeInverse(new(iso.Inverse, iso.Biases, 1000F), isoInput1000, isoExpected1000, isoUnfused1000);
        VerifyNativeInverse(new(customInverse, new(-.004F, -.002F, -.006F), 1000F), customInput, customExpected, customUnfused);
    }

    /// <summary>
    /// Checks direct inverse kernels and public XYZ conversions against native linear RGB values.
    /// </summary>
    /// <param name="transform">The transform used by the native decoder.</param>
    /// <param name="samples">The native XYB input period.</param>
    /// <param name="expected">The recorded fused linear-RGB output period.</param>
    /// <param name="unfused">The recorded unfused linear-RGB output period for the same input bits.</param>
    private static void VerifyNativeInverse(XybTransform transform, ReadOnlySpan<Xyb> samples, ReadOnlySpan<Rgb> expected, ReadOnlySpan<Rgb> unfused)
    {
        Xyb.InverseOperator inverse = new(transform, KnownRgbWorkingSpaces.SRgb, false);
        GammaWorkingSpace linear = CreateNativeLinearSrgbWorkingSpace();
        ColorConversionOptions options = new() { XybTransform = transform, SourceRgbWorkingSpace = linear, TargetRgbWorkingSpace = linear };
        ColorProfileConverter converter = new(options);
        Xyb[] source = new Xyb[33];
        Rgb[] destination = new Rgb[source.Length + 1];
        Rgb sentinel = new(-1F, -2F, -3F);
        destination[^1] = sentinel;

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = samples[i % samples.Length];
        }

        // The converter receives the XYZ connection values and produces the requested linear RGB endpoint.
        // Its output transfer belongs to that endpoint, so the native linear samples need no encode/expand round trip.
        converter.Convert(source, destination);

        for (int i = 0; i < source.Length; i++)
        {
            Rgb expectedColor = expected[i % expected.Length];
            Rgb unfusedColor = unfused[i % unfused.Length];
            VerifyNativeInverseKernel(in inverse, source[i], expectedColor, unfusedColor);

            // Native values describe the direct linear-RGB kernel checked above. The public converter also
            // applies RGB-to-XYZ and XYZ-to-RGB matrices. Use its existing conversion bound for that complete path.
            Rgb actualScalar = converter.Convert<Xyb, Rgb>(source[i]);
            Assert.True(ConverterComparer.Equals(expectedColor, actualScalar) || ConverterComparer.Equals(unfusedColor, actualScalar),
                $"Intensity {transform.IntensityTarget:R}; input {source[i]}; "
                + $"fused {expectedColor.ToScaledVector4()}; unfused {unfusedColor.ToScaledVector4()}; scalar {actualScalar.ToScaledVector4()}.");
            Assert.True(ConverterComparer.Equals(expectedColor, destination[i]) || ConverterComparer.Equals(unfusedColor, destination[i]),
                $"Intensity {transform.IntensityTarget:R}; input {source[i]}; "
                + $"fused {expectedColor.ToScaledVector4()}; unfused {unfusedColor.ToScaledVector4()}; bulk {destination[i].ToScaledVector4()}.");
        }

        Assert.Equal(sentinel, destination[^1]);

        // Both models occupy three floats. The public converter consumes the XYB source into its XYZ workspace
        // before it publishes RGB. Check exact overlap only after all separate-source checks have consumed the inputs.
        Span<Rgb> inPlace = MemoryMarshal.Cast<Xyb, Rgb>(source.AsSpan());
        converter.Convert(source, inPlace);

        for (int i = 0; i < inPlace.Length; i++)
        {
            Assert.True(
                ConverterComparer.Equals(expected[i % expected.Length], inPlace[i]) || ConverterComparer.Equals(unfused[i % unfused.Length], inPlace[i]));
        }
    }

    /// <summary>
    /// Checks the direct inverse equations against the recorded native values at the existing component accuracy bound.
    /// </summary>
    /// <param name="operation">The intensity-scaled inverse that produces linear sRGB directly.</param>
    /// <param name="sample">One recorded native XYB input.</param>
    /// <param name="expected">The recorded fused native output.</param>
    /// <param name="unfused">The recorded unfused native output.</param>
    private static void VerifyNativeInverseKernel(in Xyb.InverseOperator operation, Xyb sample, Rgb expected, Rgb unfused)
    {
        // Native XybToRgb applies the inverse absorbance matrix directly. It does not pass through XYZ.
        // Keep the strict component bound here for scalar arithmetic and every available SIMD width.
        operation.Invoke(sample.X, sample.Y, sample.B, out float r, out float g, out float b);
        Rgb scalar = new(r, g, b);
        Assert.True(ComponentComparer.Equals(expected, scalar) || ComponentComparer.Equals(unfused, scalar),
            $"Input {sample}; fused {expected}; unfused {unfused}; direct scalar {scalar}.");

        // Each register repeats one component across independent pixels. Check every output lane;
        // the surrounding FeatureTestRunner also selects narrower workers and the scalar fallback.
        if (Vector512.IsHardwareAccelerated)
        {
            operation.Invoke(
                Vector512.Create(sample.X), Vector512.Create(sample.Y), Vector512.Create(sample.B),
                out Vector512<float> red, out Vector512<float> green, out Vector512<float> blue);

            for (int lane = 0; lane < Vector512<float>.Count; lane++)
            {
                Rgb actual = new(red.GetElement(lane), green.GetElement(lane), blue.GetElement(lane));
                Assert.True(ComponentComparer.Equals(expected, actual) || ComponentComparer.Equals(unfused, actual),
                    $"Input {sample}; fused {expected}; unfused {unfused}; direct Vector512 lane {lane}: {actual}.");
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            operation.Invoke(
                Vector256.Create(sample.X), Vector256.Create(sample.Y), Vector256.Create(sample.B),
                out Vector256<float> red, out Vector256<float> green, out Vector256<float> blue);

            for (int lane = 0; lane < Vector256<float>.Count; lane++)
            {
                Rgb actual = new(red.GetElement(lane), green.GetElement(lane), blue.GetElement(lane));
                Assert.True(ComponentComparer.Equals(expected, actual) || ComponentComparer.Equals(unfused, actual),
                    $"Input {sample}; fused {expected}; unfused {unfused}; direct Vector256 lane {lane}: {actual}.");
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            operation.Invoke(
                Vector128.Create(sample.X), Vector128.Create(sample.Y), Vector128.Create(sample.B),
                out Vector128<float> red, out Vector128<float> green, out Vector128<float> blue);

            for (int lane = 0; lane < Vector128<float>.Count; lane++)
            {
                Rgb actual = new(red.GetElement(lane), green.GetElement(lane), blue.GetElement(lane));
                Assert.True(ComponentComparer.Equals(expected, actual) || ComponentComparer.Equals(unfused, actual),
                    $"Input {sample}; fused {expected}; unfused {unfused}; direct Vector128 lane {lane}: {actual}.");
            }
        }
    }

    /// <summary>
    /// Checks both public conversion directions and the prepared inverse with independently recorded native endpoint values.
    /// </summary>
    /// <remarks>
    /// Expected values come from libjxl revision 91837342b24034078780b687f57f670a7def8153.
    /// Forward conversion calls ToXYB in lib/jxl/enc_xyb.cc with JxlGetDefaultCms for each linear input profile.
    /// Inverse conversion calls OutputEncodingInfo.MaybeSetColorEncoding in lib/jxl/dec_xyb.cc and GetXYBStage
    /// in render_pipeline/stage_xyb.cc. All cases use an intensity target of 1000 nits and the printed native xy coordinates.
    /// Both working spaces have unit gamma. No RGB transfer function participates in these comparisons.
    /// The forward CMS path includes native ICC tag quantization; the existing converter bound applies.
    /// </remarks>
    private static void VerifyNativeWorkingSpaces()
    {
        ReadOnlySpan<Rgb> rgbInput =
        [
            new(.12F, .75F, .33F),
            new(.9F, .2F, .65F),
            new(.03F, .15F, .7F)
        ];

        ReadOnlySpan<Xyb> xybInput =
        [
            new(-0.0172451138F, 1.13683414F, 0.971904337F),
            new(0.0229206085F, 1.02603185F, 1.18848717F),
            new(-0.00742799044F, 0.703756988F, 1.02991104F)
        ];

        ReadOnlySpan<Xyb> forwardP3 =
        [
            new(-0.022467196F, 1.1189065F, 0.923599541F),
            new(0.0279655457F, 1.0485338F, 1.2231133F),
            new(-0.00939252973F, 0.70471549F, 1.05322635F)
        ];

        ReadOnlySpan<Xyb> forwardProPhoto =
        [
            new(-0.0378002524F, 1.09732938F, 0.815573096F),
            new(0.043777585F, 1.07373011F, 1.28700686F),
            new(-0.0356215537F, 0.594427705F, 1.01453936F)
        ];

        ReadOnlySpan<Rgb> inverseP3 =
        [
            new(0.231857881F, 0.729083002F, 0.356816739F),
            new(0.775714517F, 0.22324039F, 0.621694088F),
            new(0.05131175F, 0.146010891F, 0.648744881F)
        ];

        ReadOnlySpan<Rgb> inverseProPhoto =
        [
            new(0.35750252F, 0.676198006F, 0.375869095F),
            new(0.633765757F, 0.281532109F, 0.601275563F),
            new(0.163816035F, 0.153686062F, 0.623989344F)
        ];

        ColorConversionOptions whiteOptions = new();
        CieXyz d65 = new CieXyy(.3127F, .329F, 1F).ToProfileConnectingSpace(whiteOptions);
        CieXyz d50 = new CieXyy(.345669F, .358496F, 1F).ToProfileConnectingSpace(whiteOptions);
        RgbWorkingSpace[] endpoints =
        [
            new GammaWorkingSpace(1F, d65, new(new(.68F, .32F), new(.265F, .69F), new(.15F, .06F))),
            new GammaWorkingSpace(1F, d50, new(new(.7347F, .2653F), new(.1596F, .8404F), new(.0366F, .0001F)))
        ];

        GammaWorkingSpace srgb = CreateNativeLinearSrgbWorkingSpace();
        XybTransform iso = KnownXybTransforms.Iso18181;
        XybTransform transform = new(iso.Forward, iso.Inverse, iso.Biases, 1000F);
        Rgb[] rgbSource = new Rgb[33];
        Xyb[] xybSource = new Xyb[rgbSource.Length];
        Xyb[] encoded = new Xyb[rgbSource.Length];
        Rgb[] decoded = new Rgb[rgbSource.Length];

        for (int i = 0; i < rgbSource.Length; i++)
        {
            rgbSource[i] = rgbInput[i % rgbInput.Length];
            xybSource[i] = xybInput[i % xybInput.Length];
        }

        for (int space = 0; space < endpoints.Length; space++)
        {
            // Both sides are linear. This isolates XYB, primaries and white-point adaptation from RGB transfer approximations.
            // The ISO XYB connection side retains sRGB primaries. Only the RGB endpoint changes working space.
            ColorProfileConverter forward = new(new()
            {
                XybTransform = transform,
                SourceRgbWorkingSpace = endpoints[space],
                TargetRgbWorkingSpace = srgb
            });

            ColorProfileConverter inverse = new(new()
            {
                XybTransform = transform,
                SourceRgbWorkingSpace = srgb,
                TargetRgbWorkingSpace = endpoints[space]
            });
            ReadOnlySpan<Xyb> expectedForward = space == 0 ? forwardP3 : forwardProPhoto;
            ReadOnlySpan<Rgb> expectedInverse = space == 0 ? inverseP3 : inverseProPhoto;
            forward.Convert(rgbSource, encoded);
            inverse.Convert(xybSource, decoded);

            for (int i = 0; i < rgbSource.Length; i++)
            {
                Xyb expectedXyb = expectedForward[i % expectedForward.Length];
                Rgb expectedRgb = expectedInverse[i % expectedInverse.Length];

                Assert.Equal(expectedXyb, forward.Convert<Rgb, Xyb>(rgbSource[i]), ConverterComparer);
                Assert.Equal(expectedXyb, encoded[i], ConverterComparer);
                Rgb actualRgb = inverse.Convert<Xyb, Rgb>(xybSource[i]);
                Assert.True(ConverterComparer.Equals(expectedRgb, actualRgb),
                    $"Space {space}; input {xybSource[i]}; expected {expectedRgb.ToScaledVector4()}; scalar {actualRgb.ToScaledVector4()}.");
                Assert.Equal(expectedRgb, decoded[i], ConverterComparer);
            }

            // The prepared inverse maps absorbance directly to the same native endpoint coordinates.
            // Reuse the independent P3/D65 and ProPhoto/D50 results above; this path has no RGB/XYZ
            // conversion afterward, so it separately checks matrix composition and intensity scaling.
            Xyb.InverseOperator directInverse = new(transform, endpoints[space], false);
            for (int i = 0; i < xybInput.Length; i++)
            {
                Xyb sample = xybInput[i];
                Rgb expectedRgb = expectedInverse[i];
                directInverse.Invoke(sample.X, sample.Y, sample.B, out float r, out float g, out float b);
                Assert.Equal(expectedRgb, new Rgb(r, g, b), ConverterComparer);

                // Each register holds one component. Broadcast a native sample and compare every
                // output lane, with distinct RGB expectations to detect component-order mistakes.
                if (Vector128.IsHardwareAccelerated)
                {
                    directInverse.Invoke(
                        Vector128.Create(sample.X),
                        Vector128.Create(sample.Y),
                        Vector128.Create(sample.B),
                        out Vector128<float> r128,
                        out Vector128<float> g128,
                        out Vector128<float> b128);

                    for (int lane = 0; lane < Vector128<float>.Count; lane++)
                    {
                        Assert.Equal(expectedRgb, new Rgb(r128.GetElement(lane), g128.GetElement(lane), b128.GetElement(lane)), ConverterComparer);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    directInverse.Invoke(
                        Vector256.Create(sample.X),
                        Vector256.Create(sample.Y),
                        Vector256.Create(sample.B),
                        out Vector256<float> r256,
                        out Vector256<float> g256,
                        out Vector256<float> b256);

                    for (int lane = 0; lane < Vector256<float>.Count; lane++)
                    {
                        Assert.Equal(expectedRgb, new Rgb(r256.GetElement(lane), g256.GetElement(lane), b256.GetElement(lane)), ConverterComparer);
                    }
                }

                if (Vector512.IsHardwareAccelerated)
                {
                    directInverse.Invoke(
                        Vector512.Create(sample.X),
                        Vector512.Create(sample.Y),
                        Vector512.Create(sample.B),
                        out Vector512<float> r512,
                        out Vector512<float> g512,
                        out Vector512<float> b512);

                    for (int lane = 0; lane < Vector512<float>.Count; lane++)
                    {
                        Assert.Equal(expectedRgb, new Rgb(r512.GetElement(lane), g512.GetElement(lane), b512.GetElement(lane)), ConverterComparer);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates linear sRGB with the original native reference's xy white point and rounded ICC primaries.
    /// </summary>
    /// <returns>The native reference working space.</returns>
    private static GammaWorkingSpace CreateNativeLinearSrgbWorkingSpace()
    {
        CieXyz white = new CieXyy(.3127F, .329F, 1F).ToProfileConnectingSpace(new());
        RgbPrimariesChromaticityCoordinates primaries = new(
            new(.639998686F, .330010138F),
            new(.300003784F, .600003357F),
            new(.150002046F, .059997204F));

        return new(1F, white, primaries);
    }
}
