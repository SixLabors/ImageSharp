// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataWriterMultiProcessElementTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void WriteMultiProcessElement(byte[] expected, IccMultiProcessElement data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMultiProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void WriteCurveSetProcessElement(byte[] expected, IccCurveSetProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteCurveSetProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void WriteMatrixProcessElement(byte[] expected, IccMatrixProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMatrixProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void WriteClutProcessElement(byte[] expected, IccClutProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteClutProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
