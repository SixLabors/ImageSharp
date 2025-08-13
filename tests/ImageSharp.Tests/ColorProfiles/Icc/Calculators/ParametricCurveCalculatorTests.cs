// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="ParametricCurveCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class ParametricCurveCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataTrc.ParametricCurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void ParametricCurveCalculator_WithCurveEntry_ReturnsResult(IccParametricCurveTagDataEntry curve, bool inverted, float input, float expected)
    {
        ParametricCurveCalculator calculator = new(curve, inverted);

        float result = calculator.Calculate(input);

        Assert.Equal(expected, result, 4f);
    }
}
