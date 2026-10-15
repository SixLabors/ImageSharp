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
public class IccLutColorOperatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataLutEntry.Lut8ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
    internal void IccLutColorOperator_WithLut8_ReturnsResult(IccLut8TagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutColorOperator calculator = new(lut, false);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }

    [Theory]
    [MemberData(nameof(IccConversionDataLutEntry.Lut16ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
    internal void IccLutColorOperator_WithLut16_ReturnsResult(IccLut16TagDataEntry lut, Vector4 input, Vector4 expected)
    {
        LutColorOperator calculator = new(lut, false);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
