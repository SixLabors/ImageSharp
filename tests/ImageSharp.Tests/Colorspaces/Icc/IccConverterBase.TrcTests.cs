namespace ImageSharp.Tests.Colorspaces.Icc
{
    using System.Collections.Generic;
    using Xunit;

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