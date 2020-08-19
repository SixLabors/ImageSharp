// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffImageFormatDetectorTests
    {
        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DetectFormat_ReturnsTiffFormat_ForValidFile(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd()
            }
                            .ToBytes(isLittleEndian);

            TiffImageFormatDetector formatDetector = new TiffImageFormatDetector();
            byte[] headerBytes = bytes.Take(formatDetector.HeaderSize).ToArray();
            var format = formatDetector.DetectFormat(headerBytes);

            Assert.NotNull(format);
            Assert.IsType<TiffFormat>(format);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DetectFormat_ReturnsNull_WithInvalidByteOrderMarkers(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd(),
                ByteOrderMarker = 0x1234
            }
                            .ToBytes(isLittleEndian);

            TiffImageFormatDetector formatDetector = new TiffImageFormatDetector();
            byte[] headerBytes = bytes.Take(formatDetector.HeaderSize).ToArray();
            var format = formatDetector.DetectFormat(headerBytes);

            Assert.Null(format);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DetectFormat_ReturnsNull_WithIncorrectMagicNumber(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd(),
                MagicNumber = 32
            }
                            .ToBytes(isLittleEndian);

            TiffImageFormatDetector formatDetector = new TiffImageFormatDetector();
            byte[] headerBytes = bytes.Take(formatDetector.HeaderSize).ToArray();
            var format = formatDetector.DetectFormat(headerBytes);

            Assert.Null(format);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DetectFormat_ReturnsNull_WithShortHeader(bool isLittleEndian)
        {
            byte[] bytes = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd()
            }
                            .ToBytes(isLittleEndian);

            TiffImageFormatDetector formatDetector = new TiffImageFormatDetector();
            byte[] headerBytes = bytes.Take(formatDetector.HeaderSize - 1).ToArray();
            var format = formatDetector.DetectFormat(headerBytes);

            Assert.Null(format);
        }
    }
}
