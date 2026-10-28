// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Operators;

/// <summary>
/// Tests ICC <see cref="LutColorOperator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class IccLutABOperatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataLutAB.LutAToBConversionTestData), MemberType = typeof(IccConversionDataLutAB))]
    internal void IccLutColorOperator_WithLutAToB_ReturnsResult(IccLutAToBTagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutColorOperator calculator = new(lut, false);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }

    [Theory]
    [MemberData(nameof(IccConversionDataLutAB.LutBToAConversionTestData), MemberType = typeof(IccConversionDataLutAB))]
    internal void IccLutColorOperator_WithLutBToA_ReturnsResult(IccLutBToATagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutColorOperator calculator = new(lut, false);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
