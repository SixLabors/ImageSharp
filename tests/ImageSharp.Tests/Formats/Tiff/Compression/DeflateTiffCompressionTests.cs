// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
    public class DeflateTiffCompressionTests
    {
        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 42 })] // One byte
        [InlineData(new byte[] { 42, 16, 128, 53, 96, 218, 7, 64, 3, 4, 97 })] // Random bytes
        [InlineData(new byte[] { 1, 2, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 3, 4 })] // Repeated bytes
        [InlineData(new byte[] { 1, 2, 42, 53, 42, 53, 42, 53, 42, 53, 42, 53, 3, 4 })] // Repeated sequence
        public void Decompress_ReadsData(byte[] data)
        {
            using (Stream stream = CreateCompressedStream(data))
            {
                byte[] buffer = new byte[data.Length];

                DeflateTiffCompression.Decompress(stream, (int)stream.Length, buffer);

                Assert.Equal(data, buffer);
            }
        }

        private static Stream CreateCompressedStream(byte[] data)
        {
            Stream compressedStream = new MemoryStream();

            using (Stream uncompressedStream = new MemoryStream(data),
                          deflateStream = new ZlibDeflateStream(compressedStream, 6))
            {
                uncompressedStream.CopyTo(deflateStream);
            }

            compressedStream.Seek(0, SeekOrigin.Begin);
            return compressedStream;
        }
    }
}
