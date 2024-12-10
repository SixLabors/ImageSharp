// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;
using Rgb = SixLabors.ImageSharp.ColorProfiles.Rgb;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public class ColorProfileConverterTests
{
    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (commonly used v2 profiles)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.JapanColor2011, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 16-bit vs 8-bit)
    [InlineData(TestIccProfiles.JapanColor2011, TestIccProfiles.Fogra39, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (different bit depth v2 LUTs, 8-bit vs 16-bit)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Cgats21, IccConversion.CmykToCmyk)] // CMYK -> LAB -> CMYK (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV4, IccConversion.CmykToRgb)] // CMYK -> LAB -> RGB (different LUT versions, v2 vs v4)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.Fogra39, IccConversion.RgbToCmyk)] // RGB -> LAB -> CMYK (different LUT versions, v4 vs v2)
    [InlineData(TestIccProfiles.StandardRgbV4, TestIccProfiles.RommRgb, IccConversion.RgbToRgb)] // RGB -> LAB -> XYZ -> RGB (different LUT elements, B-Matrix-M-CLUT-A vs B-Matrix-M)
    // TODO: enable once supported by Unicolour - in the meantime, manually test known values
    // [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV2, IccConversion.CmykToRgb)] // CMYK -> XYZ -> LAB -> RGB (different LUT tags, A2B vs TRC)
    // [InlineData(TestIccProfiles.StandardRgbV2, TestIccProfiles.Fogra39, IccConversion.RgbToCmyk)] // RGB -> XYZ -> LAB -> CMYK (different LUT tags, TRC vs A2B)
    public void CanConvertCmykIccProfiles(string sourceProfile, string targetProfile, IccConversion iccConversion)
    {
        // TODO: delete after testing
        float[] input = [0.734798908f, 0.887050927f, 0.476583719f, 0.547810674f];
        // float[] input = [GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue()];
        double[] expectedTargetValues = GetExpectedTargetValues(sourceProfile, targetProfile, input);

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfile),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfile)
        });

        Vector4 actualTargetValues = iccConversion switch
        {
            IccConversion.CmykToCmyk => converter.Convert<Cmyk, Cmyk>(new Cmyk(new Vector4(input))).ToScaledVector4(),
            IccConversion.CmykToRgb => converter.Convert<Cmyk, Rgb>(new Cmyk(new Vector4(input))).ToScaledVector4(),
            IccConversion.RgbToCmyk => converter.Convert<Rgb, Cmyk>(new Rgb(new Vector3(input))).ToScaledVector4(),
            IccConversion.RgbToRgb => converter.Convert<Rgb, Rgb>(new Rgb(new Vector3(input))).ToScaledVector4(),
            _ => throw new ArgumentOutOfRangeException(nameof(iccConversion), iccConversion, null)
        };

        const double tolerance = 0.000005;
        for (int i = 0; i < expectedTargetValues.Length; i++)
        {
            Assert.Equal(expectedTargetValues[i], actualTargetValues[i], tolerance);
        }
    }

    private static double[] GetExpectedTargetValues(string sourceProfile, string targetProfile, float[] input)
    {
        Wacton.Unicolour.Configuration sourceConfig = TestIccProfiles.GetUnicolourConfiguration(sourceProfile);
        Wacton.Unicolour.Configuration targetConfig = TestIccProfiles.GetUnicolourConfiguration(targetProfile);

        if (sourceConfig.Icc.Error != null || targetConfig.Icc.Error != null)
        {
            Assert.Fail("Unicolour does not support the ICC profile - test values manually in the meantime");
        }

        Channels channels = new(input.Select(value => (double)value).ToArray());

        Unicolour source = new(sourceConfig, channels);
        Unicolour target = source.ConvertToConfiguration(targetConfig);
        return target.Icc.Values;
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

    public enum IccConversion
    {
        CmykToCmyk,
        CmykToRgb,
        RgbToCmyk,
        RgbToRgb
    }
}
