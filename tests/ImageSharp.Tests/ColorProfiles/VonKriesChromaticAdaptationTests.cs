// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

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
}
