// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class ObuFrameHeaderTests
{
    /// <summary>
    /// Identifies one reference sequence-header conformance condition used by the malformed-input theory.
    /// </summary>
    public enum InvalidSequenceHeaderCase
    {
        /// <summary>
        /// Encodes the otherwise valid baseline.
        /// </summary>
        None,

        /// <summary>
        /// Encodes an unassigned sequence-level index.
        /// </summary>
        UndefinedSequenceLevel,

        /// <summary>
        /// Encodes an initial display delay greater than ten frames.
        /// </summary>
        InitialDisplayDelayAboveTen,

        /// <summary>
        /// Encodes a frame identifier wider than sixteen bits.
        /// </summary>
        FrameIdentifierLengthAboveSixteen,

        /// <summary>
        /// Encodes a zero display-tick unit.
        /// </summary>
        ZeroDisplayTick,

        /// <summary>
        /// Encodes a zero time scale.
        /// </summary>
        ZeroTimeScale,

        /// <summary>
        /// Encodes the unsigned-variable-length overflow sentinel.
        /// </summary>
        OverflowingTicksPerPicture,

        /// <summary>
        /// Encodes the sRGB identity-matrix tuple with the main profile.
        /// </summary>
        MainProfileSrgbIdentity,

        /// <summary>
        /// Encodes an identity matrix with subsampled components.
        /// </summary>
        SubsampledIdentityMatrix
    }

    private static readonly byte[] DefaultSequenceHeaderBitStream =
        [0x0a, 0x06, 0b001_1_1_000, 0b00_1000_01, 0b11_110101, 0b001_11101, 0b111_1_1_1_0_1, 0b1_0_0_1_1_1_10];

    // This complete temporal-delimiter and sequence-header prefix comes from the color item in libavif's
    // draw_points_idat_progressive.avif. Its operating points select spatial layers 0+1 and layer 0 respectively.
    private static ReadOnlySpan<byte> ProgressiveSequenceHeaderBitStream =>
    [
        0x12, 0x00,
        0x0A, 0x0F, 0x20, 0x13, 0x01, 0x00, 0x80, 0x81, 0x4E, 0x0A, 0x36, 0xBE, 0x48, 0x08, 0x20, 0x34, 0x80
    ];

    // Bits  Syntax element                  Value
    // 1     obu_forbidden_bit               0
    // 4     obu_type                        2 (OBU_TEMPORAL_DELIMITER)
    // 1     obu_extension_flag              0
    // 1     obu_has_size_field              1
    // 1     obu_reserved_1bit               0
    // 8     obu_size                        0
    private static readonly byte[] DefaultTemporalDelimiterBitStream = [0x12, 0x00];

    [Theory]
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
        using ObuWriter obuWriter = new(Configuration.Default);
        obuWriter.WriteSequenceFrame(encoded, obuReader.SequenceHeader, obuReader.FrameHeader, tileStub);

        // Assert
        byte[] encodedArray = encoded.ToArray();
        Assert.Equal((ReadOnlySpan<byte>)span, encodedArray);
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
    /// Verifies reference sequence-header conformance failures through the complete bounded OBU parser.
    /// </summary>
    /// <param name="invalidCase">The single invalid syntax condition encoded into an otherwise valid sequence header.</param>
    [Fact]
    public void ReadSequenceHeaderRejectsConformanceFailure()
    {
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.UndefinedSequenceLevel);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.InitialDisplayDelayAboveTen);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.FrameIdentifierLengthAboveSixteen);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.ZeroDisplayTick);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.ZeroTimeScale);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.OverflowingTicksPerPicture);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.MainProfileSrgbIdentity);
        this.ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase.SubsampledIdentityMatrix);
    }

    private void ReadSequenceHeaderRejectsConformanceFailureCase(InvalidSequenceHeaderCase invalidCase)
    {
        byte[] bitStream = CreateNonReducedSequenceHeaderObu(invalidCase);

        Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream));
    }

    /// <summary>
    /// Verifies that an extended OBU belongs to an operating point only when both of its layer identifiers are selected.
    /// </summary>
    /// <param name="temporalId">The temporal-layer identifier carried by the test OBU.</param>
    /// <param name="spatialId">The spatial-layer identifier carried by the test OBU.</param>
    /// <param name="isIncluded">Whether operating point one selects both identifiers.</param>
    [Theory]
    [InlineData(0, 1, false)]
    public void ReadOperatingPointRequiresBothLayerBits(byte temporalId, byte spatialId, bool isIncluded)
    {
        byte extension = (byte)((temporalId << 5) | (spatialId << 3));
        byte[] bitStream = [.. ProgressiveSequenceHeaderBitStream, 0x2E, extension, 0x01, 0x00];

        // A selected metadata OBU reaches ignored-payload validation, where an all-zero payload is invalid. A filtered
        // OBU has nevertheless had its complete header, size and payload boundary consumed before syntax is skipped.
        if (isIncluded)
        {
            Assert.Throws<InvalidImageContentException>(() => ReadObuStream(bitStream, 1));
        }
        else
        {
            ReadObuStream(bitStream, 1);
        }
    }

    /// <summary>
    /// Verifies that a declared still-picture sequence rejects frame prefixes which do not describe a shown key frame.
    /// </summary>
    /// <param name="invalidFramePrefix">The high nibble containing show-existing, frame-type, and show-frame syntax.</param>
    [Theory]
    [InlineData(0b1000_0000)] // show_existing_frame = 1
    public void InvalidStillPicturePrefixThrows(int invalidFramePrefix)
    {
        ObuSequenceHeader sequenceHeader = GetDefaultSequenceHeader();
        sequenceHeader.IsReducedStillPictureHeader = false;
        ObuFrameHeader frameHeader = GetKeyFrameHeader();
        Av1TileDecoderStub tileStub = new();
        byte[] emptyTile = [];
        tileStub.ReadTile(emptyTile, 0);

        using MemoryStream stream = new();
        using ObuWriter writer = new(Configuration.Default);
        writer.WriteSequenceFrame(stream, sequenceHeader, frameHeader, tileStub);
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
    /// Verifies that content-light metadata is parsed from the OBU and retained for image metadata transfer.
    /// </summary>
    [Fact]
    public void ReadMetadataRetainsContentLightLevel()
    {
        // Metadata type 1 is followed by big-endian MaxCLL and MaxFALL values and byte-aligned trailing bits.
        byte[] bitStream = [0x2A, 0x06, 0x01, 0x03, 0xE8, 0x01, 0x90, 0x80];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        obuReader.ReadAll(ref reader, bitStream.Length, tileDecoder);

        Assert.True(obuReader.ContentLightLevel.HasValue);
        Assert.Equal((ushort)1_000, obuReader.ContentLightLevel.Value.MaximumContentLightLevel);
        Assert.Equal((ushort)400, obuReader.ContentLightLevel.Value.MaximumPictureAverageLightLevel);
        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
    }

    [Fact]
    public void WriteNonReducedSequenceHeaderPreservesTimingAndDecoderModel()
    {
        ObuSequenceHeader input = GetDefaultSequenceHeader();
        input.IsStillPicture = false;
        input.IsReducedStillPictureHeader = false;
        input.TimingInfoPresentFlag = true;
        input.TimingInfo = new ObuTimingInfo
        {
            NumUnitsInDisplayTick = 1001,
            TimeScale = 60000,
            EqualPictureInterval = true,
            NumTicksPerPicture = 2
        };

        input.DecoderModelInfoPresentFlag = true;
        input.DecoderModelInfo = new ObuDecoderModelInfo
        {
            BufferDelayLength = 12,
            NumUnitsInDecodingTick = 1000,
            BufferRemovalTimeLength = 10,
            FramePresentationTimeLength = 9
        };

        input.InitialDisplayDelayPresentFlag = true;
        ObuOperatingPoint inputOperatingPoint = input.OperatingPoint[0];
        inputOperatingPoint.SequenceLevelIndex = Av1Constants.SequenceTierMinimumLevelIndex;
        inputOperatingPoint.SequenceTier = 1;
        inputOperatingPoint.IsDecoderModelInfoPresent = true;
        inputOperatingPoint.DecoderBufferDelay = 137;
        inputOperatingPoint.EncoderBufferDelay = 211;
        inputOperatingPoint.LowDelayMode = true;
        inputOperatingPoint.IsInitialDisplayDelayPresent = true;
        inputOperatingPoint.InitialDisplayDelay = 4;

        using MemoryStream stream = new();
        ObuWriter.WriteSequenceHeader(Configuration.Default, stream, input);
        byte[] bitStream = stream.ToArray();
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        obuReader.ReadAll(ref reader, bitStream.Length, () => new Av1TileDecoderStub());

        ObuSequenceHeader output = obuReader.SequenceHeader;
        ObuTimingInfo outputTiming = output.GetTimingInfo();
        ObuDecoderModelInfo outputDecoderModel = output.GetDecoderModelInfo();
        ObuOperatingPoint outputOperatingPoint = output.OperatingPoint[0];
        Assert.False(output.IsStillPicture);
        Assert.False(output.IsReducedStillPictureHeader);
        Assert.Equal(input.TimingInfo.NumUnitsInDisplayTick, outputTiming.NumUnitsInDisplayTick);
        Assert.Equal(input.TimingInfo.TimeScale, outputTiming.TimeScale);
        Assert.Equal(input.TimingInfo.NumTicksPerPicture, outputTiming.NumTicksPerPicture);
        Assert.Equal(input.DecoderModelInfo.BufferDelayLength, outputDecoderModel.BufferDelayLength);
        Assert.Equal(input.DecoderModelInfo.NumUnitsInDecodingTick, outputDecoderModel.NumUnitsInDecodingTick);
        Assert.Equal(input.DecoderModelInfo.BufferRemovalTimeLength, outputDecoderModel.BufferRemovalTimeLength);
        Assert.Equal(input.DecoderModelInfo.FramePresentationTimeLength, outputDecoderModel.FramePresentationTimeLength);
        Assert.Equal(inputOperatingPoint.SequenceTier, outputOperatingPoint.SequenceTier);
        Assert.Equal(inputOperatingPoint.DecoderBufferDelay, outputOperatingPoint.DecoderBufferDelay);
        Assert.Equal(inputOperatingPoint.EncoderBufferDelay, outputOperatingPoint.EncoderBufferDelay);
        Assert.Equal(inputOperatingPoint.LowDelayMode, outputOperatingPoint.LowDelayMode);
        Assert.Equal(inputOperatingPoint.InitialDisplayDelay, outputOperatingPoint.InitialDisplayDelay);
    }

    /// <summary>
    /// Verifies non-uniform tile boundaries use the next stored boundary and retain the clipped final mode-info edge.
    /// </summary>
    [Fact]
    public void WriteNonUniformTileBoundariesRoundTrip()
    {
        ObuSequenceHeader sequenceHeader = GetDefaultSequenceHeader();
        ObuFrameHeader frameHeader = GetKeyFrameHeader();
        ObuTileGroupHeader tileInfo = frameHeader.TilesInfo;
        tileInfo.HasUniformTileSpacing = false;
        tileInfo.TileColumnCount = 2;
        tileInfo.TileRowCount = 1;
        tileInfo.TileSizeBytes = 1;
        tileInfo.TileColumnStartModeInfo[0] = 0;
        tileInfo.TileColumnStartModeInfo[1] = 64;
        tileInfo.TileColumnStartModeInfo[2] = frameHeader.ModeInfoColumnCount;
        tileInfo.TileRowStartModeInfo[0] = 0;
        tileInfo.TileRowStartModeInfo[1] = frameHeader.ModeInfoRowCount;

        Av1TileDecoderStub sourceTiles = new();
        sourceTiles.ReadTile([0x80], 0);
        sourceTiles.ReadTile([0x80], 1);

        using MemoryStream stream = new();
        using ObuWriter writer = new(Configuration.Default);
        writer.WriteSequenceFrame(stream, sequenceHeader, frameHeader, sourceTiles);
        byte[] bitStream = stream.ToArray();
        Assert.Equal([0x00, 0x80, 0x80], bitStream[^3..]);

        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        Av1TileDecoderStub decodedTiles = new();

        obuReader.ReadAll(ref reader, bitStream.Length, () => decodedTiles);

        ObuTileGroupHeader actual = obuReader.FrameHeader.TilesInfo;
        Assert.False(actual.HasUniformTileSpacing);
        Assert.Equal(2, actual.TileColumnCount);
        Assert.Equal(1, actual.TileRowCount);
        Assert.Equal(1, actual.TileSizeBytes);
        Assert.Equal(0, actual.TileColumnStartModeInfo[0]);
        Assert.Equal(64, actual.TileColumnStartModeInfo[1]);
        Assert.Equal(frameHeader.ModeInfoColumnCount, actual.TileColumnStartModeInfo[2]);
        Assert.Equal(0, actual.TileRowStartModeInfo[0]);
        Assert.Equal(frameHeader.ModeInfoRowCount, actual.TileRowStartModeInfo[1]);

        ReadOnlySpan<byte> expectedTileData = [0x80];

        Assert.True(decodedTiles.GetTileData(0).SequenceEqual(expectedTileData));
        Assert.True(decodedTiles.GetTileData(1).SequenceEqual(expectedTileData));
        Assert.Equal(bitStream.Length * 8, reader.BitPosition);
    }

    /// <summary>
    /// Encodes one non-reduced sequence header with a single selected conformance failure.
    /// </summary>
    /// <param name="invalidCase">The syntax condition to make invalid, or <see cref="InvalidSequenceHeaderCase.None"/>.</param>
    /// <returns>The complete explicitly sized sequence-header OBU.</returns>
    private static byte[] CreateNonReducedSequenceHeaderObu(InvalidSequenceHeaderCase invalidCase)
    {
        const int SequenceHeaderBufferLength = 32;

        bool hasTimingInfo = invalidCase is
            InvalidSequenceHeaderCase.ZeroDisplayTick or
            InvalidSequenceHeaderCase.ZeroTimeScale or
            InvalidSequenceHeaderCase.OverflowingTicksPerPicture;

        bool hasInitialDisplayDelay = invalidCase == InvalidSequenceHeaderCase.InitialDisplayDelayAboveTen;
        bool hasFrameIdentifiers = invalidCase == InvalidSequenceHeaderCase.FrameIdentifierLengthAboveSixteen;
        bool hasColorDescription = invalidCase is
            InvalidSequenceHeaderCase.MainProfileSrgbIdentity or
            InvalidSequenceHeaderCase.SubsampledIdentityMatrix;

        byte[] payloadBuffer = new byte[SequenceHeaderBufferLength];
        Av1BitStreamWriter writer = new(payloadBuffer);
        writer.WriteLiteral((uint)ObuSequenceProfile.Main, 3);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(hasTimingInfo);
        if (hasTimingInfo)
        {
            writer.WriteLiteral(invalidCase == InvalidSequenceHeaderCase.ZeroDisplayTick ? 0U : 1U, 32);
            writer.WriteLiteral(invalidCase == InvalidSequenceHeaderCase.ZeroTimeScale ? 0U : 1U, 32);

            bool overflowingTicksPerPicture = invalidCase == InvalidSequenceHeaderCase.OverflowingTicksPerPicture;
            writer.WriteBoolean(overflowingTicksPerPicture);
            if (overflowingTicksPerPicture)
            {
                // Thirty-two leading zeros are the UVLC sentinel which the reference decoder rejects as UINT32_MAX.
                writer.WriteLiteral(0U, 32);
            }

            writer.WriteBoolean(false);
        }

        writer.WriteBoolean(hasInitialDisplayDelay);
        writer.WriteLiteral(0U, 5);
        writer.WriteLiteral(0U, 12);

        uint sequenceLevel = invalidCase == InvalidSequenceHeaderCase.UndefinedSequenceLevel ? 24U : 0U;
        writer.WriteLiteral(sequenceLevel, 5);
        if (sequenceLevel > 7)
        {
            writer.WriteBoolean(false);
        }

        if (hasInitialDisplayDelay)
        {
            writer.WriteBoolean(true);
            writer.WriteLiteral(10U, 4);
        }

        writer.WriteLiteral(7U, 4);
        writer.WriteLiteral(7U, 4);
        writer.WriteLiteral(63U, 8);
        writer.WriteLiteral(63U, 8);
        writer.WriteBoolean(hasFrameIdentifiers);
        if (hasFrameIdentifiers)
        {
            writer.WriteLiteral(15U, 4);
            writer.WriteLiteral(0U, 3);
        }

        // Disable superblock and intra-edge tools.
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);

        // Disable the inter compound, warped, dual-filter, and order-hint tools.
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);

        // Select fixed disabled screen-content and integer-motion-vector behavior.
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);

        // Disable super resolution, CDEF, and restoration in the otherwise valid baseline.
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);

        // Encode an 8-bit, non-monochrome color configuration.
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteBoolean(hasColorDescription);
        if (hasColorDescription)
        {
            bool isSrgbIdentity = invalidCase == InvalidSequenceHeaderCase.MainProfileSrgbIdentity;
            writer.WriteLiteral((uint)(isSrgbIdentity ? ObuColorPrimaries.Bt709 : ObuColorPrimaries.Unspecified), 8);
            writer.WriteLiteral((uint)(isSrgbIdentity ? ObuTransferCharacteristics.Srgb : ObuTransferCharacteristics.Unspecified), 8);
            writer.WriteLiteral((uint)ObuMatrixCoefficients.Identity, 8);
        }

        if (invalidCase != InvalidSequenceHeaderCase.MainProfileSrgbIdentity)
        {
            writer.WriteBoolean(false);
            writer.WriteLiteral((uint)ObuChromoSamplePosition.Unknown, 2);
        }

        writer.WriteBoolean(false);
        writer.WriteBoolean(false);

        int trailingBitCount = 8 - (writer.BitPosition & 0x07);
        writer.WriteLiteral(1U << (trailingBitCount - 1), trailingBitCount);

        int payloadLength = (writer.BitPosition + 7) >> 3;
        writer.Flush();

        byte[] obu = new byte[payloadLength + 2];
        obu[0] = (byte)(((byte)ObuType.SequenceHeader << 3) | 0x02);
        obu[1] = (byte)payloadLength;
        payloadBuffer.AsSpan(0, payloadLength).CopyTo(obu.AsSpan(2));
        return obu;
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
    private static void ReadObuStream(byte[] bitStream, byte operatingPointIndex = 0)
    {
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new(operatingPointIndex);
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
