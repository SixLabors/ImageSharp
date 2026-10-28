// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Operators;

/// <summary>
/// Tests ICC <see cref="CurveOperator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class IccParametricCurveOperatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataTrc.ParametricCurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void IccCurveOperator_WithCurveEntry_ReturnsResult(IccParametricCurveTagDataEntry curve, bool inverted, float input, float expected)
    {
        CurveOperator calculator = new(curve, inverted);

        float result = calculator.Convert(input);

        Assert.Equal(expected, result, 4f);
    }
}
