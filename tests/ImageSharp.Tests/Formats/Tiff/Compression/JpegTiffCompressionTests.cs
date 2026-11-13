// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class JpegTiffCompressionTests
{
    [Fact]
    public void Decode_JpegStripWithoutTablesPointingAtTiffHeader_ThrowsInvalidImageContentException()
    {
        // The strip has no JPEGTables and starts at offset 0, which is the TIFF header.
        // A format-detecting load would decode the file as TIFF again and recurse until the stack overflows.
        // The JPEG decoder must reject the strip because it does not start with a JPEG SOI marker.
        byte[] tiff = CreateSelfReferencingJpegTiff();

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Image.Load(tiff));

        Assert.Equal("Missing SOI marker.", exception.Message);
    }

    /// <summary>
    /// Creates the 140-byte 8x8 RGB TIFF from the advisory. Its one JPEG strip covers the whole file, from offset 0.
    /// </summary>
    /// <returns>The encoded TIFF.</returns>
    private static byte[] CreateSelfReferencingJpegTiff()
    {
        const ushort shortType = 3;
        const ushort longType = 4;
        const int entryCount = 10;

        // The IFD follows the 8-byte header: a 2-byte count, 12 bytes for each entry, and a 4-byte next-IFD offset.
        // The three BitsPerSample values do not fit in the 4-byte value field, so they follow the IFD.
        const uint bitsPerSampleOffset = 8 + 2 + (entryCount * 12) + 4;
        const uint fileLength = bitsPerSampleOffset + (3 * sizeof(ushort));

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        // Little-endian TIFF header with the first IFD at offset 8.
        writer.Write("II"u8);
        writer.Write((ushort)42);
        writer.Write(8U);

        // TIFF requires the entries in ascending tag order. There is no JPEGTables entry (tag 347).
        writer.Write((ushort)entryCount);
        WriteEntry(writer, 256, shortType, 1, 8); // ImageWidth
        WriteEntry(writer, 257, shortType, 1, 8); // ImageLength
        WriteEntry(writer, 258, shortType, 3, bitsPerSampleOffset); // BitsPerSample: 8, 8, 8
        WriteEntry(writer, 259, shortType, 1, 7); // Compression: JPEG
        WriteEntry(writer, 262, shortType, 1, 2); // PhotometricInterpretation: RGB
        WriteEntry(writer, 273, longType, 1, 0); // StripOffsets: the TIFF header
        WriteEntry(writer, 277, shortType, 1, 3); // SamplesPerPixel
        WriteEntry(writer, 278, shortType, 1, 8); // RowsPerStrip
        WriteEntry(writer, 279, longType, 1, fileLength); // StripByteCounts: the whole file
        WriteEntry(writer, 284, shortType, 1, 1); // PlanarConfiguration: chunky
        writer.Write(0U);

        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Writes one IFD entry.
    /// </summary>
    /// <param name="writer">The TIFF writer.</param>
    /// <param name="tag">The tag identifier.</param>
    /// <param name="type">The TIFF field type. SHORT is 3 and LONG is 4.</param>
    /// <param name="count">The number of values.</param>
    /// <param name="value">The inline value, or the offset of values that do not fit in 4 bytes.</param>
    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);

        // A single SHORT value occupies the first 2 bytes of the 4-byte value field. Little-endian order
        // puts those bytes first, so writing the value as 32 bits stores SHORT values, LONG values and offsets correctly.
        writer.Write(value);
    }
}
