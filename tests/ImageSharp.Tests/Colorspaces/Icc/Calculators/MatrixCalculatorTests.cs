// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="MatrixCalculator"/>
    /// </summary>
    public class MatrixCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataMatrix.MatrixConversionTestData), MemberType = typeof(IccConversionDataMatrix))]
        internal void MatrixCalculator_WithMatrix_ReturnsResult(Matrix4x4 matrix2D, Vector3 matrix1D, Vector4 input, Vector4 expected)
        {
            var calculator = new MatrixCalculator(matrix2D, matrix1D);

            Vector4 result = calculator.Calculate(input);

            VectorAssert.Equal(expected, result, 4);
        }
    }
}