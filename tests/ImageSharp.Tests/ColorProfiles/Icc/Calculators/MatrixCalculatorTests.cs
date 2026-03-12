// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="MatrixCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class MatrixCalculatorTests
{
    [Theory]
    [MemberData(nameof(IccConversionDataMatrix.MatrixConversionTestData), MemberType = typeof(IccConversionDataMatrix))]
    internal void MatrixCalculator_WithMatrix_ReturnsResult(Matrix4x4 matrix2D, Vector3 matrix1D, Vector4 input, Vector4 expected)
    {
        MatrixCalculator calculator = new(matrix2D, matrix1D);

        Vector4 result = calculator.Calculate(input);

        VectorAssert.Equal(expected, result, 4);
    }
}
