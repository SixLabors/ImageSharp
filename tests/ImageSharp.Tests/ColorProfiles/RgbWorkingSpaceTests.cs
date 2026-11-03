// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Tests.TestUtilities;

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

    // Original libjxl v0.13.0, revision 91837342b24034078780b687f57f670a7def8153.
    // Decoder GetToLinearStage/GetFromLinearStage supply primary-weighted HLG RGB values at the recorded display peaks.
    // Keep the existing measured power bound. Values are normalized by max(1, abs(expected)) before comparison.
    private static readonly ApproximateFloatComparer NativeTransferComparer = new(5E-4F);

    private static readonly Vector3[] NativeHlgInputs =
    [
        new(-0F, 0F, 9.999999747e-05F),
        new(-0.25F, 0.1000000015F, 0.5F),
        new(1F, 1.100000024F, 1.25F),
        new(0.01999999955F, 0.400000006F, 0.8000000119F),
        new(-0.05000000075F, 0.3000000119F, 1.200000048F),
    ];

    private static readonly Vector3[][] NativeHlgExpandedRgb =
    [
        [
            new(-0F, 0F, 5.783149071e-09F),
            new(-0.02390510403F, 0.003824817017F, 0.0956204161F),
            new(0.9863445163F, 1.707822323F, 3.920354843F),
            new(0.0001428307005F, 0.05713228881F, 0.3672004044F),
            new(-0.0008635699633F, 0.03108851798F, 3.120228052F),
        ],
        [
            new(-0F, 0F, 3.973391061e-11F),
            new(-0.006895240396F, 0.001103238552F, 0.02758096159F),
            new(1.116877913F, 1.933836579F, 4.439177036F),
            new(7.66828307e-05F, 0.03067313507F, 0.1971422434F),
            new(-0.0006257182686F, 0.02252585627F, 2.260828495F),
        ],
        [
            new(-0F, 0F, 5.855971281e-16F),
            new(-0.0004290099023F, 6.864158786e-05F, 0.001716039609F),
            new(1.474265814F, 2.55264163F, 5.859661579F),
            new(1.91124218e-05F, 0.007644969504F, 0.04913571477F),
            new(-0.0003046761558F, 0.01096834149F, 1.10084784F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgCompressedRgb =
    [
        [
            new(-0F, 0F, 0.01489278302F),
            new(-0.7238216996F, 0.5264741778F, 0.857678473F),
            new(1.000400662F, 1.017839432F, 1.041171312F),
            new(0.2416737229F, 0.8243744969F, 0.9540410042F),
            new(-0.3812403381F, 0.7681210041F, 1.027573824F),
        ],
        [
            new(-0F, 0F, 0.04645323381F),
            new(-0.8323501348F, 0.6510172486F, 0.961771369F),
            new(0.9973803163F, 1.014825821F, 1.038165927F),
            new(0.2674655318F, 0.8627598882F, 0.9913372993F),
            new(-0.4293136597F, 0.813924551F, 1.070834517F),
        ],
        [
            new(-0F, 0F, 0.1991378814F),
            new(-0.9659041762F, 0.7939747572F, 1.0924716F),
            new(0.993514061F, 1.010968208F, 1.034318924F),
            new(0.3045202494F, 0.9112929106F, 1.038783193F),
            new(-0.499766916F, 0.8714680672F, 1.125948071F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgRec2020ExpandedRgb =
    [
        [
            new(-0F, 0F, 5.811532144e-09F),
            new(-0.02440484427F, 0.003904775251F, 0.09761937708F),
            new(0.9872887731F, 1.709457278F, 3.924108028F),
            new(0.0001432122663F, 0.05728491396F, 0.3681813478F),
            new(-0.000867519062F, 0.03123068623F, 3.134496927F),
        ],
        [
            new(-0F, 0F, 3.820037342e-11F),
            new(-0.005838641431F, 0.0009341826662F, 0.02335456572F),
            new(1.108318686F, 1.91901648F, 4.405157089F),
            new(7.505560643e-05F, 0.0300222449F, 0.1929588467F),
            new(-0.0006031820667F, 0.0217145551F, 2.179401398F),
        ],
        [
            new(-0F, 0F, 5.10003355e-16F),
            new(-0.0002392210299F, 3.827536784e-05F, 0.0009568841197F),
            new(1.434972167F, 2.484606028F, 5.703483582F),
            new(1.772578617e-05F, 0.007090314757F, 0.04557084292F),
            new(-0.0002678536985F, 0.009642733261F, 0.9678018689F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgRec2020CompressedRgb =
    [
        [
            new(-0F, 0F, 0.01485544164F),
            new(-0.7210807204F, 0.5231634378F, 0.8550863266F),
            new(1.000370741F, 1.01780951F, 1.041141629F),
            new(0.2414517254F, 0.8240243793F, 0.9537019134F),
            new(-0.3807266653F, 0.7675958276F, 1.027081609F),
        ],
        [
            new(-0F, 0F, 0.04722134769F),
            new(-0.8493649364F, 0.6696894765F, 0.978296876F),
            new(0.9975756407F, 1.015020609F, 1.038360238F),
            new(0.2690761983F, 0.865018785F, 0.9935390949F),
            new(-0.4331116676F, 0.817289412F, 1.074035883F),
        ],
        [
            new(-0F, 0F, 0.2073885351F),
            new(-1.006713271F, 0.8364598155F, 1.132751226F),
            new(0.9939977527F, 1.011450768F, 1.034800172F),
            new(0.3090798259F, 0.9168172479F, 1.044201016F),
            new(-0.5105798244F, 0.8796463609F, 1.133838058F),
        ],
    ];

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
    /// Checks that PQ and HLG working-space identity includes the display peak.
    /// </summary>
    [Fact]
    public void PqAndHlgWorkingSpaceEqualityIncludesPeakLuminance()
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
    /// Checks invalid luminance at the public PQ and HLG construction and companding boundaries.
    /// </summary>
    /// <param name="peak">The invalid display peak.</param>
    [Theory]
    [InlineData(0F)]
    [InlineData(-1F)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void PqAndHlgWorkingSpacesRejectInvalidPeakLuminance(float peak)
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
    /// Checks HLG RGB luminance adjustment with sRGB and Rec.2020 primaries at three display peaks.
    /// </summary>
    [Fact]
    public void HlgWorkingSpaceMatchesNative() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckHlgWorkingSpace,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks prepared HLG working spaces through all public color layouts with native RGB expectations.
    /// </summary>
    private static void CheckHlgWorkingSpace()
    {
        ReadOnlySpan<float> peaks = [255F, 1000F, 10000F];
        Vector3 sentinel = new(-123F, 456F, -789F);

        for (int space = 0; space < 2; space++)
        {
            RgbWorkingSpace primaries = space == 0 ? KnownRgbWorkingSpaces.SRgb : KnownRgbWorkingSpaces.Rec2020;
            for (int peakIndex = 0; peakIndex < peaks.Length; peakIndex++)
            {
                HlgWorkingSpace workingSpace = new(peaks[peakIndex], primaries.WhitePoint, primaries.ChromaticityCoordinates);
                for (int direction = 0; direction < 2; direction++)
                {
                    bool compress = direction != 0;
                    Vector3[] expected = space == 0
                        ? (compress ? NativeHlgCompressedRgb[peakIndex] : NativeHlgExpandedRgb[peakIndex])
                        : (compress ? NativeHlgRec2020CompressedRgb[peakIndex] : NativeHlgRec2020ExpandedRgb[peakIndex]);

                    // Thirty-three colors cover complete 512-, 256- and 128-bit groups and the final scalar color.
                    Vector3[] packed = new Vector3[34];
                    Vector4[] rgba = new Vector4[34];
                    for (int i = 0; i < 33; i++)
                    {
                        Vector3 input = NativeHlgInputs[i % NativeHlgInputs.Length];
                        packed[i] = input;
                        rgba[i] = new Vector4(input, HlgAlpha(i));
                        Vector4 scalar = compress ? workingSpace.Compress(rgba[i]) : workingSpace.Expand(rgba[i]);
                        AssertNativeHlgRgb(
                            expected[i % expected.Length],
                            scalar,
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={input}, scalar");

                        Assert.Equal(BitConverter.SingleToInt32Bits(rgba[i].W), BitConverter.SingleToInt32Bits(scalar.W));
                    }

                    packed[^1] = sentinel;
                    rgba[^1] = new Vector4(sentinel, -17F);
                    if (compress)
                    {
                        workingSpace.Compress(packed.AsSpan(0, 33));
                        workingSpace.Compress(rgba.AsSpan(0, 33));
                    }
                    else
                    {
                        workingSpace.Expand(packed.AsSpan(0, 33));
                        workingSpace.Expand(rgba.AsSpan(0, 33));
                    }

                    for (int i = 0; i < 33; i++)
                    {
                        Vector3 reference = expected[i % expected.Length];
                        AssertNativeHlgRgb(
                            reference,
                            new Vector4(packed[i], 0F),
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHlgInputs[i % NativeHlgInputs.Length]}, RGB");

                        AssertNativeHlgRgb(
                            reference,
                            rgba[i],
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHlgInputs[i % NativeHlgInputs.Length]}, RGBA");

                        Assert.Equal(BitConverter.SingleToInt32Bits(HlgAlpha(i)), BitConverter.SingleToInt32Bits(rgba[i].W));
                    }

                    Assert.Equal(sentinel, packed[^1]);
                    Assert.Equal(new Vector4(sentinel, -17F), rgba[^1]);
                }
            }
        }
    }

    /// <summary>
    /// Compares native RGB values with the existing power-approximation bound in normalized component units.
    /// </summary>
    /// <param name="expected">The original native result.</param>
    /// <param name="actual">The converted RGB color. Alpha is checked separately.</param>
    /// <param name="context">The curve, peak, input and public API layout.</param>
    private static void AssertNativeHlgRgb(Vector3 expected, Vector4 actual, string context)
    {
        for (int component = 0; component < 3; component++)
        {
            float scale = MathF.Max(1F, MathF.Abs(expected[component]));
            string message = $"{context}, component={component}, expected={expected[component]:R}, actual={actual[component]:R}, "
                + $"Vector128={Vector128.IsHardwareAccelerated}, Vector256={Vector256.IsHardwareAccelerated}, "
                + $"Vector512={Vector512.IsHardwareAccelerated}, FMA={Fma.IsSupported}";

            Assert.True(NativeTransferComparer.Equals(expected[component] / scale, actual[component] / scale), message);
        }
    }

    /// <summary>
    /// Supplies alpha values whose exact bits must survive each color transfer.
    /// </summary>
    /// <param name="index">The input color index.</param>
    /// <returns>Signed zero, fractional alpha or a NaN with a fixed payload.</returns>
    private static float HlgAlpha(int index)
        => (index % 3) switch
        {
            0 => -0F,
            1 => .375F,
            _ => BitConverter.Int32BitsToSingle(unchecked((int)0xFFC12345))
        };

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
