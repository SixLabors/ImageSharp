// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class DeflateTiffCompressionTests
{
    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 42 })] // One byte
    [InlineData(new byte[] { 42, 16, 128, 53, 96, 218, 7, 64, 3, 4, 97 })] // Random bytes
    [InlineData(new byte[] { 1, 2, 42, 42, 42, 42, 42, 42, 42, 42, 42, 42, 3, 4 })] // Repeated bytes
    [InlineData(new byte[] { 1, 2, 42, 53, 42, 53, 42, 53, 42, 53, 42, 53, 3, 4 })] // Repeated sequence
    public void Compress_Decompress_Roundtrip_Works(byte[] data)
    {
        using BufferedReadStream stream = CreateCompressedStream(data);
        byte[] buffer = new byte[data.Length];

        using DeflateTiffCompression decompressor = new(
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
    /// Data that ends before it fills the strip is damaged. In the IgnoreImageData mode, the strip keeps the inflated
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

        using DeflateTiffCompression decompressor = new(
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

    private static BufferedReadStream CreateCompressedStream(byte[] data)
    {
        Stream compressedStream = new MemoryStream();

        using (Stream uncompressedStream = new MemoryStream(data),
                      deflateStream = new ZLibStream(
                          compressedStream,
                          new ZLibCompressionOptions { CompressionLevel = (int)DeflateCompressionLevel.Level6 },
                          true))
        {
            uncompressedStream.CopyTo(deflateStream);
        }

        compressedStream.Seek(0, SeekOrigin.Begin);
        return new BufferedReadStream(Configuration.Default, compressedStream);
    }
}
