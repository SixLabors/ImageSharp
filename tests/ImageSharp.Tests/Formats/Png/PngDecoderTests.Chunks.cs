// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

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
