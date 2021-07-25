// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression
{
    [Trait("Format", "Tiff")]
    public class PackBitsTiffCompressionTests
    {
        [Theory]
        [InlineData(new byte[] { }, new byte[] { })]
        [InlineData(new byte[] { 0x00, 0x2A }, new byte[] { 0x2A })] // Read one byte
        [InlineData(new byte[] { 0x01, 0x15, 0x32 }, new byte[] { 0x15, 0x32 })] // Read two bytes
        [InlineData(new byte[] { 0xFF, 0x2A }, new byte[] { 0x2A, 0x2A })] // Repeat two bytes
        [InlineData(new byte[] { 0xFE, 0x2A }, new byte[] { 0x2A, 0x2A, 0x2A })] // Repeat three bytes
        [InlineData(new byte[] { 0x80 }, new byte[] { })] // Read a 'No operation' byte
        [InlineData(new byte[] { 0x01, 0x15, 0x32, 0x80, 0xFF, 0xA2 }, new byte[] { 0x15, 0x32, 0xA2, 0xA2 })] // Read two bytes, nop, repeat two bytes
        [InlineData(new byte[] { 0xFE, 0xAA, 0x02, 0x80, 0x00, 0x2A, 0xFD, 0xAA, 0x03, 0x80, 0x00, 0x2A, 0x22, 0xF7, 0xAA }, new byte[] { 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0x22, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA })] // Apple PackBits sample
        public void Decompress_ReadsData(byte[] inputData, byte[] expectedResult)
        {
            var stream = new BufferedReadStream(Configuration.Default, new MemoryStream(inputData));
            var buffer = new byte[expectedResult.Length];

            using var decompressor = new PackBitsTiffCompression(new ArrayPoolMemoryAllocator(), default, default);
            decompressor.Decompress(stream, 0, (uint)inputData.Length, buffer);

            Assert.Equal(expectedResult, buffer);
        }

        [Theory]
        [InlineData(new byte[] { 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0x22, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA }, new byte[] { 0xFE, 0xAA, 0x02, 0x80, 0x00, 0x2A, 0xFD, 0xAA, 0x03, 0x80, 0x00, 0x2A, 0x22, 0xF7, 0xAA })] // Apple PackBits sample
        public void Compress_Works(byte[] inputData, byte[] expectedResult)
        {
            // arrange
            Span<byte> input = inputData.AsSpan();
            var compressed = new byte[expectedResult.Length];

            // act
            PackBitsWriter.PackBits(input, compressed);

            // assert
            Assert.Equal(expectedResult, compressed);
        }
    }
}
