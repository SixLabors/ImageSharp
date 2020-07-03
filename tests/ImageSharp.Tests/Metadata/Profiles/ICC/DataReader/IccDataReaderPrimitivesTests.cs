// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderPrimitivesTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataPrimitives.AsciiTestData), MemberType = typeof(IccTestDataPrimitives))]
        public void ReadAsciiString(byte[] textBytes, int length, string expected)
        {
            IccDataReader reader = this.CreateReader(textBytes);

            string output = reader.ReadAsciiString(length);

            Assert.Equal(expected, output);
        }

        [Fact]
        public void ReadAsciiStringWithNegativeLengthThrowsArgumentException()
        {
            IccDataReader reader = this.CreateReader(new byte[4]);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadAsciiString(-1));
        }

        [Fact]
        public void ReadUnicodeStringWithNegativeLengthThrowsArgumentException()
        {
            IccDataReader reader = this.CreateReader(new byte[4]);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUnicodeString(-1));
        }

        [Theory]
        [MemberData(nameof(IccTestDataPrimitives.Fix16TestData), MemberType = typeof(IccTestDataPrimitives))]
        public void ReadFix16(byte[] data, float expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float output = reader.ReadFix16();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataPrimitives.UFix16TestData), MemberType = typeof(IccTestDataPrimitives))]
        public void ReadUFix16(byte[] data, float expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float output = reader.ReadUFix16();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataPrimitives.U1Fix15TestData), MemberType = typeof(IccTestDataPrimitives))]
        public void ReadU1Fix15(byte[] data, float expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float output = reader.ReadU1Fix15();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataPrimitives.UFix8TestData), MemberType = typeof(IccTestDataPrimitives))]
        public void ReadUFix8(byte[] data, float expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float output = reader.ReadUFix8();

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
