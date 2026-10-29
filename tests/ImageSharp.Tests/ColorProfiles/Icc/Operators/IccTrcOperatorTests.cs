// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Operators;

/// <summary>
/// Tests ICC <see cref="TrcOperator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class IccTrcOperatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataTrc.TrcArrayConversionTestData), MemberType = typeof(IccConversionDataTrc))]
    internal void IccTrcOperator_WithCurvesArray_ReturnsResult(IccTagDataEntry[] entries, bool inverted, Vector4 input, Vector4 expected)
    {
        TrcOperator calculator = new(entries, inverted);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
