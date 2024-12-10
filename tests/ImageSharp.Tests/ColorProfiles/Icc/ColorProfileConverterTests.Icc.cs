// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public class ColorProfileConverterTests
{
    [Theory]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Fogra39)]
    [InlineData(TestIccProfiles.Fogra39, TestIccProfiles.Swop2006)]
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Fogra39)]
    [InlineData(TestIccProfiles.Swop2006, TestIccProfiles.Swop2006)]
    public void CanConvertCmykIccProfiles(string sourceProfileName, string targetProfileName)
    {
        Cmyk input = new(GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue(), GetNormalizedRandomValue());

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(sourceProfileName),
            TargetIccProfile = TestIccProfiles.GetProfile(targetProfileName),
        });

        Cmyk expectedTargetValues = GetExpectedTargetCmyk(sourceProfileName, targetProfileName, input);
        Cmyk actualTargetValues = converter.Convert<Cmyk, Cmyk>(input);

        const double tolerance = 0.0000005;
        Assert.Equal(expectedTargetValues.C, actualTargetValues.C, tolerance);
        Assert.Equal(expectedTargetValues.M, actualTargetValues.M, tolerance);
        Assert.Equal(expectedTargetValues.Y, actualTargetValues.Y, tolerance);
        Assert.Equal(expectedTargetValues.K, actualTargetValues.K, tolerance);
    }

    private static Cmyk GetExpectedTargetCmyk(string sourceProfileName, string targetProfileName, Cmyk sourceCmyk)
    {
        Wacton.Unicolour.Configuration sourceConfig = TestIccProfiles.GetUnicolourConfiguration(sourceProfileName);
        Wacton.Unicolour.Configuration targetConfig = TestIccProfiles.GetUnicolourConfiguration(targetProfileName);

        Channels channels = new(sourceCmyk.C, sourceCmyk.M, sourceCmyk.Y, sourceCmyk.K);

        Unicolour source = new(sourceConfig, channels);
        ColourSpace pcs = sourceConfig.Icc.Profile!.Header.Pcs == "Lab " ? ColourSpace.Lab : ColourSpace.Xyz;
        ColourTriplet pcsTriplet = pcs == ColourSpace.Lab ? source.Lab.Triplet : source.Xyz.Triplet;
        Unicolour target = new(targetConfig, pcs, pcsTriplet.Tuple);
        double[] targetCmyk = target.Icc.Values;
        return new Cmyk((float)targetCmyk[0], (float)targetCmyk[1], (float)targetCmyk[2], (float)targetCmyk[3]);
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
}
