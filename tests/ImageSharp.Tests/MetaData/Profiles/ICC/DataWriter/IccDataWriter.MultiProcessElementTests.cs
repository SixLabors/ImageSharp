// <copyright file="IccDataWriter.MultiProcessElementTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Icc
{
    using Xunit;

    public class IccDataWriterMultiProcessElementTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.MultiProcessElementTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void WriteMultiProcessElement(byte[] expected, IccMultiProcessElement data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMultiProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.CurveSetTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void WriteCurveSetProcessElement(byte[] expected, IccCurveSetProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteCurveSetProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.MatrixTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void WriteMatrixProcessElement(byte[] expected, IccMatrixProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrixProcessElement(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMultiProcessElement.ClutTestData), MemberType = typeof(IccTestDataMultiProcessElement))]
        internal void WriteClutProcessElement(byte[] expected, IccClutProcessElement data, int inChannelCount, int outChannelCount)
        {
            IccDataWriter writer = CreateWriter();

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
