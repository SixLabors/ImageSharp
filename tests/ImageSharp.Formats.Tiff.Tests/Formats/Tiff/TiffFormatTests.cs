// <copyright file="TiffFormatTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Linq;
    using Xunit;

    using ImageSharp.Formats;

    public class TiffFormatTests
    {
        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void IsSupportedFileFormat_ReturnsTrue_ForValidFile(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
                            {
                                FirstIfd = new TiffGenIfd()
                            }
                            .ToBytes(isLittleEndian);

            TiffFormat tiffFormat = new TiffFormat();
            byte[] headerBytes = bytes.Take(tiffFormat.HeaderSize).ToArray();
            bool isSupported = tiffFormat.IsSupportedFileFormat(headerBytes);

            Assert.True(isSupported);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void IsSupportedFileFormat_ReturnsFalse_WithInvalidByteOrderMarkers(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
                            {
                                FirstIfd = new TiffGenIfd(),
                                ByteOrderMarker = 0x1234
                            }
                            .ToBytes(isLittleEndian);

            TiffFormat tiffFormat = new TiffFormat();
            byte[] headerBytes = bytes.Take(tiffFormat.HeaderSize).ToArray();
            bool isSupported = tiffFormat.IsSupportedFileFormat(headerBytes);

            Assert.False(isSupported);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void IsSupportedFileFormat_ReturnsFalse_WithIncorrectMagicNumber(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
                            {
                                FirstIfd = new TiffGenIfd(),
                                MagicNumber = 32
                            }
                            .ToBytes(isLittleEndian);

            TiffFormat tiffFormat = new TiffFormat();
            byte[] headerBytes = bytes.Take(tiffFormat.HeaderSize).ToArray();
            bool isSupported = tiffFormat.IsSupportedFileFormat(headerBytes);

            Assert.False(isSupported);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void IsSupportedFileFormat_ReturnsFalse_WithShortHeader(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
                            {
                                FirstIfd = new TiffGenIfd()
                            }
                            .ToBytes(isLittleEndian);

            TiffFormat tiffFormat = new TiffFormat();
            byte[] headerBytes = bytes.Take(tiffFormat.HeaderSize - 1).ToArray();
            bool isSupported = tiffFormat.IsSupportedFileFormat(headerBytes);

            Assert.False(isSupported);
        }
    }
}
