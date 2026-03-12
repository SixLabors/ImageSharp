// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="CurveCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class CurveCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataTrc.CurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void CurveCalculator_WithCurveEntry_ReturnsResult(IccCurveTagDataEntry curve, bool inverted, float input, float expected)
    {
        CurveCalculator calculator = new(curve, inverted);

        float result = calculator.Calculate(input);

        Assert.Equal(expected, result, 4f);
    }
}
