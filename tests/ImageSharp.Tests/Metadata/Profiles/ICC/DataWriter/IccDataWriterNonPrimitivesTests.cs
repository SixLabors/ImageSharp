// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataWriterNonPrimitivesTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.DateTimeTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void WriteDateTime(byte[] expected, DateTime data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteDateTime(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.VersionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void WriteVersionNumber(byte[] expected, IccVersion data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteVersionNumber(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.XyzNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void WriteXyzNumber(byte[] expected, Vector3 data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteXyzNumber(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ProfileIdTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WriteProfileId(byte[] expected, IccProfileId data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteProfileId(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.PositionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WritePositionNumber(byte[] expected, IccPositionNumber data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WritePositionNumber(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ResponseNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WriteResponseNumber(byte[] expected, IccResponseNumber data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteResponseNumber(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.NamedColorTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WriteNamedColor(byte[] expected, IccNamedColor data, uint coordinateCount)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteNamedColor(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ProfileDescriptionWriteTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WriteProfileDescription(byte[] expected, IccProfileDescription data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteProfileDescription(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ScreeningChannelTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void WriteScreeningChannel(byte[] expected, IccScreeningChannel data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteScreeningChannel(data);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
