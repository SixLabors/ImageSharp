// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public partial class PngDecoderTests
{
    // Represents ASCII string of "123456789"
    private readonly byte[] check = [49, 50, 51, 52, 53, 54, 55, 56, 57];

    // Contains the png marker, IHDR and pHYs chunks of a 1x1 pixel 32bit png 1 a single black pixel.
    private static readonly byte[] Raw1X1PngIhdrAndpHYs =
    [
        // PNG Identifier
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,

        // IHDR
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02,
        0x00, 0x00, 0x00,

        // IHDR CRC
        0x90, 0x77, 0x53, 0xDE,

        // pHYS
        0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00,
        0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01,

        // pHYS CRC
        0xC7, 0x6F, 0xA8, 0x64
    ];

    // Contains the png marker, IDAT and IEND chunks of a 1x1 pixel 32bit png 1 a single black pixel.
    private static readonly byte[] Raw1X1PngIdatAndIend =
    [
        // IDAT
        0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x18,
        0x57, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04,
        0x00, 0x01,

        // IDAT CRC
        0x5C, 0xCD, 0xFF, 0x69,

        // IEND
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,

        // IEND CRC
        0xAE, 0x42, 0x60, 0x82
    ];

    [Theory]
    [InlineData((uint)PngChunkType.Header)] // IHDR
    [InlineData((uint)PngChunkType.Palette)] // PLTE
    /* [InlineData(PngChunkTypes.Data)] TODO: Figure out how to test this */
    public void Decode_IncorrectCRCForCriticalChunk_ExceptionIsThrown(uint chunkType)
    {
        string chunkName = GetChunkTypeName(chunkType);

        using (MemoryStream memStream = new())
        {
            WriteHeaderChunk(memStream);
            WriteChunk(memStream, chunkName);
            WriteDataChunk(memStream);

            InvalidImageContentException exception =
                Assert.Throws<InvalidImageContentException>(() => PngDecoder.Instance.Decode<Rgb24>(DecoderOptions.Default, memStream));

            Assert.Equal($"CRC Error. PNG {chunkName} chunk is corrupt!", exception.Message);
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/3078
    [Fact]
    public void Decode_TruncatedPhysChunk_ExceptionIsThrown()
    {
        // 24 bytes — PNG signature + truncated pHYs chunk
        byte[] payload = Convert.FromHexString(
            "89504e470d0a1a0a3030303070485973" +
            "3030303030303030");

        using MemoryStream stream = new(payload);
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(stream));

        Assert.Equal("pHYs chunk is too short", exception.Message);
    }

    // https://github.com/SixLabors/ImageSharp/issues/3093
    [Fact]
    public void Decode_TruncatedFrameControlChunk_ExceptionIsThrown()
    {
        // PNG signature + truncated frame control chunk
        byte[] payload = Convert.FromHexString(
            "89504e470d0a1a0a424d3a00000000007f000000000028030405060000000100" +
            "000101002000000000000000000000000000ff00006663544cff190000000000" +
            "010000424d000100000101002000000000");

        using MemoryStream stream = new(payload);
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(stream));

        Assert.Equal("The frame control chunk does not contain enough data!", exception.Message);
    }

    [Fact]
    public void DecodeAndIdentify_WithDuplicateHeader_ThrowInvalidImageContentException()
    {
        using MemoryStream payloadStream = new();
        payloadStream.Write(Raw1X1PngIhdrAndpHYs);
        payloadStream.Write(Raw1X1PngIhdrAndpHYs.AsSpan(8, 25));
        payloadStream.Write(Raw1X1PngIdatAndIend);
        byte[] payload = payloadStream.ToArray();

        Assert.Throws<InvalidImageContentException>(() => Image.Load(payload));
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(payload));
    }

    /// <summary>
    /// Chunk recovery must not replace the header after scanline storage has been sized.
    /// </summary>
    /// <param name="integrity">The segment integrity policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeAndIdentify_WithChunkRecovery_FollowIntegrityPolicy(SegmentIntegrityHandling integrity)
    {
        byte[] data = TestFile.Create(TestImages.Png.DuplicateHeaderChunkResync).Bytes;
        DecoderOptions options = new() { SegmentIntegrityHandling = integrity };

        Assert.Throws<InvalidImageContentException>(() => Image.Load<La16>(options, data));

        if (integrity == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
        }
        else
        {
            // Identify skips the image-data payload rather than decoding and resynchronizing within it.
            Assert.Equal(new Size(1, 1), Image.Identify(options, data).Size);
        }
    }

    /// <summary>
    /// Corrupt compressed metadata follows the ancillary policy without preventing valid pixel decoding.
    /// </summary>
    /// <param name="chunkType">The compressed metadata chunk type.</param>
    /// <param name="integrity">The segment integrity policy.</param>
    [Theory]
    [InlineData("iCCP", SegmentIntegrityHandling.Strict)]
    [InlineData("iCCP", SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData("iCCP", SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData("zTXt", SegmentIntegrityHandling.Strict)]
    [InlineData("zTXt", SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData("zTXt", SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData("iTXt", SegmentIntegrityHandling.Strict)]
    [InlineData("iTXt", SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData("iTXt", SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_InvalidCompressedMetadata_FollowsIntegrityPolicy(string chunkType, SegmentIntegrityHandling integrity)
    {
        // iTXt adds a compression flag and empty language/translated-keyword fields before the zlib stream.
        byte[] fields = chunkType == "iTXt" ? [(byte)'p', 0, 1, 0, 0, 0] : [(byte)'p', 0, 0];

        // The zlib header is valid, but the first deflate block uses reserved block type 3.
        byte[] chunk = [.. Encoding.ASCII.GetBytes(chunkType), .. fields, 0x78, 0x9C, 0x07, 0, 0, 0, 0];
        using MemoryStream stream = new();
        stream.Write(Raw1X1PngIhdrAndpHYs);
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, chunk.Length - 4);
        stream.Write(buffer);
        stream.Write(chunk);
        Crc32 crc = new();
        crc.Append(chunk);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc.GetCurrentHashAsUInt32());
        stream.Write(buffer);
        stream.Write(Raw1X1PngIdatAndIend);
        byte[] data = stream.ToArray();
        DecoderOptions options = new() { SegmentIntegrityHandling = integrity };

        if (integrity == SegmentIntegrityHandling.Strict)
        {
            InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgb24>(options, data));
            Assert.IsType<InvalidDataException>(exception.InnerException);
        }
        else
        {
            using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
            Assert.Equal(new Size(1, 1), image.Size);
            Assert.Equal(default(Rgb24), image[0, 0]);
            Assert.Null(image.Metadata.IccProfile);
            Assert.Empty(image.Metadata.GetPngMetadata().TextData);
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/3079
    [Fact]
    public void Decode_CompressedTxtChunk_WithTruncatedData_DoesNotThrow()
    {
        byte[] payload = [137, 80, 78, 71, 13, 10, 26, 10, // PNG signature
                            0, 0, 0, 13, // chunk length 13 bytes
                            73, 72, 68, 82, // chunk type IHDR
                            0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, // data
                            55, 110, 249, 36, // checksum
                            0, 0, 0, 2, // chunk length
                            122, 84, 88, 116, // chunk type zTXt
                            1, 0, // truncated data
                            100, 138, 166, 20, // crc
                            0, 0, 0, 10, // chunk length 10 bytes
                            73, 68, 65, 84, // chunk type IDAT
                            120, 1, 99, 96, 0, 0, 0, 2, 0, 1, // data
                            115, 117, 1, 24, // checksum
                            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]; // end chunk

        using MemoryStream stream = new(payload);
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
    }

    // https://github.com/SixLabors/ImageSharp/issues/3079
    [Fact]
    public void Decode_InternationalText_WithTruncatedData_DoesNotThrow()
    {
        byte[] payload = [137, 80, 78, 71, 13, 10, 26, 10, // PNG signature
                            0, 0, 0, 13, // chunk length 13 bytes
                            73, 72, 68, 82, // chunk type IHDR
                            0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, // data
                            55, 110, 249, 36, // checksum
                            0, 0, 0, 2, // chunk length
                            105, 84, 88, 116, // chunk type iTXt
                            1, 0, // truncated data
                            225, 200, 214, 33, // crc
                            0, 0, 0, 10, // chunk length 10 bytes
                            73, 68, 65, 84, // chunk type IDAT
                            120, 1, 99, 96, 0, 0, 0, 2, 0, 1, // data
                            115, 117, 1, 24, // checksum
                            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]; // end chunk

        using MemoryStream stream = new(payload);
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
    }

    // https://github.com/SixLabors/ImageSharp/issues/3079
    [Fact]
    public void Decode_InternationalText_WithTruncatedDataAfterLanguageTag_DoesNotThrow()
    {
        byte[] payload = [137, 80, 78, 71, 13, 10, 26, 10, // PNG signature
                            0, 0, 0, 13, // chunk length 13 bytes
                            73, 72, 68, 82, // chunk type IHDR
                            0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, // data
                            55, 110, 249, 36, // checksum
                            0, 0, 0, 21, // chunk length
                            105, 84, 88, 116, // chunk type iTXt
                            73, 110, 116, 101, 114, 110, 97, 116, 105, 111, 110, 97, 108, 50, 0, 0, 0, 114, 117, 115, 0, // truncated data after language tag
                            225, 200, 214, 33, // crc
                            0, 0, 0, 10, // chunk length 10 bytes
                            73, 68, 65, 84, // chunk type IDAT
                            120, 1, 99, 96, 0, 0, 0, 2, 0, 1, // data
                            115, 117, 1, 24, // checksum
                            0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]; // end chunk

        using MemoryStream stream = new(payload);
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
    }

    [Fact]
    public void Decode_tRnsChunk_WithAlphaLengthGreaterColorTableLength_ShouldNotThrowException()
    {
        byte[] payload = [137, 80, 78, 71, 13, 10, 26, 10, // PNG signature
                            0, 0, 0, 13, // chunk length 13 bytes
                            73, 72, 68, 82, // chunk type IHDR
                            0, 0, 0, 1, 0, 0, 0, 1, 8, 3, 0, 0, 0, // data
                            40, 203, 52, 187, // crc
                            0, 0, 0, 6, // chunk length 6 bytes
                            80, 76, 84, 69, // chunk type palettte
                            255, 0, 0, 0, 255, 0, // data
                            210, 135, 239, 113, // crc
                            0, 0, 0, 18, // chunk length
                            116, 82, 78, 83, // chunk type tRns
                            48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, // data
                            0, 0, 0, 10, // chunk length
                            73, 68, 65, 84, // chunk type data
                            120, 156, 99, 96, 0, 0, 0, 2, 0, 1, 72, 175, 164, 113]; // alpha.Length > colorTable.Length

        using MemoryStream stream = new(payload);
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
    }

    /// <summary>
    /// The compressed image data may be split across data chunks at any byte, including inside the zlib header,
    /// and zero-length data chunks are permitted. Every layout must decode to the same pixels.
    /// </summary>
    /// <param name="layout">The name of the data chunk layout to write.</param>
    [Theory]
    [InlineData("LeadingEmpty")]
    [InlineData("ConsecutiveLeadingEmpty")]
    [InlineData("FirstOneByte")]
    [InlineData("EmptyWithinHeader")]
    [InlineData("EveryOneByte")]
    [InlineData("EveryOneByteWithEmpty")]
    [InlineData("EmptyBetween")]
    [InlineData("TrailingEmpty")]
    public void Decode_CompressedDataSplitAcrossDataChunks_DecodesPixelExact(string layout)
    {
        using Image<Rgba32> expected = CreateNoiseImage(40, 30, 1);
        using MemoryStream stream = new();
        expected.Save(stream, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
        List<(string Type, byte[] Data)> chunks = ReadChunks(stream.ToArray());

        byte[] zlib = [.. chunks.Where(c => c.Type == "IDAT").SelectMany(c => c.Data)];
        byte[][] segments = layout switch
        {
            "LeadingEmpty" => [[], zlib],
            "ConsecutiveLeadingEmpty" => [[], [], zlib],
            "FirstOneByte" => [zlib[..1], zlib[1..]],
            "EmptyWithinHeader" => [zlib[..1], [], zlib[1..]],
            "EveryOneByte" => [.. zlib.Select(b => new[] { b })],
            "EveryOneByteWithEmpty" => [.. zlib.SelectMany(b => new[] { [b], Array.Empty<byte>() })],
            "EmptyBetween" => [zlib[..(zlib.Length / 2)], [], zlib[(zlib.Length / 2)..]],
            "TrailingEmpty" => [zlib, []],
            _ => throw new ArgumentOutOfRangeException(nameof(layout)),
        };

        // Replace the encoder's data chunks with the requested layout, keeping every other chunk in place.
        using MemoryStream output = new();
        output.Write(stream.ToArray().AsSpan(0, 8));
        foreach ((string type, byte[] data) in chunks)
        {
            if (type == "IDAT")
            {
                continue;
            }

            if (type == "IEND")
            {
                foreach (byte[] segment in segments)
                {
                    WriteChunk(output, "IDAT", segment);
                }
            }

            WriteChunk(output, type, data);
        }

        using Image<Rgba32> actual = Image.Load<Rgba32>(output.ToArray());

        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// A frame data chunk may carry nothing but its sequence number. Such chunks must not end the frame's
    /// compressed data early, wherever they fall.
    /// </summary>
    [Fact]
    public void Decode_EmptyFrameDataChunks_DecodesPixelExact()
    {
        using Image<Rgba32> image = CreateNoiseImage(40, 30, 1);
        using (Image<Rgba32> frame = CreateNoiseImage(40, 30, 2))
        {
            image.Frames.AddFrame(frame.Frames.RootFrame);
        }

        using MemoryStream stream = new();
        image.Save(stream, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
        byte[] encoded = stream.ToArray();
        List<(string Type, byte[] Data)> chunks = ReadChunks(encoded);
        Assert.Contains(chunks, c => c.Type == "fdAT");

        // Split every frame data chunk inside its zlib header, around an empty frame data chunk, and renumber
        // the shared fcTL/fdAT sequence so the result stays a well-formed animation.
        using MemoryStream output = new();
        output.Write(encoded.AsSpan(0, 8));
        uint sequenceNumber = 0;
        foreach ((string type, byte[] data) in chunks)
        {
            if (type == "fdAT")
            {
                ReadOnlySpan<byte> payload = data.AsSpan(4);
                WriteChunk(output, type, [.. GetSequenceNumber(sequenceNumber++), .. payload[..1]]);
                WriteChunk(output, type, GetSequenceNumber(sequenceNumber++));
                WriteChunk(output, type, [.. GetSequenceNumber(sequenceNumber++), .. payload[1..]]);
                continue;
            }

            if (type == "fcTL")
            {
                BinaryPrimitives.WriteUInt32BigEndian(data, sequenceNumber++);
            }

            WriteChunk(output, type, data);
        }

        using Image<Rgba32> expected = Image.Load<Rgba32>(encoded);
        using Image<Rgba32> actual = Image.Load<Rgba32>(output.ToArray());

        Assert.Equal(expected.Frames.Count, actual.Frames.Count);
        for (int i = 0; i < expected.Frames.Count; i++)
        {
            using Image<Rgba32> expectedFrame = expected.Frames.CloneFrame(i);
            using Image<Rgba32> actualFrame = actual.Frames.CloneFrame(i);
            ImageComparer.Exact.VerifySimilarity(expectedFrame, actualFrame);
        }
    }

    /// <summary>
    /// A palette chunk too short to hold one entry leaves no colors to index, so it is reported as a missing palette
    /// instead of reading outside the color table.
    /// </summary>
    /// <param name="interlaceMode">The interlace mode of the image.</param>
    [Theory]
    [InlineData(PngInterlaceMode.None)]
    [InlineData(PngInterlaceMode.Adam7)]
    public void Decode_PaletteChunkShorterThanOneEntry_ThrowsInvalidImageContentException(PngInterlaceMode interlaceMode)
    {
        // Zeroed scanlines: filter type None and palette index 0. A 9x9 image has 9 rows of 1 + 9 bytes, or
        // 100 bytes across the seven Adam7 passes, each row of which also starts with a filter byte.
        byte[] indices = interlaceMode == PngInterlaceMode.Adam7 ? new byte[100] : new byte[90];
        using MemoryStream stream = new();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", CreateHeaderData(9, 9, 8, PngColorType.Palette, interlaceMode));
        WriteChunk(stream, "PLTE", [10, 20]);
        WriteChunk(stream, "IDAT", Compress(indices));
        WriteChunk(stream, "IEND", []);

        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(stream.ToArray()));
    }

    /// <summary>
    /// 16-bit grayscale tRNS samples are big-endian, so 0x0102 is transparent and its byte-swapped value is not.
    /// </summary>
    [Fact]
    public void Decode_16BitGrayscaleTransparency_IsBigEndian()
    {
        using MemoryStream stream = new();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", CreateHeaderData(2, 1, 16, PngColorType.Grayscale, PngInterlaceMode.None));
        WriteChunk(stream, "tRNS", [0x01, 0x02]);
        WriteChunk(stream, "IDAT", Compress([0, 0x01, 0x02, 0x02, 0x01]));
        WriteChunk(stream, "IEND", []);
        byte[] data = stream.ToArray();

        Assert.Equal(new L16(0x0102), Image.Identify(data).Metadata.GetPngMetadata().TransparentColor?.ToPixel<L16>());

        using Image<Rgba64> image = Image.Load<Rgba64>(data);
        Assert.Equal(ushort.MinValue, image[0, 0].A);
        Assert.Equal(ushort.MaxValue, image[1, 0].A);
    }

    /// <summary>
    /// 16-bit truecolor tRNS samples are big-endian, so (0x0102, 0x0304, 0x0506) is transparent and its byte-swapped
    /// value is not.
    /// </summary>
    [Fact]
    public void Decode_16BitRgbTransparency_IsBigEndian()
    {
        using MemoryStream stream = new();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", CreateHeaderData(2, 1, 16, PngColorType.Rgb, PngInterlaceMode.None));
        WriteChunk(stream, "tRNS", [0x01, 0x02, 0x03, 0x04, 0x05, 0x06]);
        WriteChunk(stream, "IDAT", Compress([0, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x02, 0x01, 0x04, 0x03, 0x06, 0x05]));
        WriteChunk(stream, "IEND", []);
        byte[] data = stream.ToArray();

        Rgb48 expected = new(0x0102, 0x0304, 0x0506);
        Assert.Equal(expected, Image.Identify(data).Metadata.GetPngMetadata().TransparentColor?.ToPixel<Rgb48>());

        using Image<Rgba64> image = Image.Load<Rgba64>(data);
        Assert.Equal(ushort.MinValue, image[0, 0].A);
        Assert.Equal(ushort.MaxValue, image[1, 0].A);
    }

    /// <summary>
    /// Creates an image filled with reproducible random colors.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="seed">The random seed.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    private static Image<Rgba32> CreateNoiseImage(int width, int height, int seed)
    {
        Random random = new(seed);
        Image<Rgba32> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }

        return image;
    }

    /// <summary>
    /// Splits a PNG file into its chunks without validating them.
    /// </summary>
    /// <param name="data">The PNG file, including the signature.</param>
    /// <returns>The type and data of each chunk, in file order.</returns>
    private static List<(string Type, byte[] Data)> ReadChunks(byte[] data)
    {
        List<(string Type, byte[] Data)> chunks = [];
        int offset = 8;
        while (offset + 12 <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            chunks.Add((Encoding.ASCII.GetString(data, offset + 4, 4), data.AsSpan(offset + 8, length).ToArray()));
            offset += length + 12;
        }

        return chunks;
    }

    /// <summary>
    /// Writes a chunk with its length, type, data and a valid CRC.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="type">The four character chunk type.</param>
    /// <param name="data">The chunk data.</param>
    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);

        // The CRC covers the chunk type and data but not the length.
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        Crc32 crc = new();
        crc.Append(typeBytes);
        crc.Append(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc.GetCurrentHashAsUInt32());
        stream.Write(buffer);
    }

    /// <summary>
    /// Creates IHDR chunk data using compression method 0 and filter method 0.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="bitDepth">The bit depth.</param>
    /// <param name="colorType">The color type.</param>
    /// <param name="interlaceMode">The interlace mode.</param>
    /// <returns>The chunk data.</returns>
    private static byte[] CreateHeaderData(int width, int height, byte bitDepth, PngColorType colorType, PngInterlaceMode interlaceMode)
    {
        byte[] data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        data[8] = bitDepth;
        data[9] = (byte)colorType;
        data[12] = (byte)interlaceMode;
        return data;
    }

    /// <summary>
    /// Compresses filtered scanlines into a zlib stream.
    /// </summary>
    /// <param name="scanlines">The filtered scanlines.</param>
    /// <returns>The zlib stream.</returns>
    private static byte[] Compress(byte[] scanlines)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(scanlines);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Gets the big-endian bytes of an APNG sequence number.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number.</param>
    /// <returns>The encoded sequence number.</returns>
    private static byte[] GetSequenceNumber(uint sequenceNumber)
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, sequenceNumber);
        return data;
    }

    private static string GetChunkTypeName(uint value)
    {
        byte[] data = new byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(data, value);

        return Encoding.ASCII.GetString(data);
    }

    private static void WriteHeaderChunk(MemoryStream memStream) =>

        // Writes a 1x1 32bit png header chunk containing a single black pixel.
        memStream.Write(Raw1X1PngIhdrAndpHYs, 0, Raw1X1PngIhdrAndpHYs.Length);

    private static void WriteChunk(MemoryStream memStream, string chunkName)
    {
        // Needs a minimum length of 9 for pHYs chunk.
        memStream.Write([0, 0, 0, 9], 0, 4);
        memStream.Write(Encoding.ASCII.GetBytes(chunkName), 0, 4); // 4 bytes chunk header
        memStream.Write([0, 0, 0, 0, 0, 0, 0, 0, 0], 0, 9); // 9 bytes of chunk data
        memStream.Write([0, 0, 0, 0], 0, 4); // Junk Crc
    }

    private static void WriteDataChunk(MemoryStream memStream)
    {
        // Writes a 1x1 32bit png data chunk containing a single black pixel.
        memStream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
        memStream.Position = 0;
    }
}
