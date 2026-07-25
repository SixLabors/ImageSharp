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

    private static readonly byte[] KeyFrameHeaderBitStream = [0x32, 0x06, 0x10, 0x00];

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
        Assert.Equal(span, encodedArray);
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
    public void ReadHeaderWithoutSizeField()
    {
        // Arrange
        byte[] bitStream = [0x10];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileReader tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.ReadAll(ref reader, bitStream.Length, () => tileDecoder);

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
        byte[] buffer = stream.GetBuffer();

        // Assert
        // Skip over Temporal Delimiter and Sequence header.
        byte[] actual = buffer.AsSpan().Slice(DefaultTemporalDelimiterBitStream.Length + DefaultSequenceHeaderBitStream.Length, KeyFrameHeaderBitStream.Length).ToArray();
        Assert.Equal(KeyFrameHeaderBitStream, actual);
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
