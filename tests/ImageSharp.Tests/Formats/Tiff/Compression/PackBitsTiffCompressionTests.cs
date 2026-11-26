// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

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
        using MemoryStream memoryStream = new(inputData);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        byte[] buffer = new byte[expectedResult.Length];

        using PackBitsTiffCompression decompressor = new(MemoryAllocator.Create(), default, default, SegmentIntegrityHandling.Strict);

        // These codecs return raw TIFF samples; no final pixel region is needed.
        decompressor.Decompress<Rgba32>(stream, 0, (uint)inputData.Length, 1, buffer, default, default);

        Assert.Equal(expectedResult, buffer);
    }

    /// <summary>
    /// Data that ends before it fills the strip is damaged. In the IgnoreImageData mode, the strip keeps the decoded
    /// bytes, and the decoder clears the bytes of the reused buffer after them. The other modes throw.
    /// </summary>
    /// <param name="integrity">The segment integrity mode.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void Decompress_DataEndsEarly_UsesImageDataIntegrityPolicy(SegmentIntegrityHandling integrity)
    {
        // A literal run of two bytes for a strip of four bytes. The buffer holds the bytes of an earlier strip.
        byte[] inputData = [0x01, 0x15, 0x32];
        using MemoryStream memoryStream = new(inputData);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        byte[] buffer = [0xFF, 0xFF, 0xFF, 0xFF];

        using PackBitsTiffCompression decompressor = new(MemoryAllocator.Create(), default, default, integrity);

        if (integrity != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() => decompressor.Decompress<Rgba32>(stream, 0, (uint)inputData.Length, 1, buffer, default, default));
            return;
        }

        decompressor.Decompress<Rgba32>(stream, 0, (uint)inputData.Length, 1, buffer, default, default);

        Assert.Equal(new byte[] { 0x15, 0x32, 0x00, 0x00 }, buffer);
    }

    /// <summary>
    /// A run that is longer than the rest of the strip writes the part that fits, as libtiff does. Only missing output is damage. Thus the Strict mode does not
    /// throw.
    /// </summary>
    /// <param name="inputData">The compressed data.</param>
    /// <param name="expectedResult">The expected strip.</param>
    [Theory]
    [InlineData(new byte[] { 0xFE, 0x2A }, new byte[] { 0x2A, 0x2A })] // A repeat run of three bytes. The strip has space for two bytes.
    [InlineData(new byte[] { 0x02, 0x15, 0x32, 0x40 }, new byte[] { 0x15, 0x32 })] // A literal run of three bytes. The strip has space for two bytes.
    public void Decompress_RunLongerThanStrip_WritesPartThatFits(byte[] inputData, byte[] expectedResult)
    {
        using MemoryStream memoryStream = new(inputData);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        byte[] buffer = new byte[expectedResult.Length];

        using PackBitsTiffCompression decompressor = new(MemoryAllocator.Create(), default, default, SegmentIntegrityHandling.Strict);
        decompressor.Decompress<Rgba32>(stream, 0, (uint)inputData.Length, 1, buffer, default, TestContext.Current.CancellationToken);

        Assert.Equal(expectedResult, buffer);
    }

    [Theory]
    [InlineData(new byte[] { 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA, 0x80, 0x00, 0x2A, 0x22, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA }, new byte[] { 0xFE, 0xAA, 0x02, 0x80, 0x00, 0x2A, 0xFD, 0xAA, 0x03, 0x80, 0x00, 0x2A, 0x22, 0xF7, 0xAA })] // Apple PackBits sample
    public void Compress_Works(byte[] inputData, byte[] expectedResult)
    {
        // arrange
        Span<byte> input = inputData.AsSpan();
        byte[] compressed = new byte[expectedResult.Length];

        // act
        PackBitsWriter.PackBits(input, compressed);

        // assert
        Assert.Equal(expectedResult, compressed);
    }
}
