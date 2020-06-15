// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="TrcCalculator"/>
    /// </summary>
    public class TrcCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataTrc.TrcArrayConversionTestData), MemberType = typeof(IccConversionDataTrc))]
        internal void TrcCalculator_WithCurvesArray_ReturnsResult(IccTagDataEntry[] entries, bool inverted, Vector4 input, Vector4 expected)
        {
            var calculator = new TrcCalculator(entries, inverted);

            Vector4 result = calculator.Calculate(input);

            VectorAssert.Equal(expected, result, 4);
        }
    }
}