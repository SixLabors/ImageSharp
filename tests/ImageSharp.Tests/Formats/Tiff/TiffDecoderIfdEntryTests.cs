// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffDecoderIfdEntryTests
    {
        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, 1u, 1u)]
        [InlineDataAttribute((ushort)TiffType.Ascii, 1u, 1u)]
        [InlineDataAttribute((ushort)TiffType.Short, 1u, 2u)]
        [InlineDataAttribute((ushort)TiffType.Long, 1u, 4u)]
        [InlineDataAttribute((ushort)TiffType.Rational, 1u, 8u)]
        [InlineDataAttribute((ushort)TiffType.SByte, 1u, 1u)]
        [InlineDataAttribute((ushort)TiffType.Undefined, 1u, 1u)]
        [InlineDataAttribute((ushort)TiffType.SShort, 1u, 2u)]
        [InlineDataAttribute((ushort)TiffType.SLong, 1u, 4u)]
        [InlineDataAttribute((ushort)TiffType.SRational, 1u, 8u)]
        [InlineDataAttribute((ushort)TiffType.Float, 1u, 4u)]
        [InlineDataAttribute((ushort)TiffType.Double, 1u, 8u)]
        [InlineDataAttribute((ushort)TiffType.Ifd, 1u, 4u)]
        [InlineDataAttribute((ushort)999, 1u, 0u)]
        public void GetSizeOfData_SingleItem_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTags.ImageWidth, (TiffType)type, count, new byte[4]);
            uint size = TiffDecoderCore.GetSizeOfData(entry);
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, 15u, 15u)]
        [InlineDataAttribute((ushort)TiffType.Ascii, 20u, 20u)]
        [InlineDataAttribute((ushort)TiffType.Short, 18u, 36u)]
        [InlineDataAttribute((ushort)TiffType.Long, 4u, 16u)]
        [InlineDataAttribute((ushort)TiffType.Rational, 9u, 72u)]
        [InlineDataAttribute((ushort)TiffType.SByte, 5u, 5u)]
        [InlineDataAttribute((ushort)TiffType.Undefined, 136u, 136u)]
        [InlineDataAttribute((ushort)TiffType.SShort, 12u, 24u)]
        [InlineDataAttribute((ushort)TiffType.SLong, 15u, 60u)]
        [InlineDataAttribute((ushort)TiffType.SRational, 10u, 80u)]
        [InlineDataAttribute((ushort)TiffType.Float, 2u, 8u)]
        [InlineDataAttribute((ushort)TiffType.Double, 2u, 16u)]
        [InlineDataAttribute((ushort)TiffType.Ifd, 10u, 40u)]
        [InlineDataAttribute((ushort)999, 1050u, 0u)]
        public void GetSizeOfData_Array_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTags.ImageWidth, (TiffType)type, count, new byte[4]);
            uint size = TiffDecoderCore.GetSizeOfData(entry);
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, 1u, new byte[] { 17 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 1u, new byte[] { 17 }, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, 2u, new byte[] { 17, 28 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 2u, new byte[] { 17, 28 }, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineDataAttribute((ushort)TiffType.Short, 1u, new byte[] { 17, 28 }, false)]
        [InlineDataAttribute((ushort)TiffType.Short, 1u, new byte[] { 17, 28 }, true)]
        [InlineDataAttribute((ushort)TiffType.Short, 2u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute((ushort)TiffType.Short, 2u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute((ushort)TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineDataAttribute((ushort)TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineDataAttribute((ushort)TiffType.Long, 1u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineDataAttribute((ushort)TiffType.Long, 1u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineDataAttribute((ushort)TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute((ushort)TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineDataAttribute((ushort)TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute((ushort)TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_ReturnsExpectedData(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, count, bytes), isLittleEndian);

            byte[] result = decoder.ReadBytes(ref entry);

            if (bytes.Length < 4)
            {
                result = result.Take(bytes.Length).ToArray();
            }

            Assert.Equal(bytes, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineDataAttribute((ushort)TiffType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineDataAttribute((ushort)TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineDataAttribute((ushort)TiffType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineDataAttribute((ushort)TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute((ushort)TiffType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineDataAttribute((ushort)TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineDataAttribute((ushort)TiffType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_CachesDataLongerThanFourBytes(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, count, bytes), isLittleEndian);

            Assert.Equal(4, entry.Value.Length);

            byte[] result = decoder.ReadBytes(ref entry);

            Assert.Equal(bytes.Length, entry.Value.Length);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Byte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Byte, true, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineDataAttribute((ushort)TiffType.Byte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Byte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Byte, false, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineDataAttribute((ushort)TiffType.Short, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Short, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Short, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineDataAttribute((ushort)TiffType.Short, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineDataAttribute((ushort)TiffType.Short, true, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineDataAttribute((ushort)TiffType.Short, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Short, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Short, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineDataAttribute((ushort)TiffType.Short, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineDataAttribute((ushort)TiffType.Short, false, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute((ushort)TiffType.Long, true, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineDataAttribute((ushort)TiffType.Long, false, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        public void ReadUnsignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, uint expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, bytes), isLittleEndian);

            uint result = decoder.ReadUnsignedInteger(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadUnsignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, true)]
        [InlineDataAttribute((ushort)TiffType.Short, true)]
        [InlineDataAttribute((ushort)TiffType.Long, true)]
        [InlineDataAttribute((ushort)TiffType.Byte, false)]
        [InlineDataAttribute((ushort)TiffType.Short, false)]
        [InlineDataAttribute((ushort)TiffType.Long, false)]
        public void ReadUnsignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.SByte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SByte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SByte, true, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineDataAttribute((ushort)TiffType.SByte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SByte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SByte, false, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineDataAttribute((ushort)TiffType.SShort, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SShort, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SShort, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineDataAttribute((ushort)TiffType.SShort, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineDataAttribute((ushort)TiffType.SShort, true, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineDataAttribute((ushort)TiffType.SShort, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SShort, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SShort, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineDataAttribute((ushort)TiffType.SShort, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineDataAttribute((ushort)TiffType.SShort, false, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute((ushort)TiffType.SLong, true, new byte[] { 255, 255, 255, 255 }, -1)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineDataAttribute((ushort)TiffType.SLong, false, new byte[] { 255, 255, 255, 255 }, -1)]
        public void ReadSignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, int expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, bytes), isLittleEndian);

            int result = decoder.ReadSignedInteger(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadSignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a signed integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.SByte, true)]
        [InlineDataAttribute((ushort)TiffType.SShort, true)]
        [InlineDataAttribute((ushort)TiffType.SLong, true)]
        [InlineDataAttribute((ushort)TiffType.SByte, false)]
        [InlineDataAttribute((ushort)TiffType.SShort, false)]
        [InlineDataAttribute((ushort)TiffType.SLong, false)]
        public void ReadSignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte, 1, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineDataAttribute((ushort)TiffType.Byte, 3, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineDataAttribute((ushort)TiffType.Byte, 7, true, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute((ushort)TiffType.Byte, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineDataAttribute((ushort)TiffType.Byte, 3, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineDataAttribute((ushort)TiffType.Byte, 7, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute((ushort)TiffType.Short, 1, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1 })]
        [InlineDataAttribute((ushort)TiffType.Short, 2, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1, 515 })]
        [InlineDataAttribute((ushort)TiffType.Short, 3, true, new byte[] { 1, 0, 3, 2, 5, 4, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineDataAttribute((ushort)TiffType.Short, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1 })]
        [InlineDataAttribute((ushort)TiffType.Short, 2, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1, 515 })]
        [InlineDataAttribute((ushort)TiffType.Short, 3, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineDataAttribute((ushort)TiffType.Long, 1, true, new byte[] { 4, 3, 2, 1 }, new uint[] { 0x01020304 })]
        [InlineDataAttribute((ushort)TiffType.Long, 2, true, new byte[] { 4, 3, 2, 1, 6, 5, 4, 3, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        [InlineDataAttribute((ushort)TiffType.Long, 1, false, new byte[] { 1, 2, 3, 4 }, new uint[] { 0x01020304 })]
        [InlineDataAttribute((ushort)TiffType.Long, 2, false, new byte[] { 1, 2, 3, 4, 3, 4, 5, 6, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        public void ReadUnsignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, uint[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            uint[] result = decoder.ReadUnsignedIntegerArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadUnsignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.SByte, 1, true, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineDataAttribute((ushort)TiffType.SByte, 3, true, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineDataAttribute((ushort)TiffType.SByte, 7, true, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute((ushort)TiffType.SByte, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineDataAttribute((ushort)TiffType.SByte, 3, false, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineDataAttribute((ushort)TiffType.SByte, 7, false, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 1, true, new byte[] { 1, 0, 3, 2 }, new int[] { 1 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 2, true, new byte[] { 1, 0, 255, 255 }, new int[] { 1, -1 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 3, true, new byte[] { 1, 0, 255, 255, 5, 4, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 1 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 2, false, new byte[] { 0, 1, 255, 255 }, new int[] { 1, -1 })]
        [InlineDataAttribute((ushort)TiffType.SShort, 3, false, new byte[] { 0, 1, 255, 255, 4, 5, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineDataAttribute((ushort)TiffType.SLong, 1, true, new byte[] { 4, 3, 2, 1 }, new int[] { 0x01020304 })]
        [InlineDataAttribute((ushort)TiffType.SLong, 2, true, new byte[] { 4, 3, 2, 1, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        [InlineDataAttribute((ushort)TiffType.SLong, 1, false, new byte[] { 1, 2, 3, 4 }, new int[] { 0x01020304 })]
        [InlineDataAttribute((ushort)TiffType.SLong, 2, false, new byte[] { 1, 2, 3, 4, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        public void ReadSignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, int[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            int[] result = decoder.ReadSignedIntegerArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadSignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a signed integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { 0 }, "")]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 }, "ABC")]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', 0 }, "ABCDEF")]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H', 0 }, "ABCD\0EFGH")]
        [InlineDataAttribute(false, new byte[] { 0 }, "")]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 }, "ABC")]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', 0 }, "ABCDEF")]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H', 0 }, "ABCD\0EFGH")]
        public void ReadString_ReturnsValue(bool isLittleEndian, byte[] bytes, string expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Ascii, (uint)bytes.Length, bytes), isLittleEndian);

            string result = decoder.ReadString(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadString_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadString(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a string.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { (byte)'A' })]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C' })]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' })]
        [InlineDataAttribute(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H' })]
        [InlineDataAttribute(false, new byte[] { (byte)'A' })]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C' })]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' })]
        [InlineDataAttribute(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H' })]
        public void ReadString_ThrowsExceptionIfStringIsNotNullTerminated(bool isLittleEndian, byte[] bytes)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Ascii, (uint)bytes.Length, bytes), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadString(ref entry));

            Assert.Equal($"The retrieved string is not null terminated.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, 0, 2)]
        [InlineDataAttribute(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, 0, 2)]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, 1, 2)]
        public void ReadUnsignedRational_ReturnsValue(bool isLittleEndian, byte[] bytes, uint expectedNumerator, uint expectedDenominator)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Rational, 1, bytes), isLittleEndian);

            Rational result = decoder.ReadUnsignedRational(ref entry);
            Rational expectedValue = new Rational(expectedNumerator, expectedDenominator);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, 0, 2)]
        [InlineDataAttribute(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        [InlineDataAttribute(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, -1, 2)]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, 0, 2)]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, 1, 2)]
        [InlineDataAttribute(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2 }, -1, 2)]
        public void ReadSignedRational_ReturnsValue(bool isLittleEndian, byte[] bytes, int expectedNumerator, int expectedDenominator)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.SRational, 1, bytes), isLittleEndian);

            SignedRational result = decoder.ReadSignedRational(ref entry);
            SignedRational expectedValue = new SignedRational(expectedNumerator, expectedDenominator);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, new uint[] { 0 }, new uint[] { 2 })]
        [InlineDataAttribute(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new uint[] { 1 }, new uint[] { 2 })]
        [InlineDataAttribute(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new uint[] { 1, 2 }, new uint[] { 2, 3 })]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, new uint[] { 0 }, new uint[] { 2 })]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, new uint[] { 1 }, new uint[] { 2 })]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3 }, new uint[] { 1, 2 }, new uint[] { 2, 3 })]
        public void ReadUnsignedRationalArray_ReturnsValue(bool isLittleEndian, byte[] bytes, uint[] expectedNumerators, uint[] expectedDenominators)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Rational, (uint)expectedNumerators.Length, bytes), isLittleEndian);

            Rational[] result = decoder.ReadUnsignedRationalArray(ref entry);
            Rational[] expectedValue = Enumerable.Range(0, expectedNumerators.Length).Select(i => new Rational(expectedNumerators[i], expectedDenominators[i])).ToArray();

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, new int[] { 0 }, new int[] { 2 })]
        [InlineDataAttribute(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new int[] { 1 }, new int[] { 2 })]
        [InlineDataAttribute(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, new int[] { -1 }, new int[] { 2 })]
        [InlineDataAttribute(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new int[] { -1, 2 }, new int[] { 2, 3 })]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, new int[] { 0 }, new int[] { 2 })]
        [InlineDataAttribute(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, new int[] { 1 }, new int[] { 2 })]
        [InlineDataAttribute(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2 }, new int[] { -1 }, new int[] { 2 })]
        [InlineDataAttribute(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3 }, new int[] { -1, 2 }, new int[] { 2, 3 })]
        public void ReadSignedRationalArray_ReturnsValue(bool isLittleEndian, byte[] bytes, int[] expectedNumerators, int[] expectedDenominators)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.SRational, (uint)expectedNumerators.Length, bytes), isLittleEndian);

            SignedRational[] result = decoder.ReadSignedRationalArray(ref entry);
            SignedRational[] expectedValue = Enumerable.Range(0, expectedNumerators.Length).Select(i => new SignedRational(expectedNumerators[i], expectedDenominators[i])).ToArray();

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadUnsignedRational_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRational(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a Rational.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadSignedRational_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRational(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a SignedRational.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadUnsignedRationalArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRationalArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a Rational.", e.Message);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadSignedRationalArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRationalArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a SignedRational.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false)]
        [InlineDataAttribute(true)]
        public void ReadUnsignedRational_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Rational, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRational(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false)]
        [InlineDataAttribute(true)]
        public void ReadSignedRational_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.SRational, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRational(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0.0F)]
        [InlineDataAttribute(false, new byte[] { 0x3F, 0x80, 0x00, 0x00 }, 1.0F)]
        [InlineDataAttribute(false, new byte[] { 0xC0, 0x00, 0x00, 0x00 }, -2.0F)]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0x7F, 0xFF, 0xFF }, float.MaxValue)]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0x80, 0x00, 0x00 }, float.PositiveInfinity)]
        [InlineDataAttribute(false, new byte[] { 0xFF, 0x80, 0x00, 0x00 }, float.NegativeInfinity)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0.0F)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0x3F }, 1.0F)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0xC0 }, -2.0F)]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, float.MaxValue)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0x7F }, float.PositiveInfinity)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0xFF }, float.NegativeInfinity)]
        public void ReadFloat_ReturnsValue(bool isLittleEndian, byte[] bytes, float expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Float, 1, bytes), isLittleEndian);

            float result = decoder.ReadFloat(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadFloat_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloat(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a float.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false)]
        [InlineDataAttribute(true)]
        public void ReadFloat_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Float, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloat(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00 }, new float[] { 0.0F })]
        [InlineDataAttribute(false, new byte[] { 0x3F, 0x80, 0x00, 0x00 }, new float[] { 1.0F })]
        [InlineDataAttribute(false, new byte[] { 0xC0, 0x00, 0x00, 0x00 }, new float[] { -2.0F })]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0x7F, 0xFF, 0xFF }, new float[] { float.MaxValue })]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0x80, 0x00, 0x00 }, new float[] { float.PositiveInfinity })]
        [InlineDataAttribute(false, new byte[] { 0xFF, 0x80, 0x00, 0x00 }, new float[] { float.NegativeInfinity })]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x3F, 0x80, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00 }, new float[] { 0.0F, 1.0F, -2.0F })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00 }, new float[] { 0.0F })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0x3F }, new float[] { 1.0F })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0xC0 }, new float[] { -2.0F })]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, new float[] { float.MaxValue })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0x7F }, new float[] { float.PositiveInfinity })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x80, 0xFF }, new float[] { float.NegativeInfinity })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0xC0 }, new float[] { 0.0F, 1.0F, -2.0F })]

        public void ReadFloatArray_ReturnsValue(bool isLittleEndian, byte[] bytes, float[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Float, (uint)expectedValue.Length, bytes), isLittleEndian);

            float[] result = decoder.ReadFloatArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Double)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadFloatArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloatArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a float.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0.0)]
        [InlineDataAttribute(false, new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 1.0)]
        [InlineDataAttribute(false, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 2.0)]
        [InlineDataAttribute(false, new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, -2.0)]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, double.MaxValue)]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, double.PositiveInfinity)]
        [InlineDataAttribute(false, new byte[] { 0xFF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, double.NegativeInfinity)]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, double.NaN)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0.0)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, 1.0)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, 2.0)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, -2.0)]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, double.MaxValue)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, double.PositiveInfinity)]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, double.NegativeInfinity)]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, double.NaN)]
        public void ReadDouble_ReturnsValue(bool isLittleEndian, byte[] bytes, double expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Double, 1, bytes), isLittleEndian);

            double result = decoder.ReadDouble(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadDouble_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDouble(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a double.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false)]
        [InlineDataAttribute(true)]
        public void ReadDouble_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Double, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDouble(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0 })]
        [InlineDataAttribute(false, new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 1.0 })]
        [InlineDataAttribute(false, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 2.0 })]
        [InlineDataAttribute(false, new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { -2.0 })]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, new double[] { double.MaxValue })]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { double.PositiveInfinity })]
        [InlineDataAttribute(false, new byte[] { 0xFF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { double.NegativeInfinity })]
        [InlineDataAttribute(false, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, new double[] { double.NaN })]
        [InlineDataAttribute(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0, 1.0, -2.0 })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0 })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, new double[] { 1.0 })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, new double[] { 2.0 })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { -2.0 })]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, new double[] { double.MaxValue })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, new double[] { double.PositiveInfinity })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, new double[] { double.NegativeInfinity })]
        [InlineDataAttribute(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, new double[] { double.NaN })]
        [InlineDataAttribute(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { 0.0, 1.0, -2.0 })]
        public void ReadDoubleArray_ReturnsValue(bool isLittleEndian, byte[] bytes, double[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, TiffType.Double, (uint)expectedValue.Length, bytes), isLittleEndian);

            double[] result = decoder.ReadDoubleArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute((ushort)TiffType.Byte)]
        [InlineDataAttribute((ushort)TiffType.Ascii)]
        [InlineDataAttribute((ushort)TiffType.Short)]
        [InlineDataAttribute((ushort)TiffType.Long)]
        [InlineDataAttribute((ushort)TiffType.Rational)]
        [InlineDataAttribute((ushort)TiffType.SByte)]
        [InlineDataAttribute((ushort)TiffType.Undefined)]
        [InlineDataAttribute((ushort)TiffType.SShort)]
        [InlineDataAttribute((ushort)TiffType.SLong)]
        [InlineDataAttribute((ushort)TiffType.SRational)]
        [InlineDataAttribute((ushort)TiffType.Float)]
        [InlineDataAttribute((ushort)TiffType.Ifd)]
        [InlineDataAttribute((ushort)99)]
        public void ReadDoubleArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDoubleArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a double.", e.Message);
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

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfdEntry ifdEntry = decoder.ReadIfd(0).Entries[0];

            return (decoder, ifdEntry);
        }
    }
}
