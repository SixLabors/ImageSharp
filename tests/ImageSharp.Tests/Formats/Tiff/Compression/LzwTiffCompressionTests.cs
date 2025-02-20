// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.IO;

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

        using var decompressor = new LzwTiffCompression(Configuration.Default.MemoryAllocator, 10, 8, TiffColorType.BlackIsZero8, TiffPredictor.None, false, false, 0);
        decompressor.Decompress(stream, 0, (uint)stream.Length, 1, buffer, default);

        Assert.Equal(data, buffer);
    }

    private static BufferedReadStream CreateCompressedStream(byte[] inputData)
    {
        Stream compressedStream = new MemoryStream();

        using (var encoder = new TiffLzwEncoder(Configuration.Default.MemoryAllocator))
        {
            encoder.Encode(inputData, compressedStream);
        }

        compressedStream.Seek(0, SeekOrigin.Begin);

        return new BufferedReadStream(Configuration.Default, compressedStream);
    }
}
