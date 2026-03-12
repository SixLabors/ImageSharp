// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="LutABCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class LutABCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataLutAB.LutAToBConversionTestData), MemberType = typeof(IccConversionDataLutAB))]
    internal void LutABCalculator_WithLutAToB_ReturnsResult(IccLutAToBTagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutABCalculator calculator = new(lut);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }

    [Theory]
    [MemberData(nameof(IccConversionDataLutAB.LutBToAConversionTestData), MemberType = typeof(IccConversionDataLutAB))]
    internal void LutABCalculator_WithLutBToA_ReturnsResult(IccLutBToATagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutABCalculator calculator = new(lut);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
