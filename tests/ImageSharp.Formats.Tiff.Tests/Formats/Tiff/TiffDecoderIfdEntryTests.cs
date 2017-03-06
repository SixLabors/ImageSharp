// <copyright file="TiffDecoderIfdEntryTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Linq;
    using Xunit;

    using ImageSharp.Formats;

    public class TiffDecoderIfdEntryTests
    {
        [Theory]
        [InlineDataAttribute(TiffType.Byte, 1u, 1u)]
        [InlineDataAttribute(TiffType.Ascii, 1u, 1u)]
        [InlineDataAttribute(TiffType.Short, 1u, 2u)]
        [InlineDataAttribute(TiffType.Long, 1u, 4u)]
        [InlineDataAttribute(TiffType.Rational, 1u, 8u)]
        [InlineDataAttribute(TiffType.SByte, 1u, 1u)]
        [InlineDataAttribute(TiffType.Undefined, 1u, 1u)]
        [InlineDataAttribute(TiffType.SShort, 1u, 2u)]
        [InlineDataAttribute(TiffType.SLong, 1u, 4u)]
        [InlineDataAttribute(TiffType.SRational, 1u, 8u)]
        [InlineDataAttribute(TiffType.Float, 1u, 4u)]
        [InlineDataAttribute(TiffType.Double, 1u, 8u)]
        [InlineDataAttribute(TiffType.Ifd, 1u, 4u)]
        [InlineDataAttribute((TiffType)999, 1u, 0u)]
        public void GetSizeOfData_SingleItem_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTags.ImageWidth, (TiffType)type, count, new byte[4]);
            uint size = TiffDecoderCore.GetSizeOfData(entry);
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte, 15u, 15u)]
        [InlineDataAttribute(TiffType.Ascii, 20u, 20u)]
        [InlineDataAttribute(TiffType.Short, 18u, 36u)]
        [InlineDataAttribute(TiffType.Long, 4u, 16u)]
        [InlineDataAttribute(TiffType.Rational, 9u, 72u)]
        [InlineDataAttribute(TiffType.SByte, 5u, 5u)]
        [InlineDataAttribute(TiffType.Undefined, 136u, 136u)]
        [InlineDataAttribute(TiffType.SShort, 12u, 24u)]
        [InlineDataAttribute(TiffType.SLong, 15u, 60u)]
        [InlineDataAttribute(TiffType.SRational, 10u, 80u)]
        [InlineDataAttribute(TiffType.Float, 2u, 8u)]
        [InlineDataAttribute(TiffType.Double, 2u, 16u)]
        [InlineDataAttribute(TiffType.Ifd, 10u, 40u)]
        [InlineDataAttribute((TiffType)999, 1050u, 0u)]
        public void GetSizeOfData_Array_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTags.ImageWidth, (TiffType)type, count, new byte[4]);
            uint size = TiffDecoderCore.GetSizeOfData(entry);
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte, 1u, new byte[] { 17 }, false)]
        [InlineDataAttribute(TiffType.Byte, 1u, new byte[] { 17 }, true)]
        [InlineDataAttribute(TiffType.Byte, 2u, new byte[] { 17, 28 }, false)]
        [InlineDataAttribute(TiffType.Byte, 2u, new byte[] { 17, 28 }, true)]
        [InlineDataAttribute(TiffType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute(TiffType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute(TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineDataAttribute(TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineDataAttribute(TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineDataAttribute(TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineDataAttribute(TiffType.Short, 1u, new byte[] { 17, 28 }, false)]
        [InlineDataAttribute(TiffType.Short, 1u, new byte[] { 17, 28 }, true)]
        [InlineDataAttribute(TiffType.Short, 2u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute(TiffType.Short, 2u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute(TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineDataAttribute(TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineDataAttribute(TiffType.Long, 1u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute(TiffType.Long, 1u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute(TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute(TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineDataAttribute(TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute(TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_ReturnsExpectedData(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, count, bytes), isLittleEndian);

            byte[] result = decoder.ReadBytes(ref entry);
            
            if (bytes.Length < 4)
                result = result.Take(bytes.Length).ToArray();

            Assert.Equal(bytes, result);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineDataAttribute(TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineDataAttribute(TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineDataAttribute(TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineDataAttribute(TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineDataAttribute(TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineDataAttribute(TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute(TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineDataAttribute(TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute(TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_CachesDataLongerThanFourBytes(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, count, bytes), isLittleEndian);

            Assert.Equal(4, entry.Value.Length);

            byte[] result = decoder.ReadBytes(ref entry);
            
            Assert.Equal(bytes.Length, entry.Value.Length);
            Assert.Equal(bytes, entry.Value);
        }

        private (TiffDecoderCore, TiffIfdEntry) GenerateTestIfdEntry(TiffGenEntry entry, bool isLittleEndian)
        {
            Stream stream = new TiffGenIfd()
                            {
                                Entries =
                                {
                                    entry
                                }
                            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null);
            TiffIfdEntry ifdEntry = decoder.ReadIfd(0).Entries[0];

            return (decoder, ifdEntry);
        }
    }
}