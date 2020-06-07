// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataWriterTests
    {
        [Fact]
        public void WriteEmpty()
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteEmpty(4);
            byte[] output = writer.GetData();

            Assert.Equal(new byte[4], output);
        }

        [Theory]
        [InlineData(1, 4)]
        [InlineData(4, 4)]
        public void WritePadding(int writePosition, int expectedLength)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteEmpty(writePosition);
            writer.WritePadding();
            byte[] output = writer.GetData();

            Assert.Equal(new byte[expectedLength], output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.UInt8TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayUInt8(byte[] data, byte[] expected)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.UInt16TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayUInt16(byte[] expected, ushort[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.Int16TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayInt16(byte[] expected, short[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.UInt32TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayUInt32(byte[] expected, uint[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.Int32TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayInt32(byte[] expected, int[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataArray.UInt64TestData), MemberType = typeof(IccTestDataArray))]
        public void WriteArrayUInt64(byte[] expected, ulong[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteArray(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
