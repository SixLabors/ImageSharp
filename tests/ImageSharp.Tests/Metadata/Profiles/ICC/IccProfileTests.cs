// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccProfileTests
{
    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileIdTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CalculateHash_WithByteArray_CalculatesProfileHash(byte[] data, IccProfileId expected)
    {
        IccProfileId result = IccProfile.CalculateHash(data);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateHash_WithByteArray_DoesNotModifyData()
    {
        byte[] data = IccTestDataProfiles.ProfileRandomArray;
        var copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        IccProfile.CalculateHash(data);

        Assert.Equal(data, copy);
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileValidityTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CheckIsValid_WithProfiles_ReturnsValidity(byte[] data, bool expected)
    {
        var profile = new IccProfile(data);

        bool result = profile.CheckIsValid();

        Assert.Equal(expected, result);
    }

    private const string Fogra39 = "Coated_Fogra39L_VIGC_300.icc";
    private const string Swop2006 = "SWOP2006_Coated5v2.icc";

    [Theory]
    [InlineData(Fogra39, Fogra39)]
    [InlineData(Fogra39, Swop2006)]
    [InlineData(Swop2006, Fogra39)]
    [InlineData(Swop2006, Swop2006)]
    public void UnicolourComparison(string sourceProfileName, string targetProfileName)
    {
        string sourceIccFilepath = Path.Combine(".", "TestDataIcc", "Profiles", sourceProfileName);
        string targetIccFilepath = Path.Combine(".", "TestDataIcc", "Profiles", targetProfileName);

        // TODO: pass in specific values to test? use random values?
        Cmyk input = new(0.8f, 0.6f, 0.4f, 0.2f);
        Cmyk expectedTargetValues = GetExpectedTargetCmyk(sourceIccFilepath, targetIccFilepath, input);

        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = new IccProfile(File.ReadAllBytes(sourceIccFilepath)),
            TargetIccProfile = new IccProfile(File.ReadAllBytes(targetIccFilepath))
        });

        Cmyk actualTargetValues = converter.Convert<Cmyk, Cmyk>(input);

        const double tolerance = 0.0000005;
        Assert.Equal(expectedTargetValues.C, actualTargetValues.C, tolerance);
        Assert.Equal(expectedTargetValues.M, actualTargetValues.M, tolerance);
        Assert.Equal(expectedTargetValues.Y, actualTargetValues.Y, tolerance);
        Assert.Equal(expectedTargetValues.K, actualTargetValues.K, tolerance);
    }

    private static Cmyk GetExpectedTargetCmyk(string sourceIccFilepath, string targetIccFilepath, Cmyk sourceCmyk)
    {
        Wacton.Unicolour.Configuration sourceConfig = GetUnicolourConfig(sourceIccFilepath);
        Wacton.Unicolour.Configuration targetConfig = GetUnicolourConfig(targetIccFilepath);

        Channels channels = new(sourceCmyk.C, sourceCmyk.M, sourceCmyk.Y, sourceCmyk.K);

        Unicolour source = new(sourceConfig, channels);
        ColourSpace pcs = sourceConfig.Icc.Profile!.Header.Pcs == "Lab " ? ColourSpace.Lab : ColourSpace.Xyz;
        ColourTriplet pcsTriplet = pcs == ColourSpace.Lab ? source.Lab.Triplet : source.Xyz.Triplet;
        Unicolour target = new(targetConfig, pcs, pcsTriplet.Tuple);
        double[] targetCmyk = target.Icc.Values;
        return new Cmyk((float)targetCmyk[0], (float)targetCmyk[1], (float)targetCmyk[2], (float)targetCmyk[3]);
    }

    // Unicolour configurations are relatively expensive to instantiate
    private static readonly Dictionary<string, Wacton.Unicolour.Configuration> UnicolourConfigCache = new();

    private static Wacton.Unicolour.Configuration GetUnicolourConfig(string iccFilepath)
    {
        Wacton.Unicolour.Configuration config;
        if (UnicolourConfigCache.TryGetValue(iccFilepath, out config))
        {
            return config;
        }

        config = new Wacton.Unicolour.Configuration(iccConfiguration: new(iccFilepath, Intent.Unspecified));
        UnicolourConfigCache.Add(iccFilepath, config);
        return config;
    }
}
