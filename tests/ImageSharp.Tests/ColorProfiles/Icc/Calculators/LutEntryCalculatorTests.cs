// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="LutEntryCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class LutEntryCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataLutEntry.Lut8ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
    internal void LutEntryCalculator_WithLut8_ReturnsResult(IccLut8TagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutEntryCalculator calculator = new(lut);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }

    [Theory]
    [MemberData(nameof(IccConversionDataLutEntry.Lut16ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
    internal void LutEntryCalculator_WithLut16_ReturnsResult(IccLut16TagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutEntryCalculator calculator = new(lut);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
