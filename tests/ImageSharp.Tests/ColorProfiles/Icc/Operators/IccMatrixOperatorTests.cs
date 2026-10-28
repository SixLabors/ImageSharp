// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Operators;

/// <summary>
/// Tests ICC <see cref="MatrixOperator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class IccMatrixOperatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataMatrix.MatrixConversionTestData), MemberType = typeof(IccConversionDataMatrix))]
    internal void IccMatrixOperator_WithMatrix_ReturnsResult(Matrix4x4 matrix2D, Vector3 matrix1D, Vector4 input, Vector4 expected)
    {
        MatrixOperator calculator = new(matrix2D, matrix1D);

        Vector4 result = calculator.Convert(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
