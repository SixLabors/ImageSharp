// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="ParametricCurveCalculator"/>
    /// </summary>
    public class ParametricCurveCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataTrc.ParametricCurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
        internal void ParametricCurveCalculator_WithCurveEntry_ReturnsResult(IccParametricCurveTagDataEntry curve, bool inverted, float input, float expected)
        {
            var calculator = new ParametricCurveCalculator(curve, inverted);

            float result = calculator.Calculate(input);

            Assert.Equal(expected, result, 4);
        }
    }
}