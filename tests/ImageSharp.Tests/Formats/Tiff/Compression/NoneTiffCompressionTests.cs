// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.IO;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression
{
    [Trait("Format", "Tiff")]
    public class NoneTiffCompressionTests
    {
        [Theory]
        [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 8, new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 })]
        [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 5, new byte[] { 10, 15, 20, 25, 30 })]
        public void Decompress_ReadsData(byte[] inputData, uint byteCount, byte[] expectedResult)
        {
            var stream = new BufferedReadStream(Configuration.Default, new MemoryStream(inputData));
            var buffer = new byte[expectedResult.Length];

            new NoneTiffCompression(default, default, default).Decompress(stream, 0, byteCount, buffer);

            Assert.Equal(expectedResult, buffer);
        }
    }
}
