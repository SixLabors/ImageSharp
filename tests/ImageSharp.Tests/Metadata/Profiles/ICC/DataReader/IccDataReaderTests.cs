// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader
{
    [Trait("Profile", "Icc")]
    public class IccDataReaderTests
    {
        [Fact]
        public void ConstructorThrowsNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new IccDataReader(null));
        }

        /// <summary>
        ///     Ignores tag entries that cannot contain a complete tag header.
        /// </summary>
        [Fact]
        public void ReadProfile_WithUndersizedArrayTags_IgnoresTags()
        {
            const int tagCount = 100;
            const int dataOffset = 132 + (tagCount * 12);
            byte[] data = new byte[dataOffset + 16];
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(128), tagCount);

            for (int i = 0; i < tagCount; i++)
            {
                Span<byte> entry = data.AsSpan(132 + (i * 12), 12);
                BinaryPrimitives.WriteUInt32BigEndian(entry, 0x73663332);
                BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(4, entry.Length - 4), dataOffset);
                BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(8, entry.Length - 8), 0);
            }

            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(dataOffset), 0x73663332);
            IccProfile profile = new(data);

            Assert.Empty(profile.Entries);
        }
    }
}
