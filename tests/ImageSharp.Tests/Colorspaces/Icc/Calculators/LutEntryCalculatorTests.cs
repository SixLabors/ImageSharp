// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="LutEntryCalculator"/>
    /// </summary>
    public class LutEntryCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataLutEntry.Lut8ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
        internal void LutEntryCalculator_WithLut8_ReturnsResult(IccLut8TagDataEntry lut, Vector4 input, Vector4 expected)
        {
            var calculator = new LutEntryCalculator(lut);

            Vector4 result = calculator.Calculate(input);

            VectorAssert.Equal(expected, result, 4);
        }

        [Theory]
        [MemberData(nameof(IccConversionDataLutEntry.Lut16ConversionTestData), MemberType = typeof(IccConversionDataLutEntry))]
        internal void LutEntryCalculator_WithLut16_ReturnsResult(IccLut16TagDataEntry lut, Vector4 input, Vector4 expected)
        {
            var calculator = new LutEntryCalculator(lut);

            Vector4 result = calculator.Calculate(input);

            VectorAssert.Equal(expected, result, 4);
        }
    }
}