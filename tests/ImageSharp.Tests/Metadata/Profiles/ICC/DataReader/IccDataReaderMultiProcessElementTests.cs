// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderMultiProcessElementTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void ReadMultiProcessElement(byte[] data, IccMultiProcessElement expected)
        {
            IccDataReader reader = this.CreateReader(data);

            IccMultiProcessElement output = reader.ReadMultiProcessElement();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void ReadCurveSetProcessElement(byte[] data, IccCurveSetProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = this.CreateReader(data);

            IccCurveSetProcessElement output = reader.ReadCurveSetProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void ReadMatrixProcessElement(byte[] data, IccMatrixProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = this.CreateReader(data);

            IccMatrixProcessElement output = reader.ReadMatrixProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElements.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElements))]
        internal void ReadClutProcessElement(byte[] data, IccClutProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = this.CreateReader(data);

            IccClutProcessElement output = reader.ReadClutProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
