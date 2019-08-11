// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class TiffDecoderHeaderTests
    {
        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadHeader_ReadsEndianness(bool isLittleEndian)
        {
            Stream stream = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd()
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, false, null, null);

            decoder.ReadHeader();

            Assert.Equal(isLittleEndian, decoder.IsLittleEndian);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadHeader_ReadsFirstIfdOffset(bool isLittleEndian)
        {
            Stream stream = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd()
            }
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, false, null, null);

            uint firstIfdOffset = decoder.ReadHeader();

            Assert.Equal(8u, firstIfdOffset);
        }

        [Theory]
        [InlineData(0x1234)]
        [InlineData(0x4912)]
        [InlineData(0x1249)]
        [InlineData(0x4D12)]
        [InlineData(0x124D)]
        [InlineData(0x494D)]
        [InlineData(0x4D49)]
        public void Decode_ThrowsException_WithInvalidByteOrderMarkers(ushort byteOrderMarker)
        {
            Stream stream = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd(),
                ByteOrderMarker = byteOrderMarker
            }
                            .ToStream(true);

            TiffDecoder decoder = new TiffDecoder();

            ImageFormatException e = Assert.Throws<ImageFormatException>(() => { decoder.Decode<Rgba32>(Configuration.Default, stream); });

            Assert.Equal("Invalid TIFF file header.", e.Message);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void Decode_ThrowsException_WithIncorrectMagicNumber(bool isLittleEndian)
        {
            Stream stream = new TiffGenHeader()
            {
                FirstIfd = new TiffGenIfd(),
                MagicNumber = 32
            }
                            .ToStream(isLittleEndian);

            TiffDecoder decoder = new TiffDecoder();

            ImageFormatException e = Assert.Throws<ImageFormatException>(() => { decoder.Decode<Rgba32>(Configuration.Default, stream); });

            Assert.Equal("Invalid TIFF file header.", e.Message);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void Decode_ThrowsException_WithNoIfdZero(bool isLittleEndian)
        {
            Stream stream = new TiffGenHeader()
            {
                FirstIfd = null
            }
                            .ToStream(isLittleEndian);

            TiffDecoder decoder = new TiffDecoder();

            ImageFormatException e = Assert.Throws<ImageFormatException>(() => { decoder.Decode<Rgba32>(Configuration.Default, stream); });

            Assert.Equal("Invalid TIFF file header.", e.Message);
        }
    }
}