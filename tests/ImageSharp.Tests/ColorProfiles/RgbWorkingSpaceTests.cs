// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="RgbWorkingSpace"/> class.
/// </summary>
[Trait("Color", "Conversion")]
public class RgbWorkingSpaceTests
{
    private static readonly ApproximateFloatComparer TolerantComparer = new(1e-5F);

    public static readonly TheoryData<RgbWorkingSpace> WorkingSpaces = new()
    {
        KnownRgbWorkingSpaces.SRgb,
        KnownRgbWorkingSpaces.SRgbSimplified,
        KnownRgbWorkingSpaces.Rec709,
        KnownRgbWorkingSpaces.Rec2020,
        KnownRgbWorkingSpaces.ECIRgbv2,
        KnownRgbWorkingSpaces.AdobeRgb1998,
        KnownRgbWorkingSpaces.ApplesRgb,
        KnownRgbWorkingSpaces.BestRgb,
        KnownRgbWorkingSpaces.BetaRgb,
        KnownRgbWorkingSpaces.BruceRgb,
        KnownRgbWorkingSpaces.CIERgb,
        KnownRgbWorkingSpaces.ColorMatchRgb,
        KnownRgbWorkingSpaces.DonRgb4,
        KnownRgbWorkingSpaces.EktaSpacePS5,
        KnownRgbWorkingSpaces.NTSCRgb,
        KnownRgbWorkingSpaces.PALSECAMRgb,
        KnownRgbWorkingSpaces.ProPhotoRgb,
        KnownRgbWorkingSpaces.SMPTECRgb,
        KnownRgbWorkingSpaces.WideGamutRgb
    };

    [Fact]
    public void RgbWorkingSpaceEqualityRequiresSameConcreteType()
    {
        RgbWorkingSpace sRgb = KnownRgbWorkingSpaces.SRgb;
        RgbWorkingSpace sRgbSimplified = KnownRgbWorkingSpaces.SRgbSimplified;
        RgbWorkingSpace rec709 = KnownRgbWorkingSpaces.Rec709;

        Assert.False(sRgb.Equals(sRgbSimplified));
        Assert.False(sRgbSimplified.Equals(sRgb));
        Assert.False(sRgb.Equals(rec709));
        Assert.False(rec709.Equals(sRgb));

        Assert.NotEqual(sRgb.GetHashCode(), sRgbSimplified.GetHashCode());
        Assert.NotEqual(sRgb.GetHashCode(), rec709.GetHashCode());
    }

    [Fact]
    public void RgbWorkingSpaceEqualityMatchesSameConcreteTypeValues()
    {
        RgbWorkingSpace x = new SRgbWorkingSpace(
            KnownRgbWorkingSpaces.SRgb.WhitePoint,
            KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

        RgbWorkingSpace y = new SRgbWorkingSpace(
            KnownRgbWorkingSpaces.SRgb.WhitePoint,
            KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates);

        Assert.Equal(x, y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Fact]
    public void GammaWorkingSpaceEqualityIncludesGamma()
    {
        GammaWorkingSpace x = new(
            2.2F,
            KnownRgbWorkingSpaces.SRgbSimplified.WhitePoint,
            KnownRgbWorkingSpaces.SRgbSimplified.ChromaticityCoordinates);

        GammaWorkingSpace y = new(
            1.8F,
            KnownRgbWorkingSpaces.SRgbSimplified.WhitePoint,
            KnownRgbWorkingSpaces.SRgbSimplified.ChromaticityCoordinates);

        Assert.NotEqual(x, y);
        Assert.NotEqual(x.GetHashCode(), y.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(WorkingSpaces))]
    public void CompressAndExpand_RoundTripsWithTolerance(RgbWorkingSpace workingSpace)
    {
        Vector4[] linear =
        [
            new(0F, .001F, .18F, 1F), // Endpoint, below the sRGB breakpoint, common middle gray, and opaque alpha.
            new(.0031308F, .25F, .5F, .75F), // sRGB linear/gamma breakpoint with interior values and partial alpha.
            new(.75F, .5F, .25F, .5F), // Reversed interior values ensure channel order does not hide errors.
            new(1F, .8F, .2F, .25F) // Upper endpoint, high/low interior values, and low alpha.
        ];

        Vector4[] expectedCompressed = new Vector4[linear.Length];

        for (int i = 0; i < linear.Length; i++)
        {
            Vector4 compressed = workingSpace.Compress(linear[i]);
            Vector4 expanded = workingSpace.Expand(compressed);

            expectedCompressed[i] = compressed;

            Assert.Equal(linear[i], expanded, TolerantComparer);
        }

        Vector4[] actualCompressed = linear.ToArray();
        workingSpace.Compress(actualCompressed);

        Assert.Equal(expectedCompressed, actualCompressed, TolerantComparer);

        workingSpace.Expand(actualCompressed);

        Assert.Equal(linear, actualCompressed, TolerantComparer);
    }
}
