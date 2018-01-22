// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="LutCalculator"/>
    /// </summary>
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