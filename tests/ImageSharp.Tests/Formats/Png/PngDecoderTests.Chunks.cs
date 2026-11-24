// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

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
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

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
    /// Checks stored IDAT layouts and applies the configured policy to the input's CRC errors.
    /// </summary>
    /// <param name="path">The stored PNG input.</param>
    /// <param name="integrity">The segment integrity policy.</param>
    /// <param name="hasInvalidCrc">Whether the input contains an invalid image-data CRC.</param>
    [Theory]
    [InlineData(TestImages.Png.IdatLeadingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatConsecutiveLeadingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatFirstOneByte, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatEmptyWithinHeader, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatEveryOneByte, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatEveryOneByteWithEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatEmptyBetween, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatTrailingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.IdatEmptyBadCrc, SegmentIntegrityHandling.Strict, true)]
    [InlineData(TestImages.Png.IdatEmptyBadCrc, SegmentIntegrityHandling.IgnoreAncillary, true)]
    [InlineData(TestImages.Png.IdatEmptyBadCrc, SegmentIntegrityHandling.IgnoreImageData, true)]
    public void Decode_CompressedDataSplitAcrossDataChunks_FollowsIntegrityPolicy(string path, SegmentIntegrityHandling integrity, bool hasInvalidCrc)
    {
        byte[] data = TestFile.Create(path).Bytes;
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
        if (hasInvalidCrc && integrity != SegmentIntegrityHandling.IgnoreImageData)
        {
            InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
            });

            Assert.Equal("CRC Error. PNG IDAT chunk is corrupt!", exception.Message);
            return;
        }

        using Image<Rgba32> expected = TestFile.Create(TestImages.Png.Noise).CreateRgba32Image();
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Checks stored empty frame payloads without changing their pixels or bypassing image-data CRC policy.
    /// </summary>
    /// <param name="path">The stored APNG input.</param>
    /// <param name="integrity">The segment integrity policy.</param>
    /// <param name="hasInvalidCrc">Whether the input contains invalid frame-data CRCs.</param>
    [Theory]
    [InlineData(TestImages.Png.FdatLeadingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.FdatConsecutiveLeadingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.FdatEmptyWithinHeader, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.FdatTrailingEmpty, SegmentIntegrityHandling.Strict, false)]
    [InlineData(TestImages.Png.FdatEmptyBadCrc, SegmentIntegrityHandling.Strict, true)]
    [InlineData(TestImages.Png.FdatEmptyBadCrc, SegmentIntegrityHandling.IgnoreAncillary, true)]
    [InlineData(TestImages.Png.FdatEmptyBadCrc, SegmentIntegrityHandling.IgnoreImageData, true)]
    public void Decode_EmptyFrameDataChunks_FollowsIntegrityPolicy(string path, SegmentIntegrityHandling integrity, bool hasInvalidCrc)
    {
        byte[] data = TestFile.Create(path).Bytes;
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
        if (hasInvalidCrc && integrity != SegmentIntegrityHandling.IgnoreImageData)
        {
            InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
            });

            Assert.Equal("CRC Error. PNG fdAT chunk is corrupt!", exception.Message);
            return;
        }

        using Image<Rgba32> expected = TestFile.Create(TestImages.Png.APng).CreateRgba32Image();
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Rejects stored frame chunks without a complete sequence number, including chunks within compressed data.
    /// </summary>
    /// <param name="path">The malformed APNG input.</param>
    /// <param name="integrity">The segment integrity policy.</param>
    [Theory]
    [InlineData(TestImages.Png.FdatShort0, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.FdatShort3, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.FdatTruncatedSequenceBetween, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.FdatShort0, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Png.FdatShort3, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Png.FdatTruncatedSequenceBetween, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Png.FdatShort0, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Png.FdatShort3, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Png.FdatTruncatedSequenceBetween, SegmentIntegrityHandling.IgnoreImageData)]
    public void DecodeAndIdentify_FrameDataWithoutSequenceNumber_ThrowsInvalidImageContentException(string path, SegmentIntegrityHandling integrity)
    {
        byte[] data = TestFile.Create(path).Bytes;
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
        });

        Assert.Equal("The frame data chunk does not contain a sequence number.", exception.Message);
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
    }

    /// <summary>
    /// Rejects stored indexed PNGs without one complete RGB palette entry in every integrity mode.
    /// </summary>
    /// <param name="path">The malformed PNG input, including its interlace mode and palette length.</param>
    /// <param name="integrity">The segment integrity policy.</param>
    [Theory]
    [InlineData(TestImages.Png.PaletteShort0, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteShort1, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteShort2, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteAdam7Short0, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteAdam7Short1, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteAdam7Short2, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Png.PaletteShort2, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Png.PaletteShort2, SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_PaletteChunkShorterThanOneEntry_ThrowsInvalidImageContentException(string path, SegmentIntegrityHandling integrity)
    {
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> actual = Image.Load<Rgba32>(options, TestFile.Create(path).Bytes);
        });
    }

    /// <summary>
    /// Checks that the stored 16-bit marker 0x0102 selects that gray sample, not the byte-swapped sample 0x0201.
    /// </summary>
    [Fact]
    public void Decode_16BitGrayscaleTransparency_IsBigEndian()
    {
        byte[] data = TestFile.Create(TestImages.Png.TrnsGray16BigEndian).Bytes;
        Assert.Equal(new L16(0x0102), Image.Identify(data).Metadata.GetPngMetadata().TransparentColor?.ToPixel<L16>());

        using Image<Rgba64> image = Image.Load<Rgba64>(data);
        Assert.Equal(ushort.MinValue, image[0, 0].A);
        Assert.Equal(ushort.MaxValue, image[1, 0].A);
    }

    /// <summary>
    /// Checks that the stored marker (0x0102, 0x0304, 0x0506) selects that RGB sample, not its byte-swapped sample.
    /// </summary>
    [Fact]
    public void Decode_16BitRgbTransparency_IsBigEndian()
    {
        byte[] data = TestFile.Create(TestImages.Png.TrnsRgb16BigEndian).Bytes;
        Rgb48 expected = new(0x0102, 0x0304, 0x0506);
        Assert.Equal(expected, Image.Identify(data).Metadata.GetPngMetadata().TransparentColor?.ToPixel<Rgb48>());

        using Image<Rgba64> image = Image.Load<Rgba64>(data);
        Assert.Equal(ushort.MinValue, image[0, 0].A);
        Assert.Equal(ushort.MaxValue, image[1, 0].A);
    }

    /// <summary>
    /// Checks that image data that ends before the last row keeps the decoded rows with the default options.
    /// Strict and IgnoreAncillary throw for the same input.
    /// </summary>
    /// <param name="interlace">The interlace mode of the encoded image.</param>
    [Theory]
    [InlineData(PngInterlaceMode.None)]
    [InlineData(PngInterlaceMode.Adam7)]
    public void Decode_TruncatedImageData_KeepsDecodedRowsByDefaultAndThrowsWithStrict(PngInterlaceMode interlace)
    {
        using Image<Rgba32> source = CreateOpaqueNoiseImage(32, 32, 1);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder { InterlaceMethod = interlace, ColorType = PngColorType.RgbWithAlpha });

        // Keep one quarter of the compressed data in a chunk with a valid CRC.
        // Only the truncation of the image data is then wrong with the input.
        byte[] kept = [];
        byte[] data = ReplaceImageData(encoded.ToArray(), compressed => kept = compressed[..(compressed.Length / 4)]);

        // The decoder keeps each complete scanline and drops a partial scanline. The other pixels keep the cleared value.
        using Image<Rgba32> expected = CreateCompleteScanlineImage(source.Frames.RootFrame, GetInflatedLength(kept), interlace);
        using (Image<Rgba32> image = Image.Load<Rgba32>(data))
        {
            ImageComparer.Exact.VerifySimilarity(expected, image);
        }

        foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary })
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
            });

            Assert.Equal("The PNG image data ends before the last scanline.", exception.Message);
        }
    }

    /// <summary>
    /// Checks that a damaged zlib header or deflate stream gives a cleared image with the default options.
    /// Strict throws for the same input.
    /// </summary>
    /// <param name="damagedHeader">True to damage the zlib header. False to damage the first deflate block.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decode_DamagedCompressedImageData_KeepsClearedPixelsByDefaultAndThrowsWithStrict(bool damagedHeader)
    {
        using Image<Rgba32> source = CreateOpaqueNoiseImage(8, 8, 2);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });

        // Compression method 15 is not deflate. The second payload has a valid zlib header, then a deflate
        // block with the reserved block type 3.
        byte[] payload = damagedHeader ? [0x7F, 0x9C, 0x07, 0, 0, 0, 0] : [0x78, 0x9C, 0x07, 0, 0, 0, 0];
        byte[] data = ReplaceImageData(encoded.ToArray(), _ => payload);

        // No scanline decodes, so every pixel keeps the cleared value.
        using (Image<Rgba32> cleared = new(source.Width, source.Height))
        using (Image<Rgba32> image = Image.Load<Rgba32>(data))
        {
            ImageComparer.Exact.VerifySimilarity(cleared, image);
        }

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
        });

        string expected = damagedHeader
            ? "The PNG image data does not start with a valid zlib header."
            : "The PNG image data is damaged.";
        Assert.Equal(expected, exception.Message);
    }

    /// <summary>
    /// Checks that a truncated APNG frame keeps the earlier frames and its own decoded rows with the default options.
    /// Strict throws for the same input.
    /// </summary>
    /// <param name="blendMode">The blend mode of the truncated frame.</param>
    [Theory]
    [InlineData(FrameBlendMode.Source)]
    [InlineData(FrameBlendMode.Over)]
    public void Decode_TruncatedFrameData_KeepsDecodedFramesByDefaultAndThrowsWithStrict(FrameBlendMode blendMode)
    {
        using Image<Rgba32> source = CreateOpaqueNoiseImage(16, 16, 3);
        using (Image<Rgba32> secondFrame = CreateOpaqueNoiseImage(16, 16, 4))
        {
            source.Frames.AddFrame(secondFrame.Frames.RootFrame).Metadata.GetPngMetadata().BlendMode = blendMode;
        }

        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
        byte[] original = encoded.ToArray();

        // Find the last fdAT chunk. It holds the end of the compressed data of the last frame.
        int offset = 8;
        int lastFrameDataOffset = -1;
        while (offset < original.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(original.AsSpan(offset, 4));
            if (Encoding.ASCII.GetString(original, offset + 4, 4) == "fdAT")
            {
                lastFrameDataOffset = offset;
            }

            offset += length + 12;
        }

        Assert.NotEqual(-1, lastFrameDataOffset);

        // Keep the sequence number and one quarter of the compressed data, with a valid CRC.
        int frameDataLength = BinaryPrimitives.ReadInt32BigEndian(original.AsSpan(lastFrameDataOffset, 4));
        ReadOnlySpan<byte> frameData = original.AsSpan(lastFrameDataOffset + 8, frameDataLength);
        int keptLength = sizeof(uint) + ((frameDataLength - sizeof(uint)) / 4);
        int nextChunkOffset = lastFrameDataOffset + frameDataLength + 12;

        using MemoryStream truncated = new();
        truncated.Write(original, 0, lastFrameDataOffset);
        WriteChunkWithCrc(truncated, "fdAT", frameData[..keptLength]);
        truncated.Write(original, nextChunkOffset, original.Length - nextChunkOffset);
        byte[] data = truncated.ToArray();

        // The first frame is complete. The last frame keeps each complete scanline of the data after its sequence number.
        // The other pixels of the last frame keep the fill value, transparent black. Source blending replaces the
        // canvas with the frame pixels. Over blending of a transparent pixel keeps the canvas pixel, which is the
        // first frame. See PNG Third Edition, section 11.3.6.2 "fcTL Frame Control Chunk", blend_op.
        using Image<Rgba32> expected = source.Frames.CloneFrame(0);
        using (Image<Rgba32> lastFrame = CreateCompleteScanlineImage(
            source.Frames[1],
            GetInflatedLength(frameData[sizeof(uint)..keptLength]),
            PngInterlaceMode.None))
        {
            if (blendMode == FrameBlendMode.Over)
            {
                using Image<Rgba32> canvas = source.Frames.CloneFrame(0);
                canvas.Mutate(x => x.DrawImage(lastFrame, 1F));
                expected.Frames.AddFrame(canvas.Frames.RootFrame);
            }
            else
            {
                expected.Frames.AddFrame(lastFrame.Frames.RootFrame);
            }
        }

        using (Image<Rgba32> image = Image.Load<Rgba32>(data))
        {
            ImageComparer.Exact.VerifySimilarity(expected, image);
        }

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
        });

        Assert.Equal("The PNG image data ends before the last scanline.", exception.Message);
    }

    /// <summary>
    /// Checks that an interlaced image less than five pixels wide decodes exactly with Strict. Some Adam7 passes of
    /// such an image have no columns, so they are empty and have no scanlines. See PNG Third Edition, section 8.1
    /// "Interlace methods", and section 9.1 "Filter methods and filter types".
    /// </summary>
    /// <param name="width">The image width.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Decode_InterlacedImageWithEmptyPasses_DecodesAllPixelsWithStrict(int width)
    {
        // Eight rows give rows to each pass that has columns.
        using Image<Rgba32> source = CreateOpaqueNoiseImage(width, 8, width);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder
        {
            InterlaceMethod = PngInterlaceMode.Adam7,
            ColorType = PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8
        });

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using Image<Rgba32> image = Image.Load<Rgba32>(options, encoded.ToArray());
        ImageComparer.Exact.VerifySimilarity(source, image);
    }

    /// <summary>
    /// Checks that an interlaced APNG frame with a vertical offset decodes exactly with Strict. The rows of each
    /// Adam7 pass start at the top of the frame region. See PNG Third Edition, section 11.3.6.2 "fcTL Frame Control
    /// Chunk", y_offset.
    /// </summary>
    [Fact]
    public void Decode_InterlacedFrameWithVerticalOffset_DecodesAllPixelsWithStrict()
    {
        using Image<Rgba32> source = CreateOpaqueNoiseImage(16, 16, 5);
        using (Image<Rgba32> secondFrame = source.Frames.CloneFrame(0))
        using (Image<Rgba32> changedRows = CreateOpaqueNoiseImage(16, 8, 6))
        {
            // The second frame differs from the first frame only in rows 5 to 12. The encoder writes only the
            // changed region, so the frame control has a y_offset of 5.
            secondFrame.Mutate(x => x.DrawImage(changedRows, new Point(0, 5), 1F));
            source.Frames.AddFrame(secondFrame.Frames.RootFrame);
        }

        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder
        {
            InterlaceMethod = PngInterlaceMode.Adam7,
            ColorType = PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8
        });

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using Image<Rgba32> image = Image.Load<Rgba32>(options, encoded.ToArray());
        ImageComparer.Exact.VerifySimilarity(source, image);
    }

    /// <summary>
    /// Checks that a valid file decodes in full in each mode when its last IDAT chunks hold no deflate data. The IDAT
    /// chunks hold one zlib stream. The stream can be split at any byte (PNG Third Edition, section 10.2
    /// "Compression of the sequence of filtered scanlines"). Here the second to last IDAT chunk holds only the Adler-32
    /// checksum, and the last IDAT chunk is empty. The decoder can finish all rows before it reads these chunks.
    /// </summary>
    /// <param name="integrity">The segment integrity policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.Strict)]
    public void Decode_DataChunksAfterDeflateData_DecodesAllPixels(SegmentIntegrityHandling integrity)
    {
        using Image<Rgba32> source = CreateOpaqueNoiseImage(8, 8, 7);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });

        // The zlib stream ends with the 4-byte Adler-32 checksum (RFC 1950, section 2.2 "Data format").
        byte[] checksum = [];
        byte[] data = ReplaceImageData(encoded.ToArray(), compressed =>
        {
            checksum = compressed[^4..];
            return compressed[..^4];
        });

        // The encoder writes the IEND chunk, 12 bytes, directly after the image data.
        int endChunkOffset = data.Length - 12;
        using MemoryStream split = new();
        split.Write(data, 0, endChunkOffset);
        WriteChunkWithCrc(split, "IDAT", checksum);
        WriteChunkWithCrc(split, "IDAT", []);
        split.Write(data, endChunkOffset, 12);

        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
        using Image<Rgba32> image = Image.Load<Rgba32>(options, split.ToArray());
        ImageComparer.Exact.VerifySimilarity(source, image);
    }

    /// <summary>
    /// Creates an image of opaque random pixels. Random pixels do not compress well, so each part of the compressed
    /// data holds a similar number of rows.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="seed">The seed of the random values.</param>
    /// <returns>The image.</returns>
    private static Image<Rgba32> CreateOpaqueNoiseImage(int width, int height, int seed)
    {
        Random random = new(seed);
        Image<Rgba32> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), byte.MaxValue);
            }
        }

        return image;
    }

    /// <summary>
    /// Gets the number of bytes that a zlib stream inflates to before it ends or becomes invalid.
    /// </summary>
    /// <param name="zlibData">The zlib stream. It can end early.</param>
    /// <returns>The number of inflated bytes.</returns>
    private static int GetInflatedLength(ReadOnlySpan<byte> zlibData)
    {
        using MemoryStream input = new(zlibData.ToArray(), false);
        using ZLibStream inflater = new(input, CompressionMode.Decompress);
        byte[] buffer = new byte[4096];
        int length = 0;
        try
        {
            int count;
            while ((count = inflater.Read(buffer)) > 0)
            {
                length += count;
            }
        }
        catch (InvalidDataException)
        {
            // A truncated stream can end inside a deflate block. The bytes before that point stay valid.
        }

        return length;
    }

    /// <summary>
    /// Creates the image that a decoder keeps from filtered 8-bit RGBA scanlines that end early. Each complete scanline
    /// keeps its source pixels. A partial scanline and the scanlines after it keep the cleared value.
    /// </summary>
    /// <param name="source">The encoded frame.</param>
    /// <param name="inflatedLength">The number of inflated bytes before the data ends.</param>
    /// <param name="interlace">The interlace mode of the encoded frame.</param>
    /// <returns>The expected image.</returns>
    private static Image<Rgba32> CreateCompleteScanlineImage(ImageFrame<Rgba32> source, int inflatedLength, PngInterlaceMode interlace)
    {
        Image<Rgba32> expected = new(source.Width, source.Height);

        // Adam7 has seven passes. Each pass has its own start and step in each direction: PNG Third Edition,
        // section 8.1 "Interlace methods", https://www.w3.org/TR/png-3/#8InterlaceMethods. A frame without
        // interlace is one pass that starts at 0 and steps by 1.
        ReadOnlySpan<int> firstRow = interlace == PngInterlaceMode.Adam7 ? [0, 0, 4, 0, 2, 0, 1] : [0];
        ReadOnlySpan<int> firstColumn = interlace == PngInterlaceMode.Adam7 ? [0, 4, 0, 2, 0, 1, 0] : [0];
        ReadOnlySpan<int> rowStep = interlace == PngInterlaceMode.Adam7 ? [8, 8, 8, 4, 4, 2, 2] : [1];
        ReadOnlySpan<int> columnStep = interlace == PngInterlaceMode.Adam7 ? [8, 8, 4, 4, 2, 2, 1] : [1];

        int remaining = inflatedLength;
        for (int pass = 0; pass < firstRow.Length; pass++)
        {
            int passWidth = (source.Width - firstColumn[pass] + columnStep[pass] - 1) / columnStep[pass];
            if (passWidth <= 0)
            {
                continue;
            }

            // A scanline has one filter type byte, then 4 bytes for each 8-bit RGBA pixel.
            int scanlineLength = 1 + (passWidth * 4);
            for (int y = firstRow[pass]; y < source.Height; y += rowStep[pass])
            {
                if (remaining < scanlineLength)
                {
                    return expected;
                }

                remaining -= scanlineLength;
                for (int x = firstColumn[pass]; x < source.Width; x += columnStep[pass])
                {
                    expected[x, y] = source[x, y];
                }
            }
        }

        return expected;
    }

    /// <summary>
    /// Replaces the IDAT chunks of an encoded PNG with one IDAT chunk that has a valid CRC.
    /// </summary>
    /// <param name="png">The encoded PNG. It has no chunks between its IDAT chunks.</param>
    /// <param name="transform">Gets the new compressed data from the joined compressed data of the IDAT chunks.</param>
    /// <returns>The new PNG.</returns>
    private static byte[] ReplaceImageData(byte[] png, Func<byte[], byte[]> transform)
    {
        using MemoryStream compressed = new();
        using MemoryStream result = new();
        result.Write(png, 0, 8);

        int offset = 8;
        bool dataWritten = false;
        while (offset < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            string type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT")
            {
                compressed.Write(png, offset + 8, length);
            }
            else
            {
                if (compressed.Length > 0 && !dataWritten)
                {
                    WriteChunkWithCrc(result, "IDAT", transform(compressed.ToArray()));
                    dataWritten = true;
                }

                result.Write(png, offset, length + 12);
            }

            offset += length + 12;
        }

        return result.ToArray();
    }

    /// <summary>
    /// Writes a chunk with its length, type, data, and a valid CRC.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="type">The four letter chunk type.</param>
    /// <param name="data">The chunk data.</param>
    private static void WriteChunkWithCrc(MemoryStream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);

        // The CRC covers the chunk type and the chunk data.
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        Crc32 crc = new();
        crc.Append(typeBytes);
        crc.Append(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc.GetCurrentHashAsUInt32());
        stream.Write(buffer);
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
