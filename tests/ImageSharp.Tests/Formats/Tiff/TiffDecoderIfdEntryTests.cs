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
        [InlineData((ushort)TiffTagType.Byte, 1u, 1u)]
        [InlineData((ushort)TiffTagType.Ascii, 1u, 1u)]
        [InlineData((ushort)TiffTagType.Short, 1u, 2u)]
        [InlineData((ushort)TiffTagType.Long, 1u, 4u)]
        [InlineData((ushort)TiffTagType.Rational, 1u, 8u)]
        [InlineData((ushort)TiffTagType.SByte, 1u, 1u)]
        [InlineData((ushort)TiffTagType.Undefined, 1u, 1u)]
        [InlineData((ushort)TiffTagType.SShort, 1u, 2u)]
        [InlineData((ushort)TiffTagType.SLong, 1u, 4u)]
        [InlineData((ushort)TiffTagType.SRational, 1u, 8u)]
        [InlineData((ushort)TiffTagType.Float, 1u, 4u)]
        [InlineData((ushort)TiffTagType.Double, 1u, 8u)]
        [InlineData((ushort)TiffTagType.Ifd, 1u, 4u)]
        [InlineData((ushort)999, 1u, 0u)]
        public void GetSizeOfData_SingleItem_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTagId.ImageWidth, (TiffTagType)type, count, new byte[4]);
            uint size = entry.SizeOfData;
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, 15u, 15u)]
        [InlineData((ushort)TiffTagType.Ascii, 20u, 20u)]
        [InlineData((ushort)TiffTagType.Short, 18u, 36u)]
        [InlineData((ushort)TiffTagType.Long, 4u, 16u)]
        [InlineData((ushort)TiffTagType.Rational, 9u, 72u)]
        [InlineData((ushort)TiffTagType.SByte, 5u, 5u)]
        [InlineData((ushort)TiffTagType.Undefined, 136u, 136u)]
        [InlineData((ushort)TiffTagType.SShort, 12u, 24u)]
        [InlineData((ushort)TiffTagType.SLong, 15u, 60u)]
        [InlineData((ushort)TiffTagType.SRational, 10u, 80u)]
        [InlineData((ushort)TiffTagType.Float, 2u, 8u)]
        [InlineData((ushort)TiffTagType.Double, 2u, 16u)]
        [InlineData((ushort)TiffTagType.Ifd, 10u, 40u)]
        [InlineData((ushort)999, 1050u, 0u)]
        public void GetSizeOfData_Array_ReturnsCorrectSize(ushort type, uint count, uint expectedSize)
        {
            TiffIfdEntry entry = new TiffIfdEntry(TiffTagId.ImageWidth, (TiffTagType)type, count, new byte[4]);
            uint size = entry.SizeOfData;
            Assert.Equal(expectedSize, size);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, 1u, new byte[] { 17 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 1u, new byte[] { 17 }, true)]
        [InlineData((ushort)TiffTagType.Byte, 2u, new byte[] { 17, 28 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 2u, new byte[] { 17, 28 }, true)]
        [InlineData((ushort)TiffTagType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 4u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineData((ushort)TiffTagType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineData((ushort)TiffTagType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineData((ushort)TiffTagType.Short, 1u, new byte[] { 17, 28 }, false)]
        [InlineData((ushort)TiffTagType.Short, 1u, new byte[] { 17, 28 }, true)]
        [InlineData((ushort)TiffTagType.Short, 2u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineData((ushort)TiffTagType.Short, 2u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineData((ushort)TiffTagType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineData((ushort)TiffTagType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineData((ushort)TiffTagType.Long, 1u, new byte[] { 17, 28, 2, 9 }, false)]
        [InlineData((ushort)TiffTagType.Long, 1u, new byte[] { 17, 28, 2, 9 }, true)]
        [InlineData((ushort)TiffTagType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineData((ushort)TiffTagType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineData((ushort)TiffTagType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineData((ushort)TiffTagType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_ReturnsExpectedData(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, count, bytes), isLittleEndian);

            byte[] result = decoder.ReadBytes(ref entry);

            if (bytes.Length < 4)
            {
                result = result.Take(bytes.Length).ToArray();
            }

            Assert.Equal(bytes, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 5u, new byte[] { 17, 28, 2, 9, 13 }, true)]
        [InlineData((ushort)TiffTagType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, false)]
        [InlineData((ushort)TiffTagType.Byte, 10u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2, 127, 86 }, true)]
        [InlineData((ushort)TiffTagType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, false)]
        [InlineData((ushort)TiffTagType.Short, 3u, new byte[] { 17, 28, 2, 9, 13, 37 }, true)]
        [InlineData((ushort)TiffTagType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineData((ushort)TiffTagType.Long, 2u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        [InlineData((ushort)TiffTagType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, false)]
        [InlineData((ushort)TiffTagType.Rational, 1u, new byte[] { 17, 28, 2, 9, 13, 37, 18, 2 }, true)]
        public void ReadBytes_CachesDataLongerThanFourBytes(ushort type, uint count, byte[] bytes, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, count, bytes), isLittleEndian);

            Assert.Equal(4, entry.Value.Length);

            byte[] result = decoder.ReadBytes(ref entry);

            Assert.Equal(bytes.Length, entry.Value.Length);
            Assert.Equal(bytes, entry.Value);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.Byte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineData((ushort)TiffTagType.Byte, true, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineData((ushort)TiffTagType.Byte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.Byte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineData((ushort)TiffTagType.Byte, false, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineData((ushort)TiffTagType.Short, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.Short, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineData((ushort)TiffTagType.Short, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineData((ushort)TiffTagType.Short, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineData((ushort)TiffTagType.Short, true, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineData((ushort)TiffTagType.Short, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.Short, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineData((ushort)TiffTagType.Short, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineData((ushort)TiffTagType.Short, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineData((ushort)TiffTagType.Short, false, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineData((ushort)TiffTagType.Long, true, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineData((ushort)TiffTagType.Long, false, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        public void ReadUnsignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, uint expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, bytes), isLittleEndian);

            uint result = decoder.ReadUnsignedInteger(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadUnsignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, true)]
        [InlineData((ushort)TiffTagType.Short, true)]
        [InlineData((ushort)TiffTagType.Long, true)]
        [InlineData((ushort)TiffTagType.Byte, false)]
        [InlineData((ushort)TiffTagType.Short, false)]
        [InlineData((ushort)TiffTagType.Long, false)]
        public void ReadUnsignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.SByte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.SByte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineData((ushort)TiffTagType.SByte, true, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineData((ushort)TiffTagType.SByte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.SByte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineData((ushort)TiffTagType.SByte, false, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineData((ushort)TiffTagType.SShort, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.SShort, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineData((ushort)TiffTagType.SShort, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineData((ushort)TiffTagType.SShort, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineData((ushort)TiffTagType.SShort, true, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineData((ushort)TiffTagType.SShort, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineData((ushort)TiffTagType.SShort, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineData((ushort)TiffTagType.SShort, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineData((ushort)TiffTagType.SShort, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineData((ushort)TiffTagType.SShort, false, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineData((ushort)TiffTagType.SLong, true, new byte[] { 255, 255, 255, 255 }, -1)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineData((ushort)TiffTagType.SLong, false, new byte[] { 255, 255, 255, 255 }, -1)]
        public void ReadSignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, int expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, bytes), isLittleEndian);

            int result = decoder.ReadSignedInteger(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadSignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a signed integer.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.SByte, true)]
        [InlineData((ushort)TiffTagType.SShort, true)]
        [InlineData((ushort)TiffTagType.SLong, true)]
        [InlineData((ushort)TiffTagType.SByte, false)]
        [InlineData((ushort)TiffTagType.SShort, false)]
        [InlineData((ushort)TiffTagType.SLong, false)]
        public void ReadSignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte, 1, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineData((ushort)TiffTagType.Byte, 3, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineData((ushort)TiffTagType.Byte, 7, true, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineData((ushort)TiffTagType.Byte, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineData((ushort)TiffTagType.Byte, 3, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineData((ushort)TiffTagType.Byte, 7, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineData((ushort)TiffTagType.Short, 1, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1 })]
        [InlineData((ushort)TiffTagType.Short, 2, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1, 515 })]
        [InlineData((ushort)TiffTagType.Short, 3, true, new byte[] { 1, 0, 3, 2, 5, 4, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineData((ushort)TiffTagType.Short, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1 })]
        [InlineData((ushort)TiffTagType.Short, 2, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1, 515 })]
        [InlineData((ushort)TiffTagType.Short, 3, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineData((ushort)TiffTagType.Long, 1, true, new byte[] { 4, 3, 2, 1 }, new uint[] { 0x01020304 })]
        [InlineData((ushort)TiffTagType.Long, 2, true, new byte[] { 4, 3, 2, 1, 6, 5, 4, 3, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        [InlineData((ushort)TiffTagType.Long, 1, false, new byte[] { 1, 2, 3, 4 }, new uint[] { 0x01020304 })]
        [InlineData((ushort)TiffTagType.Long, 2, false, new byte[] { 1, 2, 3, 4, 3, 4, 5, 6, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        public void ReadUnsignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, uint[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            uint[] result = decoder.ReadUnsignedIntegerArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadUnsignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.SByte, 1, true, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineData((ushort)TiffTagType.SByte, 3, true, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineData((ushort)TiffTagType.SByte, 7, true, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineData((ushort)TiffTagType.SByte, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineData((ushort)TiffTagType.SByte, 3, false, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineData((ushort)TiffTagType.SByte, 7, false, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineData((ushort)TiffTagType.SShort, 1, true, new byte[] { 1, 0, 3, 2 }, new int[] { 1 })]
        [InlineData((ushort)TiffTagType.SShort, 2, true, new byte[] { 1, 0, 255, 255 }, new int[] { 1, -1 })]
        [InlineData((ushort)TiffTagType.SShort, 3, true, new byte[] { 1, 0, 255, 255, 5, 4, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineData((ushort)TiffTagType.SShort, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 1 })]
        [InlineData((ushort)TiffTagType.SShort, 2, false, new byte[] { 0, 1, 255, 255 }, new int[] { 1, -1 })]
        [InlineData((ushort)TiffTagType.SShort, 3, false, new byte[] { 0, 1, 255, 255, 4, 5, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineData((ushort)TiffTagType.SLong, 1, true, new byte[] { 4, 3, 2, 1 }, new int[] { 0x01020304 })]
        [InlineData((ushort)TiffTagType.SLong, 2, true, new byte[] { 4, 3, 2, 1, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        [InlineData((ushort)TiffTagType.SLong, 1, false, new byte[] { 1, 2, 3, 4 }, new int[] { 0x01020304 })]
        [InlineData((ushort)TiffTagType.SLong, 2, false, new byte[] { 1, 2, 3, 4, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        public void ReadSignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, int[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            int[] result = decoder.ReadSignedIntegerArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadSignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a signed integer.", e.Message);
        }

        [Theory]
        [InlineData(true, new byte[] { 0 }, "")]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 }, "ABC")]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', 0 }, "ABCDEF")]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H', 0 }, "ABCD\0EFGH")]
        [InlineData(false, new byte[] { 0 }, "")]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', 0 }, "ABC")]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', 0 }, "ABCDEF")]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H', 0 }, "ABCD\0EFGH")]
        public void ReadString_ReturnsValue(bool isLittleEndian, byte[] bytes, string expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Ascii, (uint)bytes.Length, bytes), isLittleEndian);

            string result = decoder.ReadString(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadString_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadString(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a string.", e.Message);
        }

        [Theory]
        [InlineData(true, new byte[] { (byte)'A' })]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C' })]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' })]
        [InlineData(true, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H' })]
        [InlineData(false, new byte[] { (byte)'A' })]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C' })]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' })]
        [InlineData(false, new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0, (byte)'E', (byte)'F', (byte)'G', (byte)'H' })]
        public void ReadString_ThrowsExceptionIfStringIsNotNullTerminated(bool isLittleEndian, byte[] bytes)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Ascii, (uint)bytes.Length, bytes), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadString(ref entry));

            Assert.Equal($"The retrieved string is not null terminated.", e.Message);
        }

        [Theory]
        [InlineData(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, 0, 2)]
        [InlineData(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        [InlineData(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, 0, 2)]
        [InlineData(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, 1, 2)]
        public void ReadUnsignedRational_ReturnsValue(bool isLittleEndian, byte[] bytes, uint expectedNumerator, uint expectedDenominator)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Rational, 1, bytes), isLittleEndian);

            Rational result = decoder.ReadUnsignedRational(ref entry);
            Rational expectedValue = new Rational(expectedNumerator, expectedDenominator);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, 0, 2)]
        [InlineData(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, 1, 2)]
        [InlineData(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, -1, 2)]
        [InlineData(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, 0, 2)]
        [InlineData(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, 1, 2)]
        [InlineData(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2 }, -1, 2)]
        public void ReadSignedRational_ReturnsValue(bool isLittleEndian, byte[] bytes, int expectedNumerator, int expectedDenominator)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.SRational, 1, bytes), isLittleEndian);

            SignedRational result = decoder.ReadSignedRational(ref entry);
            SignedRational expectedValue = new SignedRational(expectedNumerator, expectedDenominator);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, new uint[] { 0 }, new uint[] { 2 })]
        [InlineData(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new uint[] { 1 }, new uint[] { 2 })]
        [InlineData(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new uint[] { 1, 2 }, new uint[] { 2, 3 })]
        [InlineData(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, new uint[] { 0 }, new uint[] { 2 })]
        [InlineData(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, new uint[] { 1 }, new uint[] { 2 })]
        [InlineData(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3 }, new uint[] { 1, 2 }, new uint[] { 2, 3 })]
        public void ReadUnsignedRationalArray_ReturnsValue(bool isLittleEndian, byte[] bytes, uint[] expectedNumerators, uint[] expectedDenominators)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Rational, (uint)expectedNumerators.Length, bytes), isLittleEndian);

            Rational[] result = decoder.ReadUnsignedRationalArray(ref entry);
            Rational[] expectedValue = Enumerable.Range(0, expectedNumerators.Length).Select(i => new Rational(expectedNumerators[i], expectedDenominators[i])).ToArray();

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData(true, new byte[] { 0, 0, 0, 0, 2, 0, 0, 0 }, new int[] { 0 }, new int[] { 2 })]
        [InlineData(true, new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new int[] { 1 }, new int[] { 2 })]
        [InlineData(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0 }, new int[] { -1 }, new int[] { 2 })]
        [InlineData(true, new byte[] { 255, 255, 255, 255, 2, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 }, new int[] { -1, 2 }, new int[] { 2, 3 })]
        [InlineData(false, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }, new int[] { 0 }, new int[] { 2 })]
        [InlineData(false, new byte[] { 0, 0, 0, 1, 0, 0, 0, 2 }, new int[] { 1 }, new int[] { 2 })]
        [InlineData(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2 }, new int[] { -1 }, new int[] { 2 })]
        [InlineData(false, new byte[] { 255, 255, 255, 255, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 3 }, new int[] { -1, 2 }, new int[] { 2, 3 })]
        public void ReadSignedRationalArray_ReturnsValue(bool isLittleEndian, byte[] bytes, int[] expectedNumerators, int[] expectedDenominators)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.SRational, (uint)expectedNumerators.Length, bytes), isLittleEndian);

            SignedRational[] result = decoder.ReadSignedRationalArray(ref entry);
            SignedRational[] expectedValue = Enumerable.Range(0, expectedNumerators.Length).Select(i => new SignedRational(expectedNumerators[i], expectedDenominators[i])).ToArray();

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadUnsignedRational_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRational(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a Rational.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadSignedRational_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRational(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a SignedRational.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadUnsignedRationalArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRationalArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a Rational.", e.Message);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadSignedRationalArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRationalArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a SignedRational.", e.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadUnsignedRational_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Rational, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedRational(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadSignedRational_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.SRational, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedRational(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0.0F)]
        [InlineData(false, new byte[] { 0x3F, 0x80, 0x00, 0x00 }, 1.0F)]
        [InlineData(false, new byte[] { 0xC0, 0x00, 0x00, 0x00 }, -2.0F)]
        [InlineData(false, new byte[] { 0x7F, 0x7F, 0xFF, 0xFF }, float.MaxValue)]
        [InlineData(false, new byte[] { 0x7F, 0x80, 0x00, 0x00 }, float.PositiveInfinity)]
        [InlineData(false, new byte[] { 0xFF, 0x80, 0x00, 0x00 }, float.NegativeInfinity)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0.0F)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0x3F }, 1.0F)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0xC0 }, -2.0F)]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, float.MaxValue)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0x7F }, float.PositiveInfinity)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0xFF }, float.NegativeInfinity)]
        public void ReadFloat_ReturnsValue(bool isLittleEndian, byte[] bytes, float expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Float, 1, bytes), isLittleEndian);

            float result = decoder.ReadFloat(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadFloat_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloat(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a float.", e.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadFloat_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Float, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloat(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00 }, new float[] { 0.0F })]
        [InlineData(false, new byte[] { 0x3F, 0x80, 0x00, 0x00 }, new float[] { 1.0F })]
        [InlineData(false, new byte[] { 0xC0, 0x00, 0x00, 0x00 }, new float[] { -2.0F })]
        [InlineData(false, new byte[] { 0x7F, 0x7F, 0xFF, 0xFF }, new float[] { float.MaxValue })]
        [InlineData(false, new byte[] { 0x7F, 0x80, 0x00, 0x00 }, new float[] { float.PositiveInfinity })]
        [InlineData(false, new byte[] { 0xFF, 0x80, 0x00, 0x00 }, new float[] { float.NegativeInfinity })]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x3F, 0x80, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00 }, new float[] { 0.0F, 1.0F, -2.0F })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00 }, new float[] { 0.0F })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0x3F }, new float[] { 1.0F })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0xC0 }, new float[] { -2.0F })]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0x7F, 0x7F }, new float[] { float.MaxValue })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0x7F }, new float[] { float.PositiveInfinity })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x80, 0xFF }, new float[] { float.NegativeInfinity })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0xC0 }, new float[] { 0.0F, 1.0F, -2.0F })]

        public void ReadFloatArray_ReturnsValue(bool isLittleEndian, byte[] bytes, float[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Float, (uint)expectedValue.Length, bytes), isLittleEndian);

            float[] result = decoder.ReadFloatArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Double)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadFloatArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadFloatArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a float.", e.Message);
        }

        [Theory]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0.0)]
        [InlineData(false, new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 1.0)]
        [InlineData(false, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 2.0)]
        [InlineData(false, new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, -2.0)]
        [InlineData(false, new byte[] { 0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, double.MaxValue)]
        [InlineData(false, new byte[] { 0x7F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, double.PositiveInfinity)]
        [InlineData(false, new byte[] { 0xFF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, double.NegativeInfinity)]
        [InlineData(false, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, double.NaN)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0.0)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, 1.0)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, 2.0)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, -2.0)]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, double.MaxValue)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, double.PositiveInfinity)]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, double.NegativeInfinity)]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, double.NaN)]
        public void ReadDouble_ReturnsValue(bool isLittleEndian, byte[] bytes, double expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Double, 1, bytes), isLittleEndian);

            double result = decoder.ReadDouble(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadDouble_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDouble(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a double.", e.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadDouble_ThrowsExceptionIfCountIsNotOne(bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Double, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDouble(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0 })]
        [InlineData(false, new byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 1.0 })]
        [InlineData(false, new byte[] { 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 2.0 })]
        [InlineData(false, new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { -2.0 })]
        [InlineData(false, new byte[] { 0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, new double[] { double.MaxValue })]
        [InlineData(false, new byte[] { 0x7F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { double.PositiveInfinity })]
        [InlineData(false, new byte[] { 0xFF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { double.NegativeInfinity })]
        [InlineData(false, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, new double[] { double.NaN })]
        [InlineData(false, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0, 1.0, -2.0 })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, new double[] { 0.0 })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, new double[] { 1.0 })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40 }, new double[] { 2.0 })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { -2.0 })]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F }, new double[] { double.MaxValue })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F }, new double[] { double.PositiveInfinity })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF }, new double[] { double.NegativeInfinity })]
        [InlineData(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, new double[] { double.NaN })]
        [InlineData(true, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0 }, new double[] { 0.0, 1.0, -2.0 })]
        public void ReadDoubleArray_ReturnsValue(bool isLittleEndian, byte[] bytes, double[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, TiffTagType.Double, (uint)expectedValue.Length, bytes), isLittleEndian);

            double[] result = decoder.ReadDoubleArray(ref entry);

            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData((ushort)TiffTagType.Byte)]
        [InlineData((ushort)TiffTagType.Ascii)]
        [InlineData((ushort)TiffTagType.Short)]
        [InlineData((ushort)TiffTagType.Long)]
        [InlineData((ushort)TiffTagType.Rational)]
        [InlineData((ushort)TiffTagType.SByte)]
        [InlineData((ushort)TiffTagType.Undefined)]
        [InlineData((ushort)TiffTagType.SShort)]
        [InlineData((ushort)TiffTagType.SLong)]
        [InlineData((ushort)TiffTagType.SRational)]
        [InlineData((ushort)TiffTagType.Float)]
        [InlineData((ushort)TiffTagType.Ifd)]
        [InlineData((ushort)99)]
        public void ReadDoubleArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTagId.ImageWidth, (TiffTagType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadDoubleArray(ref entry));

            Assert.Equal($"A value of type '{(TiffTagType)type}' cannot be converted to a double.", e.Message);
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
