// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderCurvesTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataCurves.OneDimensionalCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadOneDimensionalCurve(byte[] data, IccOneDimensionalCurve expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccOneDimensionalCurve output = reader.ReadOneDimensionalCurve();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.ResponseCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadResponseCurve(byte[] data, IccResponseCurve expected, int channelCount)
        {
            IccDataReader reader = this.CreateReader(data);

            IccResponseCurve output = reader.ReadResponseCurve(channelCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.ParametricCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadParametricCurve(byte[] data, IccParametricCurve expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccParametricCurve output = reader.ReadParametricCurve();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.CurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadCurveSegment(byte[] data, IccCurveSegment expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccCurveSegment output = reader.ReadCurveSegment();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.FormulaCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadFormulaCurveElement(byte[] data, IccFormulaCurveElement expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccFormulaCurveElement output = reader.ReadFormulaCurveElement();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.SampledCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void ReadSampledCurveElement(byte[] data, IccSampledCurveElement expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccSampledCurveElement output = reader.ReadSampledCurveElement();

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
