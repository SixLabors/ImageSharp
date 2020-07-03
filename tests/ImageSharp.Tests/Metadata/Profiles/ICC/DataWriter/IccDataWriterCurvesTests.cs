// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataWriterCurvesTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataCurves.OneDimensionalCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteOneDimensionalCurve(byte[] expected, IccOneDimensionalCurve data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteOneDimensionalCurve(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.ResponseCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteResponseCurve(byte[] expected, IccResponseCurve data, int channelCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteResponseCurve(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.ParametricCurveTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteParametricCurve(byte[] expected, IccParametricCurve data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteParametricCurve(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.CurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteCurveSegment(byte[] expected, IccCurveSegment data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteCurveSegment(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.FormulaCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteFormulaCurveElement(byte[] expected, IccFormulaCurveElement data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteFormulaCurveElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataCurves.SampledCurveSegmentTestData), MemberType = typeof(IccTestDataCurves))]
        internal void WriteSampledCurveElement(byte[] expected, IccSampledCurveElement data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteSampledCurveElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
