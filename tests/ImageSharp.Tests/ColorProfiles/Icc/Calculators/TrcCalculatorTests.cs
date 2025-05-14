// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="TrcCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class TrcCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataTrc.TrcArrayConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void TrcCalculator_WithCurvesArray_ReturnsResult(IccTagDataEntry[] entries, bool inverted, Vector4 input, Vector4 expected)
    {
        TrcCalculator calculator = new(entries, inverted);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
