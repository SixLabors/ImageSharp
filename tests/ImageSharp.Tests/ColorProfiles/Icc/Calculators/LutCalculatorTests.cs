// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="LutCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class LutCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataLut.LutConversionTestData), MemberType = typeof(IccConversionDataLut))]
    internal void LutCalculator_WithLut_ReturnsResult(float[] lut, bool inverted, float input, float expected)
    {
        LutCalculator calculator = new(lut, inverted);

        float result = calculator.Calculate(input);

        Assert.Equal(expected, result, 4f);
    }
}
