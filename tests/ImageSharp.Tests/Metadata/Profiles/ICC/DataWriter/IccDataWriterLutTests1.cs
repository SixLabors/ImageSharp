// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataWriterLutTests1
    {
        [Theory]
        [MemberData(nameof(IccTestDataLut.ClutTestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteClutAll(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, bool isFloat)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteClut(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Clut8TestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteClut8(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteClut8(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Clut16TestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteClut16(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteClut16(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.ClutF32TestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteClutF32(byte[] expected, IccClut data, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteClutF32(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Lut8TestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteLut8(byte[] expected, IccLut data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteLut8(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Lut16TestData), MemberType = typeof(IccTestDataLut))]
        internal void WriteLut16(byte[] expected, IccLut data, int count)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteLut16(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
