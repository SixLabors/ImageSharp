// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;
using Xunit.Abstractions;
using Rgb = SixLabors.ImageSharp.ColorProfiles.Rgb;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public class ColorProfileConverterTests(ITestOutputHelper testOutputHelper)
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
        [0.199678659f, 0.67982769f, 0.805381715f, 0.982666492f], // requires clipping before source is PCS adjusted for Fogra39 -> sRGBv2
        [0.776568174f, 0.961630166f, 0.31032759f, 0.895294666f], // requires clipping after target is PCS adjusted for Fogra39 -> sRGBv2
        [GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue()]
    ];

    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.JapanColor2003)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 8-bit vs 16-bit)
    [InlineData(TestIccProfiles.JapanColor2011, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Cgats21)] // CMYK -> LAB -> RGB (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV4)] // RGB -> LAB -> CMYK (different LUT versions, v4 vs v2)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.Fogra39)] // RGB -> LAB -> XYZ -> RGB (different LUT elements, B-Matrix-M-CLUT-A vs B-Matrix-M)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.RommRgb)] // RGB -> XYZ -> LAB -> RGB (different LUT elements, B-Matrix-M vs B-Matrix-M-CLUT-A)
    [InlineData(TestIccProfiles.RommRgb, TestIccProfiles.StandardRgbV4)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 16-bit vs 8-bit)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV2, 0.0005)] // CMYK -> LAB -> XYZ -> RGB (different LUT tags, A2B vs TRC) --- tolerance slightly higher due to difference in inverse curve implementation
    [InlineData(TestIccProfiles.StandardRgbV2, TestIccProfiles.Fogra39)] // RGB -> XYZ -> LAB -> CMYK (different LUT tags, TRC vs A2B)
    public void CanConvertIccProfiles(string sourceProfile, string targetProfile, double tolerance = 0.00005)
    {
        List<Vector4> actual = Inputs.ConvertAll(input => GetActualTargetValues(input, sourceProfile, targetProfile));
        AssertConversion(sourceProfile, targetProfile, actual, tolerance, testOutputHelper);
    }

    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.JapanColor2003)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 8-bit vs 16-bit)
    [InlineData(TestIccProfiles.JapanColor2011, TestIccProfiles.Fogra39)] // CMYK -> LAB -> CMYK (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Cgats21)] // CMYK -> LAB -> RGB (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV4)] // RGB -> LAB -> CMYK (different LUT versions, v4 vs v2)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.Fogra39)] // RGB -> LAB -> XYZ -> RGB (different LUT elements, B-Matrix-M-CLUT-A vs B-Matrix-M)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.RommRgb)] // RGB -> XYZ -> LAB -> RGB (different LUT elements, B-Matrix-M vs B-Matrix-M-CLUT-A)
    [InlineData(TestIccProfiles.RommRgb, TestIccProfiles.StandardRgbV4)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 16-bit vs 8-bit)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV2, 0.0005)] // CMYK -> LAB -> XYZ -> RGB (different LUT tags, A2B vs TRC) --- tolerance slightly higher due to difference in inverse curve implementation
    [InlineData(TestIccProfiles.StandardRgbV2, TestIccProfiles.Fogra39)] // RGB -> XYZ -> LAB -> CMYK (different LUT tags, TRC vs A2B)
    [InlineData(TestIccProfiles.Issue129, TestIccProfiles.StandardRgbV4)] // CMYK -> LAB -> -> XYZ -> RGB
    public void CanBulkConvertIccProfiles(string sourceProfile, string targetProfile, double tolerance = 0.00005)
    {
        List<Vector4> actual = GetBulkActualTargetValues(Inputs, sourceProfile, targetProfile);
        AssertConversion(sourceProfile, targetProfile, actual, tolerance, testOutputHelper);
    }

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
            sourceConfig = GetUnicolourConfigAsV4Header(sourceConfig);
            targetConfig = GetUnicolourConfigAsV4Header(targetConfig);
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

    private static Wacton.Unicolour.Configuration GetUnicolourConfigAsV4Header(Wacton.Unicolour.Configuration config)
    {
        string profilePath = config.Icc.Profile!.FileInfo.FullName;
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

    private static Vector4 GetActualTargetValues(float[] input, string sourceProfile, string targetProfile)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfile),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfile)
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

    private static List<Vector4> GetBulkActualTargetValues(List<float[]> inputs, string sourceProfile, string targetProfile)
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfile),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfile)
        });

        IccColorSpaceType sourceDataSpace = converter.Options.SourceIccProfile!.Header.DataColorSpace;
        IccColorSpaceType targetDataSpace = converter.Options.TargetIccProfile!.Header.DataColorSpace;

        switch (sourceDataSpace)
        {
            case IccColorSpaceType.Cmyk:
            {
                Span<Cmyk> inputSpan = inputs.Select(x => new Cmyk(new Vector4(x))).ToArray();

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
                Span<Rgb> inputSpan = inputs.Select(x => new Rgb(new Vector3(x))).ToArray();

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
}
