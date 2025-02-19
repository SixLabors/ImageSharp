// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.IO;

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

        using var decompressor = new DeflateTiffCompression(Configuration.Default.MemoryAllocator, 10, 8, TiffColorType.BlackIsZero8, TiffPredictor.None, false, false, 0);

        decompressor.Decompress(stream, 0, (uint)stream.Length, 1, buffer, default);

        Assert.Equal(data, buffer);
    }

    private static BufferedReadStream CreateCompressedStream(byte[] data)
    {
        Stream compressedStream = new MemoryStream();

        using (Stream uncompressedStream = new MemoryStream(data),
                      deflateStream = new ZlibDeflateStream(Configuration.Default.MemoryAllocator, compressedStream, DeflateCompressionLevel.Level6))
        {
            uncompressedStream.CopyTo(deflateStream);
        }

        compressedStream.Seek(0, SeekOrigin.Begin);
        return new BufferedReadStream(Configuration.Default, compressedStream);
    }
}
