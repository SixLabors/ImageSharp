// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class LzwTiffCompressionTests
{
    [Theory]
    [InlineData(new byte[] { 1, 2, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 3, 4 }, new byte[] { 128, 0, 64, 66, 168, 36, 22, 12, 3, 2, 64, 64, 0, 0 })] // Repeated bytes

    public void Compress_Works(byte[] inputData, byte[] expectedCompressedData)
    {
        byte[] compressedData = new byte[expectedCompressedData.Length];
        Stream streamData = CreateCompressedStream(inputData);
        streamData.Read(compressedData, 0, expectedCompressedData.Length);

        Assert.Equal(expectedCompressedData, compressedData);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 42 })] // One byte
    [InlineData(new byte[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 })]
    [InlineData(new byte[] { 42, 16, 128, 53, 96, 218, 7, 64, 3, 4, 97 })] // Random bytes
    [InlineData(new byte[] { 1, 2, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 3, 4 })] // Repeated bytes
    [InlineData(new byte[] { 1, 2, 42, 53, 42, 53, 42, 53, 42, 53, 42, 53, 3, 4 })] // Repeated sequence

    public void Compress_Decompress_Roundtrip_Works(byte[] data)
    {
        using BufferedReadStream stream = CreateCompressedStream(data);
        byte[] buffer = new byte[data.Length];

        using LzwTiffCompression decompressor = new(
            Configuration.Default.MemoryAllocator,
            10,
            8,
            TiffColorType.BlackIsZero8,
            TiffPredictor.None,
            false,
            false,
            0,
            0,
            SegmentIntegrityHandling.Strict);

        // These codecs return raw TIFF samples; no final pixel region is needed.
        decompressor.Decompress<Rgba32>(stream, 0, (uint)stream.Length, 1, buffer, default, default);

        Assert.Equal(data, buffer);
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
        // The data holds three bytes for a strip of five bytes. The buffer holds the bytes of an earlier strip.
        using BufferedReadStream stream = CreateCompressedStream([1, 2, 3]);
        byte[] buffer = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        using LzwTiffCompression decompressor = new(
            Configuration.Default.MemoryAllocator,
            10,
            8,
            TiffColorType.BlackIsZero8,
            TiffPredictor.None,
            false,
            false,
            0,
            0,
            integrity);

        if (integrity != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() => decompressor.Decompress<Rgba32>(stream, 0, (uint)stream.Length, 1, buffer, default, default));
            return;
        }

        decompressor.Decompress<Rgba32>(stream, 0, (uint)stream.Length, 1, buffer, default, default);

        Assert.Equal(new byte[] { 1, 2, 3, 0, 0 }, buffer);
    }

    /// <summary>
    /// A Clear code can follow a Clear code. The decoder skips the second Clear code, as libtiff does.
    /// </summary>
    [Fact]
    public void Decompress_RepeatedClearCodes_DecodesData()
    {
        // The data holds the 9-bit codes Clear, Clear, 1, 2, 3 and EndOfInformation. The first bit is the most significant bit.
        byte[] inputData = [0x80, 0x40, 0x00, 0x20, 0x20, 0x1C, 0x04];
        using MemoryStream memoryStream = new(inputData);
        using BufferedReadStream stream = new(Configuration.Default, memoryStream);
        byte[] buffer = new byte[3];

        using LzwTiffCompression decompressor = new(
            Configuration.Default.MemoryAllocator,
            3,
            8,
            TiffColorType.BlackIsZero8,
            TiffPredictor.None,
            false,
            false,
            0,
            0,
            SegmentIntegrityHandling.Strict);

        decompressor.Decompress<Rgba32>(stream, 0, (uint)inputData.Length, 1, buffer, default, TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
    }

    private static BufferedReadStream CreateCompressedStream(byte[] inputData)
    {
        Stream compressedStream = new MemoryStream();

        using (TiffLzwEncoder encoder = new(Configuration.Default.MemoryAllocator))
        {
            encoder.Encode(inputData, compressedStream);
        }

        compressedStream.Seek(0, SeekOrigin.Begin);

        return new BufferedReadStream(Configuration.Default, compressedStream);
    }
}
