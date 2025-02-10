// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="ClutCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class ClutCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataClut.ClutConversionTestData), MemberType = typeof(IccConversionDataClut))]
    internal void ClutCalculator_WithClut_ReturnsResult(IccClut lut, Vector4 input, Vector4 expected)
    {
        ClutCalculator calculator = new(lut);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
