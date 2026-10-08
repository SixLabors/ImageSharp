// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class NoneTiffCompressionTests
{
    [Theory]
    [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 8, new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 })]
    [InlineData(new byte[] { 10, 15, 20, 25, 30, 35, 40, 45 }, 5, new byte[] { 10, 15, 20, 25, 30 })]
    public void Decompress_ReadsData(byte[] inputData, uint byteCount, byte[] expectedResult)
    {
        using MemoryStream memoryStream = new(inputData);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        byte[] buffer = new byte[expectedResult.Length];

        using NoneTiffCompression decompressor = new(default, default, default);

        // These codecs return raw TIFF samples; no final pixel region is needed.
        decompressor.Decompress<Rgba32>(stream, 0, byteCount, 1, buffer, default, default);

        Assert.Equal(expectedResult, buffer);
    }
}
