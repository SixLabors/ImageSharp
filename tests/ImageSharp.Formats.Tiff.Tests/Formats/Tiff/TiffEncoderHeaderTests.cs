// <copyright file="TiffEncoderHeaderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;
    using System.Text;

    public class TiffEncoderHeaderTests
    {
        [Fact]
        public void WriteHeader_WritesValidHeader()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                encoder.WriteHeader(writer, 1232);
            }

            stream.Position = 0;
            Assert.Equal(8, stream.Length);
            Assert.Equal(new byte[] { 0x49, 0x49, 42, 0, 0xD0, 0x04, 0x00, 0x00 }, stream.ToArray());
        }

        [Fact]
        public void WriteHeader_ThrowsExceptionIfFirstIfdOffsetIsZero()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                ArgumentException e = Assert.Throws<ArgumentException>(() => { encoder.WriteHeader(writer, 0); });
                Assert.Equal("IFD offsets must be non-zero and on a word boundary.\r\nParameter name: firstIfdOffset", e.Message);
                Assert.Equal("firstIfdOffset", e.ParamName);
            }
        }

        [Fact]
        public void WriteHeader_ThrowsExceptionIfIfdOffsetIsNotOnAWordBoundary()
        {
            MemoryStream stream = new MemoryStream();
            TiffEncoderCore encoder = new TiffEncoderCore(null);

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                ArgumentException e = Assert.Throws<ArgumentException>(() => { encoder.WriteHeader(writer, 1234); });
                Assert.Equal("IFD offsets must be non-zero and on a word boundary.\r\nParameter name: firstIfdOffset", e.Message);
                Assert.Equal("firstIfdOffset", e.ParamName);
            }
        }
    }
}