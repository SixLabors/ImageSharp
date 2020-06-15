// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="CurveCalculator"/>
    /// </summary>
    public class CurveCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataTrc.CurveConversionTestData), MemberType = typeof(IccConversionDataTrc))]
        internal void CurveCalculator_WithCurveEntry_ReturnsResult(IccCurveTagDataEntry curve, bool inverted, float input, float expected)
        {
            var calculator = new CurveCalculator(curve, inverted);

            float result = calculator.Calculate(input);

            Assert.Equal(expected, result, 4);
        }
    }
}