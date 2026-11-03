// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="RgbWorkingSpace"/> class.
/// </summary>
[Trait("Color", "Conversion")]
public class RgbWorkingSpaceTests
{
    private static readonly ApproximateFloatComparer TolerantComparer = new(1e-5F);

    // A round trip evaluates two approximate transfer functions. Scalar/bulk agreement keeps the tighter comparer above.
    private static readonly ApproximateFloatComparer RoundTripComparer = new(2F * 5E-4F);

    public static readonly TheoryData<RgbWorkingSpace> WorkingSpaces = new()
    {
        KnownRgbWorkingSpaces.SRgb,
        KnownRgbWorkingSpaces.SRgbSimplified,
        KnownRgbWorkingSpaces.Rec709,
        KnownRgbWorkingSpaces.Rec2020,
        KnownRgbWorkingSpaces.DisplayP3,
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

    /// <summary>
    /// Checks that HDR working-space identity includes the display peak.
    /// </summary>
    [Fact]
    public void HdrWorkingSpaceEqualityIncludesPeakLuminance()
    {
        RgbWorkingSpace primaries = KnownRgbWorkingSpaces.SRgb;
        PqWorkingSpace pq = new(255F, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        PqWorkingSpace samePq = new(255F, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        PqWorkingSpace otherPq = new(1000F, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        HlgWorkingSpace hlg = new(255F, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        HlgWorkingSpace sameHlg = new(255F, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        HlgWorkingSpace otherHlg = new(1000F, primaries.WhitePoint, primaries.ChromaticityCoordinates);

        Assert.Equal(pq, samePq);
        Assert.Equal(pq.GetHashCode(), samePq.GetHashCode());
        Assert.NotEqual(pq, otherPq);
        Assert.Equal(hlg, sameHlg);
        Assert.Equal(hlg.GetHashCode(), sameHlg.GetHashCode());
        Assert.NotEqual(hlg, otherHlg);
        Assert.NotEqual<RgbWorkingSpace>(pq, hlg);
    }

    /// <summary>
    /// Checks invalid luminance at the public HDR construction and companding boundaries.
    /// </summary>
    /// <param name="peak">The invalid display peak.</param>
    [Theory]
    [InlineData(0F)]
    [InlineData(-1F)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void HdrWorkingSpacesRejectInvalidPeakLuminance(float peak)
    {
        RgbWorkingSpace primaries = KnownRgbWorkingSpaces.SRgb;
        Assert.Throws<ArgumentOutOfRangeException>(() => new PqWorkingSpace(peak, primaries.WhitePoint, primaries.ChromaticityCoordinates));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HlgWorkingSpace(peak, primaries.WhitePoint, primaries.ChromaticityCoordinates));
        Assert.Throws<ArgumentOutOfRangeException>(() => PqCompanding.Expand(Vector4.Zero, peak));
        Assert.Throws<ArgumentOutOfRangeException>(() => PqCompanding.Compress(Vector4.Zero, peak));
    }

    /// <summary>
    /// Checks the float normalization limit before an infinite PQ factor can turn black into NaN.
    /// </summary>
    [Fact]
    public void PqWorkingSpaceRejectsUnrepresentableNormalization()
    {
        RgbWorkingSpace primaries = KnownRgbWorkingSpaces.SRgb;
        Assert.Throws<ArgumentOutOfRangeException>(() => new PqWorkingSpace(float.Epsilon, primaries.WhitePoint, primaries.ChromaticityCoordinates));
        Assert.Throws<ArgumentOutOfRangeException>(() => PqCompanding.Expand(Vector4.Zero, float.Epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => PqCompanding.Compress(Vector4.Zero, float.Epsilon));
    }

    /// <summary>
    /// Checks the positive finite HLG peak domain when division by 1000 would underflow before the prepared logarithm.
    /// </summary>
    [Fact]
    public void HlgWorkingSpaceSupportsSubnormalPeakLuminance()
    {
        RgbWorkingSpace primaries = KnownRgbWorkingSpaces.SRgb;
        HlgWorkingSpace workingSpace = new(float.Epsilon, primaries.WhitePoint, primaries.ChromaticityCoordinates);
        Assert.True(float.IsFinite(workingSpace.SystemGamma));
        Assert.True(workingSpace.SystemGamma > 0F);
        Assert.Equal(Vector4.Zero, workingSpace.Expand(Vector4.Zero));
        Assert.Equal(Vector4.Zero, workingSpace.Compress(Vector4.Zero));
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

            Assert.Equal(linear[i], expanded, RoundTripComparer);
            Assert.Equal(linear[i].W, compressed.W);
            Assert.Equal(linear[i].W, expanded.W);
        }

        Vector4[] actualCompressed = [.. linear];
        workingSpace.Compress(actualCompressed);

        Assert.Equal(expectedCompressed, actualCompressed, TolerantComparer);

        Vector3[] packed = linear.Select(color => new Vector3(color.X, color.Y, color.Z)).ToArray();
        workingSpace.Compress(packed.AsSpan());
        for (int i = 0; i < packed.Length; i++)
        {
            Assert.Equal(expectedCompressed[i], new Vector4(packed[i], linear[i].W), TolerantComparer);
        }

        workingSpace.Expand(packed.AsSpan());
        for (int i = 0; i < packed.Length; i++)
        {
            Assert.Equal(linear[i], new Vector4(packed[i], linear[i].W), RoundTripComparer);
        }

        workingSpace.Expand(actualCompressed);

        Assert.Equal(linear, actualCompressed, RoundTripComparer);

        for (int i = 0; i < linear.Length; i++)
        {
            Assert.Equal(linear[i].W, actualCompressed[i].W);
        }
    }

    /// <summary>
    /// Checks that existing custom bulk overrides also control packed RGB conversion across multiple stack blocks.
    /// </summary>
    [Fact]
    public void PackedRgbUsesCustomBulkOverrides()
    {
        CustomWorkingSpace custom = new();
        RgbWorkingSpace workingSpace = custom;
        Vector3[] colors = new Vector3[33];
        Array.Fill(colors, new Vector3(0.25F, 0.5F, 0.75F));

        workingSpace.Compress(colors.AsSpan());
        Assert.True(custom.CompressCalls > 0);
        Assert.All(colors, color => Assert.Equal(new Vector3(0.5F, 2F, 6F), color));

        workingSpace.Expand(colors.AsSpan());
        Assert.True(custom.ExpandCalls > 0);
        Assert.All(colors, color => Assert.Equal(new Vector3(0.25F, 0.5F, 0.75F), color));
    }

    /// <summary>
    /// Supplies an existing four-component working-space contract without packed RGB overrides.
    /// </summary>
    private sealed class CustomWorkingSpace : RgbWorkingSpace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomWorkingSpace"/> class with sRGB primaries and white.
        /// </summary>
        public CustomWorkingSpace()
            : base(KnownRgbWorkingSpaces.SRgb.WhitePoint, KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates)
        {
        }

        /// <summary>
        /// Gets the number of four-component compression calls.
        /// </summary>
        public int CompressCalls { get; private set; }

        /// <summary>
        /// Gets the number of four-component expansion calls.
        /// </summary>
        public int ExpandCalls { get; private set; }

        /// <inheritdoc/>
        public override void Compress(Span<Vector4> vectors)
        {
            this.CompressCalls++;

            for (int i = 0; i < vectors.Length; i++)
            {
                Assert.Equal(1F, vectors[i].W);
                vectors[i] = this.Compress(vectors[i]);
            }
        }

        /// <inheritdoc/>
        public override void Expand(Span<Vector4> vectors)
        {
            this.ExpandCalls++;

            for (int i = 0; i < vectors.Length; i++)
            {
                Assert.Equal(1F, vectors[i].W);
                vectors[i] = this.Expand(vectors[i]);
            }
        }

        /// <inheritdoc/>
        public override Vector4 Compress(Vector4 vector) => vector * new Vector4(2F, 4F, 8F, 1F);

        /// <inheritdoc/>
        public override Vector4 Expand(Vector4 vector) => vector / new Vector4(2F, 4F, 8F, 1F);
    }
}
