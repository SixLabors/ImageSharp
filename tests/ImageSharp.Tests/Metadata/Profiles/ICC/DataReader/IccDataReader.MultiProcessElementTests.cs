// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderMultiProcessElementTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void ReadMultiProcessElement(byte[] data, IccMultiProcessElement expected)
        {
            IccDataReader reader = CreateReader(data);

            IccMultiProcessElement output = reader.ReadMultiProcessElement();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void ReadCurveSetProcessElement(byte[] data, IccCurveSetProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = CreateReader(data);

            IccCurveSetProcessElement output = reader.ReadCurveSetProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void ReadMatrixProcessElement(byte[] data, IccMatrixProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = CreateReader(data);

            IccMatrixProcessElement output = reader.ReadMatrixProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void ReadClutProcessElement(byte[] data, IccClutProcessElement expected, int inChannelCount, int outChannelCount)
        {
            IccDataReader reader = CreateReader(data);

            IccClutProcessElement output = reader.ReadClutProcessElement(inChannelCount, outChannelCount);

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
