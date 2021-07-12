// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="LutCalculator"/>
    /// </summary>
    [Trait("Color", "Conversion")]
    public class LutCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataLut.LutConversionTestData), MemberType = typeof(IccConversionDataLut))]
        internal void LutCalculator_WithLut_ReturnsResult(float[] lut, bool inverted, float input, float expected)
        {
            var calculator = new LutCalculator(lut, inverted);

            float result = calculator.Calculate(input);

            Assert.Equal(expected, result, 4);
        }
    }
}
