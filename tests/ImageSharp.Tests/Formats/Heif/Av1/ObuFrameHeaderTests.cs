// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections;
using System.Reflection;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class ObuFrameHeaderTests
{
    private static readonly byte[] DefaultSequenceHeaderBitStream =
        [0x0a, 0x0b, 0x00, 0x00, 0x00, 0x04, 0x3e, 0xa7, 0xbd, 0xf7, 0xf9, 0x80, 0x40];

    [Theory]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0102, 0x000D)]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0198, 0x6BD1)]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void ReadFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1BitStreamReader reader = new(span);
        IAv1TileDecoder decoder = new Av1TileDecoderStub();
        ObuReader obuReader = new();

        // Act
        obuReader.Read(ref reader, blockSize, decoder);

        // Assert
        Assert.NotNull(obuReader.SequenceHeader);
        Assert.NotNull(obuReader.FrameHeader);
        Assert.NotNull(obuReader.FrameHeader.TilesInfo);
        Assert.Equal(reader.Length * 8, reader.BitPosition);
        Assert.Equal(reader.Length, blockSize);
    }

    /*
    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void BinaryIdenticalRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1TileDecoderStub tileDecoder = new();
        Av1BitStreamReader reader = new(span);
        ObuReader obuReader = new();

        // Act 1
        obuReader.Read(ref reader, blockSize, tileDecoder);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter obuWriter = new();
        ObuWriter.Write(encoded, obuReader.SequenceHeader, obuReader.FrameHeader);

        // Assert
        Assert.Equal(span, encoded.ToArray());
    }
    */

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void ThreeTimeRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();
        Av1BitStreamReader reader = new(span);
        ObuReader obuReader1 = new();

        // Act 1
        obuReader1.Read(ref reader, blockSize, tileDecoder);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter.Write(encoded, obuReader1.SequenceHeader, obuReader1.FrameHeader);

        // Assign 2
        Span<byte> encodedBuffer = encoded.ToArray();
        IAv1TileDecoder tileDecoder2 = new Av1TileDecoderStub();
        Av1BitStreamReader reader2 = new(span);
        ObuReader obuReader2 = new();

        // Act 2
        obuReader2.Read(ref reader2, encodedBuffer.Length, tileDecoder2);

        // Assert
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.SequenceHeader.ColorConfig), ObuPrettyPrint.PrettyPrintProperties(obuReader2.SequenceHeader.ColorConfig));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.SequenceHeader), ObuPrettyPrint.PrettyPrintProperties(obuReader2.SequenceHeader));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.FrameHeader), ObuPrettyPrint.PrettyPrintProperties(obuReader2.FrameHeader));
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(obuReader1.FrameHeader.TilesInfo), ObuPrettyPrint.PrettyPrintProperties(obuReader2.FrameHeader.TilesInfo));
    }

    [Fact]
    public void DefaultTemporalDelimiter()
    {
        // Arrange
        byte[] bitStream = [0x12, 0x00];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.Read(ref reader, bitStream.Length, tileDecoder);

        // Assert
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
    }

    [Fact]
    public void DefaultTemporalDelimiterWithExtension()
    {
        // Bits  Syntax element                  Value
        // 1     obu_forbidden_bit               0
        // 4     obu_type                        2 (OBU_TEMPORAL_DELIMITER)
        // 1     obu_extension_flag              1
        // 1     obu_has_size_field              1
        // 1     obu_reserved_1bit               0
        // 3     temporal_id                     6
        // 2     spatial_id                      2
        // 3     extension_header_reserved_3bits 0
        // 8     obu_size                        0

        // Arrange
        byte[] bitStream = [0x16, 0xd0, 0x00];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.Read(ref reader, bitStream.Length, tileDecoder);

        // Assert
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
    }

    [Fact]
    public void DefaultHeaderWithoutSizeField()
    {
        // Arrange
        byte[] bitStream = [0x10];
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();

        // Act
        obuReader.Read(ref reader, bitStream.Length, tileDecoder);

        // Assert
        Assert.Null(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
    }

    [Fact]
    public void DefaultSequenceHeader()
    {
        // Offset  Bits  Syntax element                     Value
        // 0       3     seq_profile                        0
        // 3       1     still_picture                      0
        // 4       1     reduced_still_picture_header       0
        // 5       1     timing_info_present_flag           0
        // 6       1     initial_display_delay_present_flag 0
        // 7       5     operating_points_cnt_minus_1       0
        // 12      12    operating_point_idc[ 0 ]           0
        // 24      5     seq_level_idx[ 0 ]                 0
        // 29      4     frame_width_bits_minus_1           8
        // 33      4     frame_height_bits_minus_1          7
        // 37      9     max_frame_width_minus_1            425
        // 46      8     max_frame_height_minus_1           239
        // 54      1     frame_id_numbers_present_flag      0
        // 55      1     use_128x128_superblock             1
        // 56      1     enable_filter_intra                1
        // 57      1     enable_intra_edge_filter           1
        // 58      1     enable_interintra_compound         1
        // 59      1     enable_masked_compound             1
        // 60      1     enable_warped_motion               0
        // 61      1     enable_dual_filter                 1
        // 62      1     enable_order_hint                  1
        // 63      1     enable_jnt_comp                    1
        // 64      1     enable_ref_frame_mvs               1
        // 65      1     seq_choose_screen_content_tools    1
        // 66      1     seq_choose_integer_mv              1
        // 67      3     order_hint_bits_minus_1            6
        // 70      1     enable_superres                    0
        // 71      1     enable_cdef                        1
        // 72      1     enable_restoration                 1
        // ...

        // Arrange
        byte[] bitStream = DefaultSequenceHeaderBitStream;
        Av1BitStreamReader reader = new(bitStream);
        ObuReader obuReader = new();
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();
        ObuSequenceHeader expected = new()
        {
            SequenceProfile = 0,
            IsStillPicture = false,
            IsReducedStillPictureHeader = false,
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
            EnableInterIntraCompound = true,
            EnableMaskedCompound = true,
            EnableWarpedMotion = false,
            EnableDualFilter = true,
            EnableOrderHint = true,
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
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = 8,
            }
        };

        // Act
        obuReader.Read(ref reader, bitStream.Length, tileDecoder);

        // Assert
        Assert.NotNull(obuReader.SequenceHeader);
        Assert.Null(obuReader.FrameHeader);
        Assert.Equal(ObuPrettyPrint.PrettyPrintProperties(expected), ObuPrettyPrint.PrettyPrintProperties(obuReader.SequenceHeader));
    }
}
