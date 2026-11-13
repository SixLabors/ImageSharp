// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class CcittTiffCompressionTests
{
    private const string T4PocGzipBase64 =
        "H4sICBOogmoCA3BvYy10aWZmLXQ0LnRpZgDty7ENwjAQhlFfjKKUoYGSPlPQZopskGVYiJFgA2zpFGUG9Kz3S19xXtelTKX0xaVEq2dbZPcNUY+u2bVtzO7vmvd72+3095792vJyfsQH" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADgH33fP9Zj5xoBYAEA";

    private const string ModifiedHuffmanPocGzipBase64 =
        "H4sICBSogmoCA3BvYy10aWZmLW1oLnRpZgDty7ENgzAQhlEfRBElNKRMnylomSIbZBkWYiTYAFs6RZkhetb7pa84r+urDKW0xa1EraUustu66L/dZ3d19+z2prz/1M0/fx/Z2zsvx2cc" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/+jcL3Buw0IBYAEA";

    private const string TiledT4ReporterPocGzipBase64 =
        "H4sIAAAAAAACCu3UKw7CUBCG0RkuIeC6AxQGj8TwEHWshyWxOhI2QC8Z0QWAIDnNmeQXran4xnEf64jYROQyMvo8RtYepltk++x+rXabblW7P6fZ++fZvtS+T3et/djVR8M2n/Btr1v6C/zCQbQQLUQLRAvRQrRAtBAtRAtEC9FCtEC0EC3+Mlpv/6RrkiQmAAA=";

    [Theory]
    [InlineData(T4PocGzipBase64)]
    [InlineData(ModifiedHuffmanPocGzipBase64)]
    public void Decode_CcittStripWithOversizedRun_ThrowsImageFormatException(string gzipBase64)
    {
        using MemoryStream tiffStream = CreateTiffStream(gzipBase64);

        ImageFormatException exception = Assert.Throws<ImageFormatException>(() => Image.Load(tiffStream));

        Assert.Equal("CCITT compression parsing error: decoded more pixels than the image width.", exception.Message);
    }

    [Fact]
    public void Decode_TiledCcittReporterSample_ThrowsImageFormatException()
    {
        // This is the reporter's exact tiled T4 TIFF sample, gzip-compressed only to keep the test source small.
        using MemoryStream tiffStream = CreateTiffStream(TiledT4ReporterPocGzipBase64);

        ImageFormatException exception = Assert.Throws<ImageFormatException>(() => Image.Load(tiffStream));

        Assert.Equal("CCITT compression parsing error: decoded more pixels than the image width.", exception.Message);
    }

    [Fact]
    public void Decode_TiledCcittWithOverflowingTileSize_ThrowsNotSupportedException()
    {
        // A 1-bit row of 524288 pixels is 65536 bytes. 65536 * 65537 rows wraps a 32-bit size to 65536 bytes,
        // so a buffer sized from the wrapped value holds one row and the second decoded row writes past it.
        byte[] tiff = CreateTiledFaxTiff(524288, 65537);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Image.Load(tiff));

        Assert.Equal("The TIFF tile buffer exceeds Int32.MaxValue bytes.", exception.Message);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    public void Decode_TiledCcittWithZeroTileDimension_ThrowsImageFormatException(uint tileWidth, uint tileLength)
    {
        byte[] tiff = CreateTiledFaxTiff(tileWidth, tileLength);

        ImageFormatException exception = Assert.Throws<ImageFormatException>(() => Image.Load(tiff));

        Assert.Equal("TIFF tile dimensions must be positive.", exception.Message);
    }

    /// <summary>
    /// Creates an 8x8 bilevel TIFF with one CCITT Group 4 tile.
    /// </summary>
    /// <param name="tileWidth">The TileWidth tag value.</param>
    /// <param name="tileLength">The TileLength tag value.</param>
    /// <returns>The encoded TIFF.</returns>
    private static byte[] CreateTiledFaxTiff(uint tileWidth, uint tileLength)
    {
        const ushort shortType = 3;
        const ushort longType = 4;
        const int entryCount = 10;

        // The IFD follows the 8-byte header: a 2-byte count, 12 bytes for each entry, and a 4-byte next-IFD offset.
        const uint tileOffset = 8 + 2 + (entryCount * 12) + 4;

        // In Group 4, a white row below a white reference row is one V0 code, which is the single bit 1.
        // 0xFF gives eight white rows, so the decoder writes past a one-row buffer on the second row.
        // Two EOL codes (000000000001) end the data.
        byte[] tileData = [0xFF, 0x00, 0x10, 0x01];

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        // Little-endian TIFF header with the first IFD at offset 8.
        writer.Write("II"u8);
        writer.Write((ushort)42);
        writer.Write(8U);

        // TIFF requires the entries in ascending tag order.
        writer.Write((ushort)entryCount);
        WriteEntry(writer, 256, longType, 8); // ImageWidth
        WriteEntry(writer, 257, longType, 8); // ImageLength
        WriteEntry(writer, 258, shortType, 1); // BitsPerSample
        WriteEntry(writer, 259, shortType, 4); // Compression: CCITT Group 4
        WriteEntry(writer, 262, shortType, 0); // PhotometricInterpretation: WhiteIsZero
        WriteEntry(writer, 277, shortType, 1); // SamplesPerPixel
        WriteEntry(writer, 322, longType, tileWidth); // TileWidth
        WriteEntry(writer, 323, longType, tileLength); // TileLength
        WriteEntry(writer, 324, longType, tileOffset); // TileOffsets
        WriteEntry(writer, 325, longType, (uint)tileData.Length); // TileByteCounts
        writer.Write(0U);

        writer.Write(tileData);
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Writes one IFD entry that has a single value stored inline.
    /// </summary>
    /// <param name="writer">The TIFF writer.</param>
    /// <param name="tag">The tag identifier.</param>
    /// <param name="type">The TIFF field type. SHORT is 3 and LONG is 4.</param>
    /// <param name="value">The tag value.</param>
    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(1U);

        // A SHORT value occupies the first 2 bytes of the 4-byte value field. Little-endian order
        // puts those bytes first, so writing the value as 32 bits stores both SHORT and LONG correctly.
        writer.Write(value);
    }

    private static MemoryStream CreateTiffStream(string gzipBase64)
    {
        // Keep the TIFF payloads compressed so the regression source remains small.
        byte[] compressed = Convert.FromBase64String(gzipBase64);
        using MemoryStream compressedStream = new(compressed);
        using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);
        MemoryStream tiffStream = new();

        gzipStream.CopyTo(tiffStream);
        tiffStream.Position = 0;

        return tiffStream;
    }
}
