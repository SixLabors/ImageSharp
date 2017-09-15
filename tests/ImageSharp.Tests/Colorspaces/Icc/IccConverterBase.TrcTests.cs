// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    /// <summary>
    /// Tests ICC conversion with tone reproduction curves
    /// </summary>
    public class IccConverterBaseTrcTests
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        [Theory]
        [MemberData(nameof(IccConversionDataTrc.TrcArrayConversionTestData), MemberType = typeof(IccConversionDataTrc))]
        internal void CalculateCurveArray(IccTagDataEntry[] entries, bool inverted, float[] input, float[] expected)
        {
            IccConverterBaseMock converter = CreateConverter();

            float[] result = converter.CalculateCurve(entries, inverted, input);

            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(IccConversionDataTrc.TrcSingleConversionTestData), MemberType = typeof(IccConversionDataTrc))]
        internal void CalculateCurveSingle(IccTagDataEntry curveEntry, bool inverted, float input, float expected)
        {
            IccConverterBaseMock converter = CreateConverter();

            float result = converter.CalculateCurve(curveEntry, inverted, input);

            Assert.Equal(expected, result, FloatRoundingComparer);
        }

        private IccConverterBaseMock CreateConverter()
        {
            return new IccConverterBaseMock();
        }
    }
}