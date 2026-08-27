// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class ObuFrameHeaderTests
{
    private static readonly byte[] DefaultSequenceHeaderBitStream =
        [0x0a, 0x06, 0b001_1_1_000, 0b00_1000_01, 0b11_110101, 0b001_11101, 0b111_1_1_1_0_1, 0b1_0_0_1_1_1_10];

    // Bits  Syntax element                  Value
    // 1     obu_forbidden_bit               0
    // 4     obu_type                        2 (OBU_TEMPORAL_DELIMITER)
    // 1     obu_extension_flag              0
    // 1     obu_has_size_field              1
    // 1     obu_reserved_1bit               0
    // 8     obu_size                        0
    private static readonly byte[] DefaultTemporalDelimiterBitStream = [0x12, 0x00];

    [Theory]

    // [InlineData(TestImages.Heif.IrvineAvif, 0x0198, 0x6bd1)]
    [InlineData(TestImages.Heif.XnConvert, 0x010e, 0x03cc)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010e, 0x001d)]
    public void ReadFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1BitStreamReader reader = new(span);
        IAv1TileReader decoder = new Av1TileDecoderStub();
        ObuReader obuReader = new();

        // Act
        obuReader.ReadAll(ref reader, blockSize, () => decoder);

        // Assert
        Assert.NotNull(obuReader.SequenceHeader);
        Assert.NotNull(obuReader.FrameHeader);
        Assert.NotNull(obuReader.FrameHeader.TilesInfo);
        Assert.Equal(reader.Length * 8, reader.BitPosition);
        Assert.Equal(reader.Length, blockSize);
    }

    [Theory]
    [InlineData(TestImages.Heif.Orange4x4, 0x010e, 0x001d)]
    [InlineData(TestImages.Heif.XnConvert, 0x010e, 0x03cc)]
    public void BinaryIdenticalRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1TileDecoderStub tileStub = new();
        Av1BitStreamReader reader = new(span);
        ObuReader obuReader = new();

        // Act 1
        obuReader.ReadAll(ref reader, blockSize, () => tileStub);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter obuWriter = new();
        obuWriter.WriteAll(Configuration.Default, encoded, obuReader.SequenceHeader, obuReader.FrameHeader, tileStub);

        // Assert
        byte[] encodedArray = encoded.ToArray();
        Assert.Equal((ReadOnlySpan<byte>)span, encodedArray);
    }

    [Theory]
    [InlineData(TestImages.Heif.Orange4x4, 0x010e, 0x001d)]
    [InlineData(TestImages.Heif.XnConvert, 0x010e, 0x03cc)]
    public void ThreeTimeRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1TileDecoderStub tileStub = new();
        Av1BitStreamReader reader = new(span);
        ObuReader obuReader1 = new();

        // Act 1
        obuReader1.ReadAll(ref reader, blockSize, () => tileStub);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter obuWriter = new();
        obuWriter.WriteAll(Configuration.Default, encoded, obuReader1.SequenceHeader, obuReader1.FrameHeader, tileStub);

        // Assign 2
        Span<byte> encodedBuffer = encoded.ToArray();
        IAv1TileReader tileDecoder2 = new Av1TileDecoderStub();
        Av1BitStreamReader reader2 = new(span);
        ObuReader obuReader2 = new();

        // Act 2
        obuReader2.ReadAll(ref reader2, encodedBuffer.Length, () => tileDecoder2);

        // Assert
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.SequenceHeader.ColorConfig), ObuPrettyPrint.PrettyPrintProperties(obuReader2.SequenceHeader.ColorConfig));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.SequenceHeader), ObuPrettyPrint.PrettyPrintProperties(obuReader2.SequenceHeader));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.FrameHeader), ObuPrettyPrint.PrettyPrintProperties(obuReader2.FrameHeader));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.FrameHeader.TilesInfo), ObuPrettyPrint.PrettyPrintProperties(obuReader2.FrameHeader.TilesInfo));
    }

    [Fact]
    public void ReadTemporalDelimiter()
    {
        // Arrange
        Av1BitStreamReader reader = new(DefaultTemporalDelimiterBitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.ReadAll(ref reader, DefaultTemporalDelimiterBitStream.Length, () => tileDecoder);

        // Assert
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
    }

    [Fact]
    public void ReadAnnexBHeaderWithoutSizeField()
    {
        // Arrange
        // Annex B's outer obu_length is one byte and covers the size-less temporal-delimiter header.
        byte[] bitStream = [0x01, 0x10];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.ReadAll(ref reader, bitStream.Length, () => tileDecoder, isAnnexB: true);

        // Assert
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
    }

    [Fact]
    public void ReadSequenceHeader()
    {
        // Arrange
        byte[] bitStream = DefaultSequenceHeaderBitStream;
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();
        ObuSequenceHeader expected = GetDefaultSequenceHeader();

        // Act
        obuReader.ReadAll(ref reader, bitStream.Length, () => tileDecoder);

        // Assert
        Assert.NotNull(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(expected), ObuPrettyPrint.PrettyPrintProperties(obuReader.SequenceHeader));
    }

    /// <summary>
    /// Verifies that the reduced sequence syntax cannot be used without declaring a still picture.
    /// </summary>
    [Fact]
    public void ReadReducedHeaderWithoutStillPictureThrowsInvalidImageContent()
    {
        const int sequenceHeaderPayloadOffset = 2;
        byte[] bitStream = [.. DefaultSequenceHeaderBitStream];

        // The first payload byte stores the three profile bits followed by still_picture and
        // reduced_still_picture_header. Clear only still_picture so the remaining syntax stays reduced.
        bitStream[sequenceHeaderPayloadOffset] &= 0b1110_1111;

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream));
    }

    /// <summary>
    /// Verifies that a declared still-picture sequence rejects frame prefixes which do not describe a shown key frame.
    /// </summary>
    /// <param name="invalidFramePrefix">The high nibble containing show-existing, frame-type, and show-frame syntax.</param>
    [Theory]
    [InlineData(0b1000_0000)] // show_existing_frame = 1
    [InlineData(0b0101_0000)] // frame_type = INTRA_ONLY_FRAME, show_frame = 1
    [InlineData(0b0000_0000)] // frame_type = KEY_FRAME, show_frame = 0
    public void ReadInvalidStillPictureFramePrefixThrowsInvalidImageContent(int invalidFramePrefix)
    {
        ObuSequenceHeader sequenceHeader = GetDefaultSequenceHeader();
        sequenceHeader.IsReducedStillPictureHeader = false;
        ObuFrameHeader frameHeader = GetKeyFrameHeader();
        Av1TileDecoderStub tileStub = new();
        byte[] emptyTile = [];
        tileStub.ReadTile(emptyTile, 0);

        using MemoryStream stream = new();
        ObuWriter writer = new();
        writer.WriteAll(Configuration.Default, stream, sequenceHeader, frameHeader, tileStub);
        byte[] bitStream = stream.ToArray();

        int sequenceObuOffset = DefaultTemporalDelimiterBitStream.Length;
        Av1BitStreamReader sequenceSizeReader = new(bitStream.AsSpan(sequenceObuOffset + 1));
        ulong sequencePayloadLength = sequenceSizeReader.ReadLittleEndianBytes128(out int sequenceSizeLength);
        int frameObuOffset = sequenceObuOffset + 1 + sequenceSizeLength + (int)sequencePayloadLength;
        Av1BitStreamReader frameSizeReader = new(bitStream.AsSpan(frameObuOffset + 1));
        _ = frameSizeReader.ReadLittleEndianBytes128(out int frameSizeLength);
        int framePayloadOffset = frameObuOffset + 1 + frameSizeLength;

        // Preserve syntax after the frame prefix so each malformed stream differs from a valid writer result only in
        // show_existing_frame or the still-picture frame-type/show-frame conformance condition under test.
        bitStream[framePayloadOffset] = (byte)((bitStream[framePayloadOffset] & 0x0F) | invalidFramePrefix);

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream));
    }

    /// <summary>
    /// Verifies that ignored OBU payloads are bounded and skipped before parsing the following sequence header.
    /// </summary>
    [Fact]
    public void ReadIgnoredObusSkipsEachDeclaredPayload()
    {
        // 0x7A identifies padding and 0x4A identifies reserved OBU type 9. Both carry explicit sizes so their payload
        // bytes must never be interpreted as another OBU header.
        byte[] bitStream =
        [
            0x7A, 0x02, 0x80, 0x00,
            0x4A, 0x01, 0x80,
            .. DefaultSequenceHeaderBitStream
        ];

        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        obuReader.ReadAll(ref reader, bitStream.Length, () => tileDecoder);

        Assert.NotNull(obuReader.SequenceHeader);
        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
    }

    /// <summary>
    /// Verifies that invalid OBU boundaries, size fields, and trailing bytes are rejected.
    /// </summary>
    /// <param name="bitStream">The malformed OBU stream.</param>
    [Theory]
    [InlineData(new byte[] { 0x7A, 0x02, 0x11 })]
    [InlineData(new byte[] { 0x7A, 0x01, 0x00 })]
    [InlineData(new byte[] { 0x12, 0x01, 0x01 })]
    [InlineData(new byte[] { 0x10 })]
    [InlineData(new byte[] { 0x7A, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 })]
    public void ReadInvalidObuBoundaryThrows(byte[] bitStream)
        => Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream));

    /// <summary>
    /// Verifies that an empty temporal delimiter may occupy a payload containing only zero padding bytes.
    /// </summary>
    [Fact]
    public void ReadTemporalDelimiterAllowsZeroPayloadPadding()
    {
        byte[] bitStream = [0x12, 0x02, 0x00, 0x00];
        ReadObuStream(bitStream);
    }

    [Fact]
    public void WriteTemporalDelimiter()
    {
        // Arrange
        using MemoryStream stream = new(2);
        ObuWriter obuWriter = new();

        // Act
        obuWriter.WriteAll(Configuration.Default, stream, null, null, null);
        byte[] actual = stream.GetBuffer();

        // Assert
        Assert.Equal(DefaultTemporalDelimiterBitStream, actual);
    }

    [Fact]
    public void WriteSequenceHeader()
    {
        // Arrange
        using MemoryStream stream = new(10);
        ObuSequenceHeader input = GetDefaultSequenceHeader();
        ObuWriter obuWriter = new();

        // Act
        obuWriter.WriteAll(Configuration.Default, stream, input, null, null);
        byte[] buffer = stream.GetBuffer();

        // Assert
        // Skip over Temporal Delimiter header.
        byte[] actual = buffer.AsSpan()[DefaultTemporalDelimiterBitStream.Length..].ToArray();
        Assert.Equal(DefaultSequenceHeaderBitStream, actual);
    }

    /// <summary>
    /// Verifies that the combined frame OBU declares exactly the payload bytes emitted by the writer.
    /// </summary>
    [Fact]
    public void WriteFrameHeader()
    {
        // Arrange
        using MemoryStream stream = new(10);
        ObuSequenceHeader sequenceInput = GetDefaultSequenceHeader();
        ObuFrameHeader frameInput = GetKeyFrameHeader();
        Av1TileDecoderStub tileStub = new();
        byte[] empty = [];
        tileStub.ReadTile(empty, 0);
        ObuWriter obuWriter = new();

        // Act
        obuWriter.WriteAll(Configuration.Default, stream, sequenceInput, frameInput, tileStub);
        byte[] bitStream = stream.ToArray();

        // Assert
        int frameOffset = DefaultTemporalDelimiterBitStream.Length + DefaultSequenceHeaderBitStream.Length;
        Span<byte> frameObu = bitStream.AsSpan(frameOffset);
        byte expectedHeader = (byte)(((byte)ObuType.Frame << 3) | 0x02);
        Assert.Equal(expectedHeader, frameObu[0]);

        Av1BitStreamReader sizeReader = new(frameObu[1..]);
        ulong declaredPayloadSize = sizeReader.ReadLittleEndianBytes128(out int encodedSizeLength);
        Assert.Equal(frameObu.Length - 1 - encodedSizeLength, (int)declaredPayloadSize);

        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        obuReader.ReadAll(ref reader, bitStream.Length, () => new Av1TileDecoderStub());

        Assert.NotNull(obuReader.SequenceHeader);
        Assert.NotNull(obuReader.FrameHeader);
        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
    }

    private static ObuSequenceHeader GetDefaultSequenceHeader()

            // Offset  Bits  Syntax element                     Value
            // 0       3     seq_profile                        1
            // 3       1     still_picture                      1
            // 4       1     reduced_still_picture_header       1
            // 5       5     seq_level_idx[ 0 ]                 0
            // 10      4     frame_width_bits_minus_1           8
            // 14      4     frame_height_bits_minus_1          7
            // 18      9     max_frame_width_minus_1            425
            // 27      8     max_frame_height_minus_1           239
            // 35      1     use_128x128_superblock             1
            // 36      1     enable_filter_intra                1
            // 37      1     enable_intra_edge_filter           1
            // 38      1     enable_superres                    0
            // 39      1     enable_cdef                        1
            // 40      1     enable_restoration                 1
            // 41      1     ColorConfig.BitDepth.HasHighBit    0
            // 42      1     ColorConfig.IsDescriptionPresent   0
            // 43      1     ColorConfig.ColorRange             1
            // 44      1     ColorConfig.HasSeparateUVDelta     1
            // 45      1     film_grain_present                 1
            // 47      2     Trailing bits                      2
            => new()
            {
                SequenceProfile = ObuSequenceProfile.High,
                IsStillPicture = true,
                IsReducedStillPictureHeader = true,
                TimingInfoPresentFlag = false,
                InitialDisplayDelayPresentFlag = false,
                FrameWidthBits = 8 + 1,
                FrameHeightBits = 7 + 1,
                MaxFrameWidth = 425 + 1,
                MaxFrameHeight = 239 + 1,
                IsFrameIdNumbersPresent = false,
                Use128x128Superblock = true,
                EnableFilterIntra = true,
                EnableIntraEdgeFilter = true,
                EnableInterIntraCompound = false,
                EnableMaskedCompound = false,
                EnableWarpedMotion = false,
                EnableDualFilter = false,
                EnableOrderHint = false,
                OperatingPoint = [new()],

                // EnableJountCompound = true,
                // EnableReferenceFrameMotionVectors = true,
                ForceScreenContentTools = 2,
                ForceIntegerMotionVector = 2,
                EnableSuperResolution = false,
                EnableCdef = true,
                EnableRestoration = true,
                ColorConfig = new()
                {
                    IsMonochrome = false,
                    ColorPrimaries = ObuColorPrimaries.Unspecified,
                    TransferCharacteristics = ObuTransferCharacteristics.Unspecified,
                    MatrixCoefficients = ObuMatrixCoefficients.Unspecified,
                    SubSamplingX = false,
                    SubSamplingY = false,
                    BitDepth = Av1BitDepth.EightBit,
                    HasSeparateUvDelta = true,
                    ColorRange = true,
                },
                AreFilmGrainingParametersPresent = true,
            };

    /// <summary>
    /// Reads one complete OBU stream for malformed-input assertions that cannot capture a ref-struct reader.
    /// </summary>
    /// <param name="bitStream">The complete encoded OBU stream.</param>
    private static void ReadObuStream(byte[] bitStream)
    {
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        obuReader.ReadAll(ref reader, bitStream.Length, () => tileDecoder);
    }

    private static ObuFrameHeader GetKeyFrameHeader()
        => new()
        {
            FrameType = ObuFrameType.KeyFrame,
            ShowFrame = true,
            ShowableFrame = false,
            DisableFrameEndUpdateCdf = false,
            FrameSize = new()
            {
                FrameWidth = 426,
                FrameHeight = 240,
                RenderWidth = 426,
                RenderHeight = 240,
                SuperResolutionUpscaledWidth = 426,
            },
            PrimaryReferenceFrame = 7,
            ModeInfoRowCount = 60,
            ModeInfoColumnCount = 108,
            RefreshFrameFlags = 0xff,
            ErrorResilientMode = true,
            ForceIntegerMotionVector = true,
            TilesInfo = new ObuTileGroupHeader()
            {
                HasUniformTileSpacing = true,
                TileColumnCount = 1,
                TileRowCount = 1,
            }
        };
}
