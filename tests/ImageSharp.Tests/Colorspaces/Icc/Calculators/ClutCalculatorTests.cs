// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc.Calculators
{
    /// <summary>
    /// Tests ICC <see cref="ClutCalculator"/>
    /// </summary>
    public class ClutCalculatorTests
    {
        [Theory]
        [MemberData(nameof(IccConversionDataClut.ClutConversionTestData), MemberType = typeof(IccConversionDataClut))]
        internal void ClutCalculator_WithClut_ReturnsResult(IccClut lut, Vector4 input, Vector4 expected)
        {
            var calculator = new ClutCalculator(lut);

            Vector4 result = calculator.Calculate(input);

            VectorAssert.Equal(expected, result, 4);
        }
    }
}