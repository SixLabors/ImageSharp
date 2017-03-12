// <copyright file="TiffDecoderIfdEntryTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
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

        [Theory]
        [InlineDataAttribute(TiffType.Byte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.Byte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute(TiffType.Byte, true, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineDataAttribute(TiffType.Byte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.Byte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute(TiffType.Byte, false, new byte[] { 255, 2, 3, 4 }, 255)]
        [InlineDataAttribute(TiffType.Short, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.Short, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineDataAttribute(TiffType.Short, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineDataAttribute(TiffType.Short, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineDataAttribute(TiffType.Short, true, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineDataAttribute(TiffType.Short, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.Short, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineDataAttribute(TiffType.Short, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineDataAttribute(TiffType.Short, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineDataAttribute(TiffType.Short, false, new byte[] { 255, 255, 2, 3 }, UInt16.MaxValue)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute(TiffType.Long, true, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineDataAttribute(TiffType.Long, false, new byte[] { 255, 255, 255, 255 }, UInt32.MaxValue)]
        public void ReadUnsignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, uint expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, bytes), isLittleEndian);

            uint result = decoder.ReadUnsignedInteger(ref entry);
            
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Ascii)]
        [InlineDataAttribute(TiffType.Rational)]
        [InlineDataAttribute(TiffType.SByte)]
        [InlineDataAttribute(TiffType.Undefined)]
        [InlineDataAttribute(TiffType.SShort)]
        [InlineDataAttribute(TiffType.SLong)]
        [InlineDataAttribute(TiffType.SRational)]
        [InlineDataAttribute(TiffType.Float)]
        [InlineDataAttribute(TiffType.Double)]
        [InlineDataAttribute(TiffType.Ifd)]
        [InlineDataAttribute((TiffType)99)]
        public void ReadUnsignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte, true)]
        [InlineDataAttribute(TiffType.Short, true)]
        [InlineDataAttribute(TiffType.Long, true)]
        [InlineDataAttribute(TiffType.Byte, false)]
        [InlineDataAttribute(TiffType.Short, false)]
        [InlineDataAttribute(TiffType.Long, false)]
        public void ReadUnsignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(TiffType.SByte, true, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.SByte, true, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute(TiffType.SByte, true, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineDataAttribute(TiffType.SByte, false, new byte[] { 0, 1, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.SByte, false, new byte[] { 1, 2, 3, 4 }, 1)]
        [InlineDataAttribute(TiffType.SByte, false, new byte[] { 255, 2, 3, 4 }, -1)]
        [InlineDataAttribute(TiffType.SShort, true, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.SShort, true, new byte[] { 1, 0, 2, 3 }, 1)]
        [InlineDataAttribute(TiffType.SShort, true, new byte[] { 0, 1, 2, 3 }, 256)]
        [InlineDataAttribute(TiffType.SShort, true, new byte[] { 2, 1, 2, 3 }, 258)]
        [InlineDataAttribute(TiffType.SShort, true, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineDataAttribute(TiffType.SShort, false, new byte[] { 0, 0, 2, 3 }, 0)]
        [InlineDataAttribute(TiffType.SShort, false, new byte[] { 0, 1, 2, 3 }, 1)]
        [InlineDataAttribute(TiffType.SShort, false, new byte[] { 1, 0, 2, 3 }, 256)]
        [InlineDataAttribute(TiffType.SShort, false, new byte[] { 1, 2, 2, 3 }, 258)]
        [InlineDataAttribute(TiffType.SShort, false, new byte[] { 255, 255, 2, 3 }, -1)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 1, 0, 0, 0 }, 1)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 0, 1, 0, 0 }, 256)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 0, 0, 1, 0 }, 256 * 256)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 0, 0, 0, 1 }, 256 * 256 * 256)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 1, 2, 3, 4 }, 67305985)]
        [InlineDataAttribute(TiffType.SLong, true, new byte[] { 255, 255, 255, 255 }, -1)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 0, 0, 0, 0 }, 0)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 0, 0, 0, 1 }, 1)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 0, 0, 1, 0 }, 256)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 0, 1, 0, 0 }, 256 * 256)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 1, 0, 0, 0 }, 256 * 256 * 256)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 4, 3, 2, 1 }, 67305985)]
        [InlineDataAttribute(TiffType.SLong, false, new byte[] { 255, 255, 255, 255 }, -1)]
        public void ReadSignedInteger_ReturnsValue(ushort type, bool isLittleEndian, byte[] bytes, int expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, bytes), isLittleEndian);

            int result = decoder.ReadSignedInteger(ref entry);
            
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte)]
        [InlineDataAttribute(TiffType.Ascii)]
        [InlineDataAttribute(TiffType.Short)]
        [InlineDataAttribute(TiffType.Long)]
        [InlineDataAttribute(TiffType.Rational)]
        [InlineDataAttribute(TiffType.Undefined)]
        [InlineDataAttribute(TiffType.SRational)]
        [InlineDataAttribute(TiffType.Float)]
        [InlineDataAttribute(TiffType.Double)]
        [InlineDataAttribute(TiffType.Ifd)]
        [InlineDataAttribute((TiffType)99)]
        public void ReadSignedInteger_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a signed integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(TiffType.SByte, true)]
        [InlineDataAttribute(TiffType.SShort, true)]
        [InlineDataAttribute(TiffType.SLong, true)]
        [InlineDataAttribute(TiffType.SByte, false)]
        [InlineDataAttribute(TiffType.SShort, false)]
        [InlineDataAttribute(TiffType.SLong, false)]
        public void ReadSignedInteger_ThrowsExceptionIfCountIsNotOne(ushort type, bool isLittleEndian)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 2, new byte[4]), isLittleEndian);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedInteger(ref entry));

            Assert.Equal($"Cannot read a single value from an array of multiple items.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte, 1, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineDataAttribute(TiffType.Byte, 3, true, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineDataAttribute(TiffType.Byte, 7, true, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute(TiffType.Byte, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0 })]
        [InlineDataAttribute(TiffType.Byte, 3, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 0, 1, 2 })]
        [InlineDataAttribute(TiffType.Byte, 7, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 0, 1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute(TiffType.Short, 1, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1 })]
        [InlineDataAttribute(TiffType.Short, 2, true, new byte[] { 1, 0, 3, 2 }, new uint[] { 1, 515 })]
        [InlineDataAttribute(TiffType.Short, 3, true, new byte[] { 1, 0, 3, 2, 5, 4, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineDataAttribute(TiffType.Short, 1, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1 })]
        [InlineDataAttribute(TiffType.Short, 2, false, new byte[] { 0, 1, 2, 3 }, new uint[] { 1, 515 })]
        [InlineDataAttribute(TiffType.Short, 3, false, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new uint[] { 1, 515, 1029 })]
        [InlineDataAttribute(TiffType.Long, 1, true, new byte[] { 4, 3, 2, 1 }, new uint[] { 0x01020304 })]
        [InlineDataAttribute(TiffType.Long, 2, true, new byte[] { 4, 3, 2, 1, 6, 5, 4, 3, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        [InlineDataAttribute(TiffType.Long, 1, false, new byte[] { 1, 2, 3, 4 }, new uint[] { 0x01020304 })]
        [InlineDataAttribute(TiffType.Long, 2, false, new byte[] { 1, 2, 3, 4, 3, 4, 5, 6, 99, 99 }, new uint[] { 0x01020304, 0x03040506 })]
        public void ReadUnsignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, uint[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            uint[] result = decoder.ReadUnsignedIntegerArray(ref entry);
            
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Ascii)]
        [InlineDataAttribute(TiffType.Rational)]
        [InlineDataAttribute(TiffType.SByte)]
        [InlineDataAttribute(TiffType.Undefined)]
        [InlineDataAttribute(TiffType.SShort)]
        [InlineDataAttribute(TiffType.SLong)]
        [InlineDataAttribute(TiffType.SRational)]
        [InlineDataAttribute(TiffType.Float)]
        [InlineDataAttribute(TiffType.Double)]
        [InlineDataAttribute(TiffType.Ifd)]
        [InlineDataAttribute((TiffType)99)]
        public void ReadUnsignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadUnsignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to an unsigned integer.", e.Message);
        }

        [Theory]
        [InlineDataAttribute(TiffType.SByte, 1, true, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineDataAttribute(TiffType.SByte, 3, true, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineDataAttribute(TiffType.SByte, 7, true, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute(TiffType.SByte, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 0 })]
        [InlineDataAttribute(TiffType.SByte, 3, false, new byte[] { 0, 255, 2, 3 }, new int[] { 0, -1, 2 })]
        [InlineDataAttribute(TiffType.SByte, 7, false, new byte[] { 0, 255, 2, 3, 4, 5, 6, 7, 8 }, new int[] { 0, -1, 2, 3, 4, 5, 6 })]
        [InlineDataAttribute(TiffType.SShort, 1, true, new byte[] { 1, 0, 3, 2 }, new int[] { 1 })]
        [InlineDataAttribute(TiffType.SShort, 2, true, new byte[] { 1, 0, 255, 255 }, new int[] { 1, -1 })]
        [InlineDataAttribute(TiffType.SShort, 3, true, new byte[] { 1, 0, 255, 255, 5, 4, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineDataAttribute(TiffType.SShort, 1, false, new byte[] { 0, 1, 2, 3 }, new int[] { 1 })]
        [InlineDataAttribute(TiffType.SShort, 2, false, new byte[] { 0, 1, 255, 255 }, new int[] { 1, -1 })]
        [InlineDataAttribute(TiffType.SShort, 3, false, new byte[] { 0, 1, 255, 255, 4, 5, 6, 7, 8 }, new int[] { 1, -1, 1029 })]
        [InlineDataAttribute(TiffType.SLong, 1, true, new byte[] { 4, 3, 2, 1 }, new int[] { 0x01020304 })]
        [InlineDataAttribute(TiffType.SLong, 2, true, new byte[] { 4, 3, 2, 1, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        [InlineDataAttribute(TiffType.SLong, 1, false, new byte[] { 1, 2, 3, 4 }, new int[] { 0x01020304 })]
        [InlineDataAttribute(TiffType.SLong, 2, false, new byte[] { 1, 2, 3, 4, 255, 255, 255, 255, 99, 99 }, new int[] { 0x01020304, -1 })]
        public void ReadSignedIntegerArray_ReturnsValue(ushort type, int count, bool isLittleEndian, byte[] bytes, int[] expectedValue)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, (uint)expectedValue.Length, bytes), isLittleEndian);

            int[] result = decoder.ReadSignedIntegerArray(ref entry);
            
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineDataAttribute(TiffType.Byte)]
        [InlineDataAttribute(TiffType.Ascii)]
        [InlineDataAttribute(TiffType.Short)]
        [InlineDataAttribute(TiffType.Long)]
        [InlineDataAttribute(TiffType.Rational)]
        [InlineDataAttribute(TiffType.Undefined)]
        [InlineDataAttribute(TiffType.SRational)]
        [InlineDataAttribute(TiffType.Float)]
        [InlineDataAttribute(TiffType.Double)]
        [InlineDataAttribute(TiffType.Ifd)]
        [InlineDataAttribute((TiffType)99)]
        public void ReadSignedIntegerArray_ThrowsExceptionIfInvalidType(ushort type)
        {
            (TiffDecoderCore decoder, TiffIfdEntry entry) = GenerateTestIfdEntry(TiffGenEntry.Bytes(TiffTags.ImageWidth, (TiffType)type, 1, new byte[4]), true);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadSignedIntegerArray(ref entry));

            Assert.Equal($"A value of type '{(TiffType)type}' cannot be converted to a signed integer.", e.Message);
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