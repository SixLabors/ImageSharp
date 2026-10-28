// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;
using Rgb = SixLabors.ImageSharp.ColorProfiles.Rgb;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public partial class ColorProfileConverterTests(ITestOutputHelper testOutputHelper)
{
    // for 3-channel spaces, 4th item is ignored
    private static readonly List<float[]> Inputs =
    [
        [0, 0, 0, 0],
        [1, 0, 0, 0],
        [0, 1, 0, 0],
        [0, 0, 1, 0],
        [0, 0, 0, 1],
        [1, 1, 1, 1],
        [0.5f, 0.5f, 0.5f, 0.5f],
        [0.199678659f, 0.67982769f, 0.805381715f, 0.982666492f], // Exposes clipping before source PCS adjustment.
        [0.776568174f, 0.961630166f, 0.31032759f, 0.895294666f], // Exposes clipping after target PCS adjustment.
        [GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue()]
    ];

    /// <summary>
    /// Checks non-mutating row conversion against whole-image conversion for straight and associated pixel formats.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="provider">A real ICC fixture containing zero, partial and opaque alpha.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossy.WithIccpAndAlpha, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    public void ConvertPixelRows_MatchesImageConversion<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HwIntrinsics intrinsics = HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

        FeatureTestRunner.RunWithHwIntrinsicsFeature(PixelTests<TPixel>.RunPixelRowConversion, intrinsics, provider);
    }

    /// <summary>
    /// Compares reusable row-buffer conversion with the established whole-image ICC path.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="provider">The real image fixture in the requested pixel format.</param>
    private static void AssertPixelRowConversion<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> expected = provider.GetImage();
        using Image<TPixel> actual = provider.GetImage();
        IccProfile profile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile.GetIccProfileForEncoding(profile, PixelColorType.RGB, provider.Configuration.MemoryAllocator, out ColorProfileConverter converter);

        Assert.NotNull(converter);
        converter.Convert(expected);

        // Allocate once, as an encoder does. Every row reuses its output and scratch storage.
        TPixel[] originalRow = new TPixel[actual.Width];
        Vector4[] vectors = new Vector4[actual.Width];
        Rgb[] scratch = new Rgb[actual.Width];

        for (int y = 0; y < actual.Height; y++)
        {
            Span<TPixel> row = actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            row.CopyTo(originalRow);
            converter.Convert<TPixel>(provider.Configuration, row, vectors, scratch);

            // Verify the input before explicitly packing the result. ICC output is straight; the pixel API restores target association.
            Assert.True(row.SequenceEqual(originalRow));
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(provider.Configuration, vectors, row);
        }

        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.JapanColor2003)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 8-bit vs 16-bit)
    [InlineData(TestIccProfiles.JapanColor2011, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.Fogra39, 5E-4F)] // Nonlinear profile stages.
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.RommRgb, 5E-4F)] // Nonlinear profile stages.
    [InlineData(TestIccProfiles.RommRgb, TestIccProfiles.StandardRgbV4, 5E-4F)] // Nonlinear profile stages.
    public void CanConvertIccProfiles(string sourceProfile, string targetProfile, double tolerance = 0.000005)
    {
        List<Vector4> actual = Inputs.ConvertAll(input => GetActualTargetValues(input, sourceProfile, targetProfile, IccInterpolationMethod.Trilinear));
        AssertConversion(sourceProfile, targetProfile, actual, tolerance, testOutputHelper);
    }

    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.JapanColor2003)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 8-bit vs 16-bit)
    [InlineData(TestIccProfiles.JapanColor2011, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.Fogra39, 5E-4F)] // Nonlinear profile stages.
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.RommRgb, 5E-4F)] // Nonlinear profile stages.
    [InlineData(TestIccProfiles.RommRgb, TestIccProfiles.StandardRgbV4, 5E-4F)] // Nonlinear profile stages.
    [InlineData(TestIccProfiles.Issue129, TestIccProfiles.StandardRgbV4, 5E-4F)] // Nonlinear profile stages.
    public void CanBulkConvertIccProfiles(string sourceProfile, string targetProfile, double tolerance = 0.000005)
    {
        List<Vector4> actual = GetBulkActualTargetValues(Inputs, sourceProfile, targetProfile, IccInterpolationMethod.Trilinear);
        AssertConversion(sourceProfile, targetProfile, actual, tolerance, testOutputHelper);
    }

    /// <summary>
    /// Checks scalar and bulk connections against LittleCMS without changing profile headers or clipping intermediate XYZ.
    /// </summary>
    /// <param name="sourceProfile">The unchanged source profile.</param>
    /// <param name="targetProfile">The unchanged destination profile.</param>
    /// <param name="expected">The independent floating-point outputs for the fixed device samples.</param>
    [Theory]
    [MemberData(nameof(IccConversionDataProfiles.UnboundedConnections), MemberType = typeof(IccConversionDataProfiles))]
    public void Convert_UnboundedProfileConnections_MatchLittleCms(string sourceProfile, string targetProfile, Vector4[] expected)
    {
        List<float[]> inputs = IccConversionDataProfiles.Inputs;
        List<Vector4> scalar = inputs.ConvertAll(input => GetActualTargetValues(input, sourceProfile, targetProfile, IccInterpolationMethod.Auto));
        List<Vector4> bulk = GetBulkActualTargetValues(inputs, sourceProfile, targetProfile, IccInterpolationMethod.Auto);

        // Unicolour's PCS adjustment clips negative XYZ and the old adapter changes v2 profile headers to avoid it.
        // Neither result describes this unbounded connection. The fixed native data uses the original profiles and source intent.
        // Auto matches LittleCMS's device-input and Lab-input table choices. Separate operator tests retain multilinear coverage.
        // Keep the existing 5E-4 component bound for analytic curves and the reference's inverse-curve quantization.
        ApproximateFloatComparer comparer = new(5E-4F);
        Assert.Equal(expected, scalar, comparer);
        Assert.Equal(expected, bulk, comparer);
    }

    /// <summary>
    /// Verifies that scalar and bulk Color conversion preserve source alpha and pack through the requested pixel format.
    /// </summary>
    /// <param name="associated">Whether the destination pixel format stores associated alpha.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConvertColor_ScalarAndBulkPreserveAlpha(bool associated)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb),
            TargetIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2)
        });

        Color[] sources =
        [
            Color.FromPixel(RgbaVectorP.FromUnassociatedScaledVector4(new Vector4(0.25F, 0.5F, 0.75F, 0.4F))),
            Color.FromScaledVector(new Vector4(0.3F, 0.6F, 0.2F, 1.25F))
        ];

        Color[] bulk = [.. sources];
        if (associated)
        {
            converter.Convert<RgbaVectorP>(bulk);
        }
        else
        {
            converter.Convert<RgbaVector>(bulk);
        }

        for (int i = 0; i < sources.Length; i++)
        {
            Vector4 source = sources[i].ToScaledVector4(PixelAlphaRepresentation.Unassociated);
            Rgb expectedRgb = converter.Convert<Rgb, Rgb>(Rgb.FromScaledVector4(source));
            Vector4 expectedVector = new(expectedRgb.ToScaledVector3(), source.W);

            // The existing RGB converter independently establishes the ICC result before target alpha packing.
            Color expected = associated
                ? Color.FromPixel(RgbaVectorP.FromUnassociatedScaledVector4(expectedVector))
                : Color.FromPixel(RgbaVector.FromUnassociatedScaledVector4(expectedVector));

            Color scalar = associated
                ? converter.Convert<RgbaVectorP>(sources[i])
                : converter.Convert<RgbaVector>(sources[i]);

            Assert.Equal(expected, scalar);
            Assert.Equal(scalar, bulk[i]);
            Assert.Equal(associated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated, scalar.AlphaRepresentation);
            // Target pixel packing retains valid opacity and bounds the over-range input to one.
            Assert.Equal(Math.Clamp(source.W, 0F, 1F), scalar.ToScaledVector4(PixelAlphaRepresentation.Unassociated).W);
        }
    }

    /// <summary>
    /// Verifies that both transforms use the source rendering intent when the profile headers disagree.
    /// </summary>
    /// <param name="targetFile">The destination profile, using either curves or a LUT.</param>
    /// <param name="tolerance">The error bound before the target's final output curves.</param>
    [Theory]
    [InlineData(TestIccProfiles.StandardRgbV2, 0.0005)]
    [InlineData(TestIccProfiles.StandardRgbV4, 5E-4F)]
    public void Convert_UsesSourceRenderingIntentForBothProfiles(string targetFile, double tolerance)
    {
        IccProfile sourceProfile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39).DeepClone();
        sourceProfile.Header.RenderingIntent = IccRenderingIntent.MediaRelativeColorimetric;
        IccProfile targetProfile = TestIccProfiles.GetProfile(targetFile);

        // The v4 profile's final curves amplify errors in extended RGB: the red linear branch has slope 137.204.
        // Compare in the preceding curve's units so the measured power bound is not applied in different units.
        IccTagDataEntry[] outputCurves = targetProfile.Entries.OfType<IccLutBToATagDataEntry>()
            .FirstOrDefault(entry => entry.TagSignature == IccProfileTag.BToA1)?.CurveA;

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = sourceProfile,
            TargetIccProfile = targetProfile,
            IccInterpolationMethod = IccInterpolationMethod.Trilinear
        });

        // Explicitly select the same intent in the independent reference converter. Its unspecified
        // intent uses each profile's header, which would reproduce the conflicting-intent defect.
        Wacton.Unicolour.Configuration sourceConfig = new(iccConfig: new IccConfiguration(
            Path.Combine("TestDataIcc", "Profiles", TestIccProfiles.Fogra39), Intent.RelativeColorimetric));

        Wacton.Unicolour.Configuration targetConfig = new(iccConfig: new IccConfiguration(
            Path.Combine("TestDataIcc", "Profiles", targetFile), Intent.RelativeColorimetric));

        Cmyk[] inputs =
        [
            new(0, 0, 0, 0), new(0, 0, 0, 1),
            new(0, 1, 1, 0), new(1, 0, 1, 0), new(1, 1, 0, 0), new(0, 0, 1, 0),
            new(0.25F, 0.5F, 0.75F, 0.125F)
        ];

        Rgb[] bulk = new Rgb[inputs.Length];
        converter.Convert<Cmyk, Rgb>(inputs, bulk);

        for (int i = 0; i < inputs.Length; i++)
        {
            Cmyk input = inputs[i];
            Unicolour reference = new(sourceConfig, new Channels(input.C, input.M, input.Y, input.K));
            Unicolour expected = reference.ConvertToConfiguration(targetConfig);
            Assert.Null(expected.Icc.Error);
            Vector4 scalar = converter.Convert<Cmyk, Rgb>(input).ToScaledVector4();
            Vector4 span = bulk[i].ToScaledVector4();

            for (int channel = 0; channel < 3; channel++)
            {
                float expectedValue = (float)expected.Icc.Values[channel];
                float scalarValue = scalar[channel];
                float spanValue = span[channel];
                if (outputCurves is not null)
                {
                    IccColorConverter.CurveOperator outputCurve = new(outputCurves[channel], true);
                    expectedValue = outputCurve.Convert(expectedValue);
                    scalarValue = outputCurve.Convert(scalarValue);
                    spanValue = outputCurve.Convert(spanValue);
                }

                Assert.Equal(expectedValue, scalarValue, tolerance);
                Assert.Equal(expectedValue, spanValue, tolerance);
            }
        }

        // Conversion must not rewrite a profile that may be shared with other callers.
        Assert.Equal(IccRenderingIntent.Perceptual, targetProfile.Header.RenderingIntent);
    }

    /// <summary>
    /// Checks that a curve-only source does not change the intent selected by a table-based profile on the return conversion.
    /// </summary>
    [Fact]
    public void Convert_UsesTargetRenderingIntentForCurveSource()
    {
        IccProfile sourceProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        IccProfile targetProfile = TestIccProfiles.GetProfile(TestIccProfiles.Fogra39).DeepClone();
        targetProfile.Header.RenderingIntent = IccRenderingIntent.MediaRelativeColorimetric;

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = sourceProfile,
            TargetIccProfile = targetProfile,
            IccInterpolationMethod = IccInterpolationMethod.Trilinear
        });

        // The curve profile recommends perceptual intent. Select relative explicitly in the independent
        // reference so this detects the previous use of the target's perceptual BToA0 table.
        Wacton.Unicolour.Configuration sourceConfig = new(iccConfig: new IccConfiguration(
            Path.Combine("TestDataIcc", "Profiles", TestIccProfiles.StandardRgbV2), Intent.RelativeColorimetric));

        Wacton.Unicolour.Configuration targetConfig = new(iccConfig: new IccConfiguration(
            Path.Combine("TestDataIcc", "Profiles", TestIccProfiles.Fogra39), Intent.RelativeColorimetric));

        // Include the dark background that exposed the intent mismatch, plus neutral and colored samples.
        Rgb[] inputs = [new(44F / 255F, 51F / 255F, 58F / 255F), new(0.5F, 0.5F, 0.5F), new(0.25F, 0.5F, 0.75F)];
        Cmyk[] bulk = new Cmyk[inputs.Length];
        converter.Convert<Rgb, Cmyk>(inputs, bulk);

        for (int i = 0; i < inputs.Length; i++)
        {
            Rgb input = inputs[i];
            Unicolour reference = new(sourceConfig, new Channels(input.R, input.G, input.B));
            Unicolour expected = reference.ConvertToConfiguration(targetConfig);
            Assert.Null(expected.Icc.Error);

            Vector4 scalar = converter.Convert<Rgb, Cmyk>(input).ToScaledVector4();
            Vector4 span = bulk[i].ToScaledVector4();

            for (int channel = 0; channel < 4; channel++)
            {
                // The ICC connection includes approximate curves and PCS adjustment. Use their normalized error bound.
                Assert.Equal(expected.Icc.Values[channel], scalar[channel], 5E-4F);
                Assert.Equal(expected.Icc.Values[channel], span[channel], 5E-4F);
            }
        }

        Assert.Equal(IccRenderingIntent.Perceptual, sourceProfile.Header.RenderingIntent);
        Assert.Equal(IccRenderingIntent.MediaRelativeColorimetric, targetProfile.Header.RenderingIntent);
    }

    // Cases with nonlinear ICC curves or PCS adjustment use the recorded power error bound.
    // Pure table comparisons retain their existing interpolation tolerance.
    private static void AssertConversion(string sourceProfile, string targetProfile, List<Vector4> actual, double tolerance, ITestOutputHelper testOutputHelper)
    {
        List<double[]> expected = Inputs.ConvertAll(input => GetExpectedTargetValues(sourceProfile, targetProfile, input, testOutputHelper));
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            Log(testOutputHelper, Inputs[i], expected[i], actual[i]);
            for (int j = 0; j < expected[i].Length; j++)
            {
                Assert.Equal(expected[i][j], actual[i][j], tolerance);
            }
        }
    }

    private static double[] GetExpectedTargetValues(string sourceProfile, string targetProfile, float[] input, ITestOutputHelper testOutputHelper)
    {
        Wacton.Unicolour.Configuration sourceConfig = TestIccProfiles.GetUnicolourConfiguration(sourceProfile);
        Wacton.Unicolour.Configuration targetConfig = TestIccProfiles.GetUnicolourConfiguration(targetProfile);

        if (sourceConfig.Icc.Error != null || targetConfig.Icc.Error != null)
        {
            Assert.Fail("Unicolour does not support the ICC profile - test values will need to be calculated manually");
        }

        /* This is a hack to trick Unicolour to work in the same way as ImageSharp.
         * ImageSharp bypasses PCS adjustment for v2 perceptual intent if source and target both need it
         * as they both share the same understanding of what the PCS is (see ColorProfileConverterExtensionsIcc.GetTargetPcsWithPerceptualAdjustment)
         * Unicolour does not support a direct profile-to-profile conversion so will always perform PCS adjustment for v2 perceptual intent.
         * However, PCS adjustment clips negative XYZ values, causing those particular values in Unicolour and ImageSharp to diverge.
         * It's unclear to me if there's a fundamental correct answer here.
         *
         * There are two obvious ways to keep Unicolour and ImageSharp values aligned:
         * 1. Make ImageSharp always perform PCS adjustment, clipping negative XYZ values during the process - but creates a lot more calculations
         * 2. Make Unicolour stop performing PCS adjustment, allowing negative XYZ values during conversion
         *
         * Option 2 is implemented by modifying the profiles so they claim to be v4 profiles
         * since v4 perceptual profiles do not apply PCS adjustment.
         */
        bool isSourcePerceptualV2 = sourceConfig.Icc.Intent == Intent.Perceptual && sourceConfig.Icc.Profile!.Header.ProfileVersion.Major == 2;
        bool isTargetPerceptualV2 = targetConfig.Icc.Intent == Intent.Perceptual && targetConfig.Icc.Profile!.Header.ProfileVersion.Major == 2;
        if (isSourcePerceptualV2 && isTargetPerceptualV2)
        {
            sourceConfig = GetUnicolourConfigAsV4Header(sourceProfile);
            targetConfig = GetUnicolourConfigAsV4Header(targetProfile);
        }

        Channels channels = new([.. input.Select(value => (double)value)]);
        Unicolour source = new(sourceConfig, channels);
        Unicolour target = source.ConvertToConfiguration(targetConfig);
        if (target.Icc.Error != null)
        {
            testOutputHelper.WriteLine($"Error during Unicolour ICC conversion of supported profile: {target.Icc.Error}");
        }

        return target.Icc.Values;
    }

    private static Wacton.Unicolour.Configuration GetUnicolourConfigAsV4Header(string profileFile)
    {
        string profilePath = TestIccProfiles.GetFullPath(profileFile);
        string modifiedFilename = $"{Path.GetFileNameWithoutExtension(profilePath)}_modified.icc";
        string modifiedProfile = Path.Combine(Path.GetDirectoryName(profilePath)!, modifiedFilename);

        Wacton.Unicolour.Configuration modifiedConfig;
        if (!TestIccProfiles.HasUnicolourConfiguration(modifiedProfile))
        {
            byte[] bytes = File.ReadAllBytes(profilePath);
            bytes[8] = 4; // byte 8 of profile is major version
            File.WriteAllBytes(modifiedProfile, bytes);
            modifiedConfig = TestIccProfiles.GetUnicolourConfiguration(modifiedProfile);
            File.Delete(modifiedProfile);
        }
        else
        {
            modifiedConfig = TestIccProfiles.GetUnicolourConfiguration(modifiedProfile);
        }

        return modifiedConfig;
    }

    /// <summary>
    /// Converts one device color through the requested profile pair and interpolation method.
    /// </summary>
    /// <param name="input">The normalized device components.</param>
    /// <param name="sourceProfile">The source profile fixture.</param>
    /// <param name="targetProfile">The destination profile fixture.</param>
    /// <param name="interpolationMethod">The table interpolation used by the independent comparison.</param>
    /// <returns>The destination device components.</returns>
    private static Vector4 GetActualTargetValues(float[] input, string sourceProfile, string targetProfile, IccInterpolationMethod interpolationMethod)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfile),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfile),
            IccInterpolationMethod = interpolationMethod
        });

        IccColorSpaceType sourceDataSpace = converter.Options.SourceIccProfile!.Header.DataColorSpace;
        IccColorSpaceType targetDataSpace = converter.Options.TargetIccProfile!.Header.DataColorSpace;
        return sourceDataSpace switch
        {
            IccColorSpaceType.Cmyk when targetDataSpace == IccColorSpaceType.Cmyk
                => converter.Convert<Cmyk, Cmyk>(new Cmyk(new Vector4(input))).ToScaledVector4(),
            IccColorSpaceType.Cmyk when targetDataSpace == IccColorSpaceType.Rgb
                => converter.Convert<Cmyk, Rgb>(new Cmyk(new Vector4(input))).ToScaledVector4(),
            IccColorSpaceType.Rgb when targetDataSpace == IccColorSpaceType.Cmyk
                => converter.Convert<Rgb, Cmyk>(new Rgb(new Vector3(input))).ToScaledVector4(),
            IccColorSpaceType.Rgb when targetDataSpace == IccColorSpaceType.Rgb
                => converter.Convert<Rgb, Rgb>(new Rgb(new Vector3(input))).ToScaledVector4(),
            _ => throw new NotSupportedException($"Unsupported ICC profile data color space conversion: {sourceDataSpace} -> {targetDataSpace}")
        };
    }

    /// <summary>
    /// Converts device colors through the requested profile pair and interpolation method.
    /// </summary>
    /// <param name="inputs">The normalized device components.</param>
    /// <param name="sourceProfile">The source profile fixture.</param>
    /// <param name="targetProfile">The destination profile fixture.</param>
    /// <param name="interpolationMethod">The table interpolation used by the independent comparison.</param>
    /// <returns>The destination device components in source order.</returns>
    private static List<Vector4> GetBulkActualTargetValues(
        List<float[]> inputs,
        string sourceProfile,
        string targetProfile,
        IccInterpolationMethod interpolationMethod)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfile),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfile),
            IccInterpolationMethod = interpolationMethod
        });

        IccColorSpaceType sourceDataSpace = converter.Options.SourceIccProfile!.Header.DataColorSpace;
        IccColorSpaceType targetDataSpace = converter.Options.TargetIccProfile!.Header.DataColorSpace;

        switch (sourceDataSpace)
        {
            case IccColorSpaceType.Cmyk:
            {
                Span<Cmyk> inputSpan = [.. inputs.Select(x => new Cmyk(new Vector4(x)))];

                switch (targetDataSpace)
                {
                    case IccColorSpaceType.Cmyk:
                    {
                        Span<Cmyk> outputSpan = stackalloc Cmyk[inputs.Count];
                        converter.Convert<Cmyk, Cmyk>(inputSpan, outputSpan);
                        return [.. outputSpan.ToArray().Select(x => x.ToScaledVector4())];
                    }

                    case IccColorSpaceType.Rgb:
                    {
                        Span<Rgb> outputSpan = stackalloc Rgb[inputs.Count];
                        converter.Convert<Cmyk, Rgb>(inputSpan, outputSpan);
                        return [.. outputSpan.ToArray().Select(x => x.ToScaledVector4())];
                    }

                    default:
                        throw new NotSupportedException($"Unsupported ICC profile data color space conversion: {sourceDataSpace} -> {targetDataSpace}");
                }
            }

            case IccColorSpaceType.Rgb:
            {
                Span<Rgb> inputSpan = [.. inputs.Select(x => new Rgb(new Vector3(x)))];

                switch (targetDataSpace)
                {
                    case IccColorSpaceType.Cmyk:
                    {
                        Span<Cmyk> outputSpan = stackalloc Cmyk[inputs.Count];
                        converter.Convert<Rgb, Cmyk>(inputSpan, outputSpan);
                        return [.. outputSpan.ToArray().Select(x => x.ToScaledVector4())];
                    }

                    case IccColorSpaceType.Rgb:
                    {
                        Span<Rgb> outputSpan = stackalloc Rgb[inputs.Count];
                        converter.Convert<Rgb, Rgb>(inputSpan, outputSpan);
                        return [.. outputSpan.ToArray().Select(x => x.ToScaledVector4())];
                    }

                    default:
                        throw new NotSupportedException($"Unsupported ICC profile data color space conversion: {sourceDataSpace} -> {targetDataSpace}");
                }
            }

            default:
                throw new NotSupportedException($"Unsupported ICC profile data color space conversion: {sourceDataSpace} -> {targetDataSpace}");
        }
    }

    private static float GetNormalizedRandomValue()
    {
        // Generate a random value between 0 (inclusive) and 1 (exclusive).
        double value = Random.Shared.NextDouble();

        // If the random value is exactly 0, return 0F to ensure inclusivity at the lower bound.
        // For non-zero values, add a small increment (0.0000001F) to ensure the range
        // is inclusive at the upper bound while retaining precision.
        // Clamp the result between 0 and 1 to ensure it does not exceed the bounds.
        return value == 0 ? 0F : Math.Clamp((float)value + 0.0000001F, 0, 1);
    }

    private static void Log(ITestOutputHelper testOutputHelper, float[] input, double[] expected, Vector4 actual)
    {
        string inputText = string.Join(", ", input);
        string expectedText = string.Join(", ", expected.Select(x => $"{x:f8}"));
        testOutputHelper.WriteLine($"Input {inputText} · Expected output {expectedText} · Actual output {actual}");
    }

    /// <summary>
    /// Provides non-generic callbacks whose declaring type retains the requested pixel format for RemoteExecutor.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    private static class PixelTests<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Restores the typed provider and runs the assertions with the selected hardware features.
        /// </summary>
        /// <param name="providerDump">The serialized image provider.</param>
        public static void RunPixelRowConversion(string providerDump)
            => AssertPixelRowConversion(FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump));
    }
}
