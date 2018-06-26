// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderNonPrimitivesTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.DateTimeTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void ReadDateTime(byte[] data, DateTime expected)
        {
            IccDataReader reader = CreateReader(data);

            DateTime output = reader.ReadDateTime();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.VersionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void ReadVersionNumber(byte[] data, IccVersion expected)
        {
            IccDataReader reader = CreateReader(data);

            IccVersion output = reader.ReadVersionNumber();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.XyzNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        public void ReadXyzNumber(byte[] data, Vector3 expected)
        {
            IccDataReader reader = CreateReader(data);

            Vector3 output = reader.ReadXyzNumber();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ProfileIdTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadProfileId(byte[] data, IccProfileId expected)
        {
            IccDataReader reader = CreateReader(data);

            IccProfileId output = reader.ReadProfileId();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.PositionNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadPositionNumber(byte[] data, IccPositionNumber expected)
        {
            IccDataReader reader = CreateReader(data);

            IccPositionNumber output = reader.ReadPositionNumber();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ResponseNumberTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadResponseNumber(byte[] data, IccResponseNumber expected)
        {
            IccDataReader reader = CreateReader(data);

            IccResponseNumber output = reader.ReadResponseNumber();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.NamedColorTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadNamedColor(byte[] data, IccNamedColor expected, uint coordinateCount)
        {
            IccDataReader reader = CreateReader(data);

            IccNamedColor output = reader.ReadNamedColor(coordinateCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ProfileDescriptionReadTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadProfileDescription(byte[] data, IccProfileDescription expected)
        {
            IccDataReader reader = CreateReader(data);

            IccProfileDescription output = reader.ReadProfileDescription();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ColorantTableEntryTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadColorantTableEntry(byte[] data, IccColorantTableEntry expected)
        {
            IccDataReader reader = CreateReader(data);

            IccColorantTableEntry output = reader.ReadColorantTableEntry();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataNonPrimitives.ScreeningChannelTestData), MemberType = typeof(IccTestDataNonPrimitives))]
        internal void ReadScreeningChannel(byte[] data, IccScreeningChannel expected)
        {
            IccDataReader reader = CreateReader(data);

            IccScreeningChannel output = reader.ReadScreeningChannel();

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
