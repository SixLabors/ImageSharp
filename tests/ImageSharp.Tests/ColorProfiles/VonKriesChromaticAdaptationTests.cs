// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

public class VonKriesChromaticAdaptationTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);
    public static readonly TheoryData<CieXyz, CieXyz> WhitePoints = new()
    {
        { KnownIlluminants.D65, KnownIlluminants.D50 },
        { KnownIlluminants.D65, KnownIlluminants.D65 }
    };

    [Theory]
    [MemberData(nameof(WhitePoints))]
    public void SingleAndBulkTransformYieldIdenticalResults(CieXyz from, CieXyz to)
    {
        ColorConversionOptions options = new()
        {
            SourceWhitePoint = from,
            TargetWhitePoint = to
        };

        CieXyz input = new(1, 0, 1);
        CieXyz expected = VonKriesChromaticAdaptation.Transform(in input, (from, to), KnownChromaticAdaptationMatrices.Bradford);

        Span<CieXyz> inputSpan = new CieXyz[5];
        inputSpan.Fill(input);

        Span<CieXyz> actualSpan = new CieXyz[5];

        VonKriesChromaticAdaptation.Transform(inputSpan, actualSpan, (from, to), KnownChromaticAdaptationMatrices.Bradford);

        for (int i = 0; i < inputSpan.Length; i++)
        {
            Assert.Equal(expected, actualSpan[i], Comparer);
        }
    }

    /// <summary>
    /// Verifies mixed adaptation values across vector groups and a tail when conversion runs in place.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    public void BulkTransformSupportsMixedInPlaceValues(int count) => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMixedInPlaceAdaptation, count, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares aliased XYZ rows after disabled SIMD features are selected in separate processes before JIT compilation.
    /// </summary>
    /// <param name="serialized">The serialized row length.</param>
    private static void RunMixedInPlaceAdaptation(string serialized)
    {
        int count = FeatureTestRunner.Deserialize<int>(serialized);
        CieXyz[] values = new CieXyz[count];
        CieXyz[] expected = new CieXyz[values.Length];
        (CieXyz From, CieXyz To) whitePoints = (KnownIlluminants.D65, KnownIlluminants.D50);

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new CieXyz((i * .07F) - .2F, (i * .11F) + .03F, (i * .13F) - .1F);
            expected[i] = VonKriesChromaticAdaptation.Transform(in values[i], whitePoints, KnownChromaticAdaptationMatrices.Bradford);
        }

        VonKriesChromaticAdaptation.Transform(values, values, whitePoints, KnownChromaticAdaptationMatrices.Bradford);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(expected[i], values[i], Comparer);
        }
    }
}
