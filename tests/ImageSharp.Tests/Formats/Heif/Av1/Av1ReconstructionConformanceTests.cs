// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Validates complete AV1 reconstruction against independently decoded native component planes.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ReconstructionConformanceTests
{
    /// <summary>
    /// The width and height of one CDEF unit in 4x4 luma mode-information units.
    /// </summary>
    private const int CdefUnitModeInfoSize = 16;

    /// <summary>
    /// The hardware configurations covering normal SIMD dispatch and the scalar fallback.
    /// </summary>
    private const HwIntrinsics ReconstructionConfigurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering the 256-bit, 128-bit, and scalar palette-reconstruction paths.
    /// </summary>
    private const HwIntrinsics PaletteConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering normal dispatch, narrower vector fallbacks, and scalar intra-block copy.
    /// </summary>
    private const HwIntrinsics IntraBlockCopyConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering the narrower vector widths and scalar fallback for the profile matrix.
    /// </summary>
    private const HwIntrinsics ProfileFallbackConfigurations =
        HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering the 128-bit and scalar lossless inverse-transform paths.
    /// </summary>
    private const HwIntrinsics LosslessConfigurations = HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The hardware configurations covering the 256-bit, 128-bit, and scalar loop-restoration paths.
    /// </summary>
    private const HwIntrinsics LoopRestorationConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The coverage bit representing an active Wiener restoration unit.
    /// </summary>
    private const int WienerRestorationCoverage = 1 << (int)Av1RestorationFilterType.Wiener;

    /// <summary>
    /// The coverage bit representing an active self-guided restoration unit.
    /// </summary>
    private const int SelfGuidedRestorationCoverage = 1 << (int)Av1RestorationFilterType.SgrProjection;

    /// <summary>
    /// The coverage bit representing luma palette prediction.
    /// </summary>
    private const int LumaPaletteCoverage = 1 << 0;

    /// <summary>
    /// The coverage bit representing chroma palette prediction.
    /// </summary>
    private const int ChromaPaletteCoverage = 1 << 1;

    /// <summary>
    /// The luma and chroma syntax coverage required from the independent palette fixture.
    /// </summary>
    private const int RequiredPaletteCoverage = LumaPaletteCoverage | ChromaPaletteCoverage;

    /// <summary>
    /// The bit mask containing every AV1 partition type defined for a coding block.
    /// </summary>
    private const int RequiredPartitionCoverage = (1 << ((int)Av1PartitionType.Vertical4 + 1)) - 1;

    /// <summary>
    /// The displayed width shared by the independent lossless fixtures.
    /// </summary>
    private const int LosslessFixtureWidth = 100;

    /// <summary>
    /// The displayed height shared by the independent lossless fixtures.
    /// </summary>
    private const int LosslessFixtureHeight = 60;

    /// <summary>
    /// The displayed width shared by the independent AV1 profile fixtures.
    /// </summary>
    private const int ProfileFixtureWidth = 512;

    /// <summary>
    /// The displayed height shared by the independent AV1 profile fixtures.
    /// </summary>
    private const int ProfileFixtureHeight = 256;

    /// <summary>
    /// The displayed width of the independent two-layer progressive fixture.
    /// </summary>
    private const int ProgressiveFixtureWidth = 33;

    /// <summary>
    /// The displayed height of the independent two-layer progressive fixture.
    /// </summary>
    private const int ProgressiveFixtureHeight = 11;

    /// <summary>
    /// The byte length of the fixture's base layer as declared by its a1lx property.
    /// </summary>
    private const int ProgressiveFirstLayerSize = 55;

    /// <summary>
    /// The displayed width and height of the independent scaled-reference fixture.
    /// </summary>
    private const int ScaledReferenceFixtureSize = 80;

    /// <summary>
    /// The retained base-layer width and height of the independent scaled-reference fixture.
    /// </summary>
    private const int ScaledReferenceBaseLayerSize = 40;

    /// <summary>
    /// The byte length of the scaled-reference fixture's base layer as declared by its a1lx property.
    /// </summary>
    private const int ScaledReferenceFirstLayerSize = 701;

    /// <summary>
    /// The displayed width and height of the independent compound image sequence.
    /// </summary>
    private const int AverageCompoundFixtureSize = 80;

    /// <summary>
    /// The number of presented frames in the independent compound image sequence.
    /// </summary>
    private const int AverageCompoundFixtureFrameCount = 19;

    /// <summary>
    /// The number of shown frames in the official libaom all-intra sequence.
    /// </summary>
    private const int OfficialAllIntraFixtureFrameCount = 39;

    /// <summary>
    /// The number of shown frames in the official libaom CDF-update sequence.
    /// </summary>
    private const int OfficialCdfUpdateFixtureFrameCount = 2;

    /// <summary>
    /// The number of shown frames in the official libaom temporal motion-field sequence.
    /// </summary>
    private const int OfficialMotionFieldFixtureFrameCount = 4;

    /// <summary>
    /// The displayed width of the official libaom extreme-displacement intra-block-copy sequence.
    /// </summary>
    private const int OfficialIntraBlockCopyFixtureWidth = 1920;

    /// <summary>
    /// The displayed height of the official libaom extreme-displacement intra-block-copy sequence.
    /// </summary>
    private const int OfficialIntraBlockCopyFixtureHeight = 1080;

    /// <summary>
    /// The number of shown frames in the official libaom extreme-displacement intra-block-copy sequence.
    /// </summary>
    private const int OfficialIntraBlockCopyFixtureFrameCount = 2;

    /// <summary>
    /// The displayed width of the official libaom two-spatial-layer sequence.
    /// </summary>
    private const int OfficialTwoSpatialLayerFixtureWidth = 1280;

    /// <summary>
    /// The displayed height of the official libaom two-spatial-layer sequence.
    /// </summary>
    private const int OfficialTwoSpatialLayerFixtureHeight = 720;

    /// <summary>
    /// The number of default-operating-point frames in the official libaom two-spatial-layer sequence.
    /// </summary>
    private const int OfficialTwoSpatialLayerFixtureFrameCount = 8;

    /// <summary>
    /// The displayed width of the official libaom two-temporal-layer sequence.
    /// </summary>
    private const int OfficialTwoTemporalLayerFixtureWidth = 640;

    /// <summary>
    /// The displayed height of the official libaom two-temporal-layer sequence.
    /// </summary>
    private const int OfficialTwoTemporalLayerFixtureHeight = 360;

    /// <summary>
    /// The number of default-operating-point frames in the official libaom two-temporal-layer sequence.
    /// </summary>
    private const int OfficialTwoTemporalLayerFixtureFrameCount = 8;

    /// <summary>
    /// The displayed width of the official libaom spatial-and-temporal-layer sequence.
    /// </summary>
    private const int OfficialSpatialTemporalLayerFixtureWidth = 1280;

    /// <summary>
    /// The displayed height of the official libaom spatial-and-temporal-layer sequence.
    /// </summary>
    private const int OfficialSpatialTemporalLayerFixtureHeight = 720;

    /// <summary>
    /// The number of default-operating-point frames in the official libaom spatial-and-temporal-layer sequence.
    /// </summary>
    private const int OfficialSpatialTemporalLayerFixtureFrameCount = 8;

    /// <summary>
    /// The number of frames in the official libaom active-film-grain sequence.
    /// </summary>
    private const int OfficialFilmGrainFixtureFrameCount = 10;

    /// <summary>
    /// The width of the official libaom eight-bit monochrome sequence.
    /// </summary>
    private const int OfficialMonochromeFixtureWidth = 320;

    /// <summary>
    /// The height of the official libaom eight-bit monochrome sequence.
    /// </summary>
    private const int OfficialMonochromeFixtureHeight = 180;

    /// <summary>
    /// The number of frames in the official libaom eight-bit monochrome sequence.
    /// </summary>
    private const int OfficialMonochromeFixtureFrameCount = 10;

    /// <summary>
    /// The width of the official libaom eight-bit quantizer-boundary sequences.
    /// </summary>
    private const int OfficialEightBitQuantizerFixtureWidth = 352;

    /// <summary>
    /// The height of the official libaom eight-bit quantizer-boundary sequences.
    /// </summary>
    private const int OfficialEightBitQuantizerFixtureHeight = 288;

    /// <summary>
    /// The width of the official libaom ten-bit quantizer-boundary sequences.
    /// </summary>
    private const int OfficialTenBitQuantizerFixtureWidth = 640;

    /// <summary>
    /// The height of the official libaom ten-bit quantizer-boundary sequences.
    /// </summary>
    private const int OfficialTenBitQuantizerFixtureHeight = 360;

    /// <summary>
    /// The number of frames in each official libaom quantizer-boundary sequence.
    /// </summary>
    private const int OfficialQuantizerFixtureFrameCount = 2;

    /// <summary>
    /// The minimum dimension retained from the official libaom frame-size matrix.
    /// </summary>
    private const int OfficialFrameSizeFixtureMinimumDimension = 196;

    /// <summary>
    /// The maximum dimension retained from the official libaom frame-size matrix.
    /// </summary>
    private const int OfficialFrameSizeFixtureMaximumDimension = 226;

    /// <summary>
    /// The number of frames in each official libaom frame-size sequence.
    /// </summary>
    private const int OfficialFrameSizeFixtureFrameCount = 2;

    /// <summary>
    /// The coverage bit representing tile-local adaptive CDF updates.
    /// </summary>
    private const int TileCdfUpdateCoverage = 1 << 0;

    /// <summary>
    /// The coverage bit representing publication of the selected frame-end CDF.
    /// </summary>
    private const int FrameEndCdfUpdateCoverage = 1 << 1;

    /// <summary>
    /// The coverage bit representing temporal reference-motion-vector projection.
    /// </summary>
    private const int ReferenceFrameMotionVectorCoverage = 1 << 2;

    /// <summary>
    /// The coverage bit representing displayed film-grain synthesis.
    /// </summary>
    private const int FilmGrainCoverage = 1 << 3;

    /// <summary>
    /// The bit mask containing every intra prediction mode.
    /// </summary>
    private const int RequiredIntraModeCoverage = (1 << (int)Av1PredictionMode.IntraModes) - 1;

    /// <summary>
    /// The transform types selected by the official all-intra conformance sequence.
    /// </summary>
    private const int RequiredAllIntraTransformTypeCoverage =
        (1 << (int)Av1TransformType.DctDct) |
        (1 << (int)Av1TransformType.AdstDct) |
        (1 << (int)Av1TransformType.DctAdst) |
        (1 << (int)Av1TransformType.AdstAdst) |
        (1 << (int)Av1TransformType.Identity) |
        (1 << (int)Av1TransformType.VerticalDct) |
        (1 << (int)Av1TransformType.HorizontalDct);

    /// <summary>
    /// The displayed width of the official libaom motion-vector sequence.
    /// </summary>
    private const int OfficialMotionVectorFixtureWidth = 352;

    /// <summary>
    /// The displayed height of the official libaom motion-vector sequence.
    /// </summary>
    private const int OfficialMotionVectorFixtureHeight = 288;

    /// <summary>
    /// The number of shown frames in the official libaom motion-vector sequence.
    /// </summary>
    private const int OfficialMotionVectorFixtureFrameCount = 4;

    /// <summary>
    /// The bit mask containing every single-reference and compound inter prediction mode.
    /// </summary>
    private const int RequiredInterModeCoverage =
        (1 << ((int)Av1PredictionMode.InterModeEnd - (int)Av1PredictionMode.InterModeStart)) - 1;

    /// <summary>
    /// The bit mask containing every simple, OBMC, and locally warped motion mode.
    /// </summary>
    private const int RequiredMotionModeCoverage = (1 << 3) - 1;

    /// <summary>
    /// The bit mask containing every regular, smooth, and sharp vertical/horizontal filter pair.
    /// </summary>
    private const int RequiredSwitchableFilterPairCoverage = (1 << 9) - 1;

    /// <summary>
    /// The coverage bit representing distance-weighted compound prediction.
    /// </summary>
    private const int DistanceWeightedCompoundCoverage = 1 << 0;

    /// <summary>
    /// The coverage bit representing a non-inverted wedge compound mask.
    /// </summary>
    private const int WedgeCompoundCoverage = 1 << 1;

    /// <summary>
    /// The coverage bit representing an inverted wedge compound mask.
    /// </summary>
    private const int InvertedWedgeCompoundCoverage = 1 << 2;

    /// <summary>
    /// The coverage bit representing the first difference-weighted mask orientation.
    /// </summary>
    private const int DifferenceWeightedCompoundCoverage = 1 << 3;

    /// <summary>
    /// The coverage bit representing the inverted difference-weighted mask orientation.
    /// </summary>
    private const int InvertedDifferenceWeightedCompoundCoverage = 1 << 4;

    /// <summary>
    /// The coverage bit representing smooth inter-intra prediction.
    /// </summary>
    private const int SmoothInterIntraCoverage = 1 << 5;

    /// <summary>
    /// The coverage bit representing wedge inter-intra prediction.
    /// </summary>
    private const int WedgeInterIntraCoverage = 1 << 6;

    /// <summary>
    /// The coverage bit representing overlapping motion compensation.
    /// </summary>
    private const int ObmcCoverage = 1 << 7;

    /// <summary>
    /// The coverage bit representing local warped-motion prediction.
    /// </summary>
    private const int LocalWarpCoverage = 1 << 8;

    /// <summary>
    /// The coverage bit representing non-translational global warped-motion prediction.
    /// </summary>
    private const int GlobalWarpCoverage = 1 << 9;

    /// <summary>
    /// The hardware configurations covering the available vector widths and the scalar color-conversion fallback.
    /// </summary>
    private const HwIntrinsics PresentationConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies deblocking syntax, filter activation, component traversal, and presentation for real eight-, ten-,
    /// and twelve-bit AV1 and AVIF content.
    /// </summary>
    [Fact]
    public void DecodeMatchesPinnedLibaomReference()
    {
        ValidateFixture(
            TestImages.Heif.Av1Deblocking8BitAvif,
            TestImages.Heif.Av1Deblocking8BitPayload,
            TestImages.Heif.Av1Deblocking8BitReference,
            768,
            512,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420,
            HeifBitDepth.Bit8);

        ValidateFixture(
            TestImages.Heif.Av1Deblocking10BitAvif,
            TestImages.Heif.Av1Deblocking10BitPayload,
            TestImages.Heif.Av1Deblocking10BitReference,
            1024,
            428,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv444,
            HeifBitDepth.Bit10);

        ValidateNativeFixture(
            TestImages.Heif.Av1Deblocking12BitPayload,
            TestImages.Heif.Av1Deblocking12BitReference,
            1024,
            428,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444,
            requireActiveCdef: false);

        ValidatePresentedImage(TestImages.Heif.Av1Deblocking12BitAvif, 64, 64, HeifBitDepth.Bit12);
    }

    /// <summary>
    /// Verifies active CDEF syntax, strength selection, unit traversal, subsampling, frame edges, and final native
    /// samples against scalar libaom for independently encoded eight-, ten-, and twelve-bit still-picture streams
    /// under normal SIMD dispatch and with hardware intrinsics disabled.
    /// </summary>
    [Fact]
    public void DecodeWithActiveCdefMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateActiveCdefFixtures, ReconstructionConfigurations);

    /// <summary>
    /// Verifies exact presented pixels and public metadata for independently encoded eight-, ten-, and twelve-bit
    /// active-CDEF AVIF images across the available vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    /// <param name="width">The expected presented width.</param>
    /// <param name="height">The expected presented height.</param>
    /// <param name="bitDepth">The expected public sample precision.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Cdef8BitAvif, PixelTypes.Rgba32, 768, 512, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Cdef10BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Cdef12BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit12)]
    public void DecodeWithActiveCdefMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider,
        int width,
        int height,
        HeifBitDepth bitDepth)
    {
        AssertPresentedMetadata(provider, width, height, bitDepth);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);
    }

    /// <summary>
    /// Verifies exact native reconstruction for every valid AV1 profile, bit-depth, and chroma-format combination
    /// supported by AVIF across every available vector width and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeProfileMatrixMatchesPinnedLibaomReference()
        => ValidateProfileNativeFixtures();

    /// <summary>
    /// Verifies exact native reconstruction for every valid AV1 profile, bit-depth, and chroma-format combination
    /// under each narrower vector width and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeProfileMatrixFallbacksMatchPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateProfileNativeFixtures, ProfileFallbackConfigurations);

    /// <summary>
    /// Verifies exact presented pixels, public bit-depth metadata, and CICP signaling for every valid AV1 profile,
    /// bit-depth, and chroma-format combination supported by AVIF.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    /// <param name="bitDepth">The expected public sample precision.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Profile8BitMonochromeAvif, PixelTypes.Rgba32, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Profile8Bit420Avif, PixelTypes.Rgba32, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Profile8Bit422Avif, PixelTypes.Rgba32, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Profile8Bit444Avif, PixelTypes.Rgba32, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Profile10BitMonochromeAvif, PixelTypes.Rgba32, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Profile10Bit420Avif, PixelTypes.Rgba32, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Profile10Bit422Avif, PixelTypes.Rgba32, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Profile10Bit444Avif, PixelTypes.Rgba32, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Profile12BitMonochromeAvif, PixelTypes.Rgba32, HeifBitDepth.Bit12)]
    [WithFile(TestImages.Heif.Av1Profile12Bit420Avif, PixelTypes.Rgba32, HeifBitDepth.Bit12)]
    [WithFile(TestImages.Heif.Av1Profile12Bit422Avif, PixelTypes.Rgba32, HeifBitDepth.Bit12)]
    [WithFile(TestImages.Heif.Av1Profile12Bit444Avif, PixelTypes.Rgba32, HeifBitDepth.Bit12)]
    public void DecodeProfileMatrixMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider,
        HeifBitDepth bitDepth)
    {
        using Image<Rgba32> image = provider.GetImage();
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(bitDepth, metadata.BitDepth);

        CicpProfile colorProfile = Assert.IsType<CicpProfile>(image.Metadata.CicpProfile);
        Assert.Equal(CicpColorPrimaries.ItuRBt709_6, colorProfile.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.Iec61966_2_1, colorProfile.TransferCharacteristics);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, colorProfile.MatrixCoefficients);
        Assert.True(colorProfile.FullRange);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);
    }

    /// <summary>
    /// Verifies decoded luma and chroma palette syntax and exact native samples against scalar libaom for an
    /// independently encoded AV1 still-picture stream.
    /// </summary>
    [Fact]
    public void DecodeWithPaletteMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePaletteNativeFixture, PaletteConfigurations);

    /// <summary>
    /// Verifies that a real palette frame whose tile entropy payload ends early is rejected instead of being decoded
    /// from the range decoder's implicit zero padding.
    /// </summary>
    [Fact]
    public void DecodeFrameBufferRejectsTruncatedPaletteTileEntropy()
    {
        const int TruncatedTileByteCount = 8;
        byte[] validPayload = TestFile.Create(TestImages.Heif.Av1Palette8BitPayload).Bytes;
        int obuOffset = 0;
        int finalObuOffset = 0;
        int finalSizeFieldOffset = 0;
        int finalSizeFieldLength = 0;
        ulong finalPayloadLength = 0;
        while (obuOffset < validPayload.Length)
        {
            byte obuHeader = validPayload[obuOffset];
            Assert.NotEqual(0, obuHeader & 0x02);

            int headerLength = 1 + ((obuHeader >> 2) & 1);
            int sizeFieldOffset = obuOffset + headerLength;
            Av1BitStreamReader sizeReader = new(validPayload.AsSpan(sizeFieldOffset));
            ulong payloadLength = sizeReader.ReadLittleEndianBytes128(out int sizeFieldLength);
            int nextObuOffset = checked(sizeFieldOffset + sizeFieldLength + (int)payloadLength);
            if (nextObuOffset == validPayload.Length)
            {
                finalObuOffset = obuOffset;
                finalSizeFieldOffset = sizeFieldOffset;
                finalSizeFieldLength = sizeFieldLength;
                finalPayloadLength = payloadLength;
            }

            obuOffset = nextObuOffset;
        }

        Assert.Equal(ObuType.Frame, (ObuType)((validPayload[finalObuOffset] >> 3) & 0x0F));
        Assert.Equal(1, finalSizeFieldLength);
        Assert.InRange(finalPayloadLength, (ulong)(TruncatedTileByteCount + 1), 0x7FUL);

        byte[] truncatedPayload = validPayload[..^TruncatedTileByteCount];
        truncatedPayload[finalSizeFieldOffset] = (byte)(finalPayloadLength - TruncatedTileByteCount);

        using Av1Decoder decoder = new(Configuration.Default);

        Assert.Throws<InvalidImageContentException>(
            () => decoder.DecodeFrameBuffer(truncatedPayload, null, null, out _).Dispose());

        Assert.Null(decoder.SequenceHeader);
        Assert.Null(decoder.FrameHeader);
        Assert.Null(decoder.FrameInfo);
    }

    /// <summary>
    /// Verifies decoded luma and chroma palette syntax and exact presented pixels for an independently encoded AVIF
    /// image across the available vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Palette8BitAvif, PixelTypes.Rgba32)]
    public void DecodeWithPaletteMatchesPinnedLibavifPresentation(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);

    /// <summary>
    /// Verifies that malformed data following a decoded palette tile releases its frame state before the same decoder
    /// processes another payload.
    /// </summary>
    [Fact]
    public void DecodeFrameBufferRecoversAfterMalformedFollowingObu()
    {
        byte[] validPayload = TestFile.Create(TestImages.Heif.Av1Palette8BitPayload).Bytes;

        // The palette fixture ends with one combined-frame OBU containing one tile, so the intact prefix creates and
        // completes a real Av1TileReader. The appended padding OBU declares one zero byte; AV1 padding requires a
        // trailing-one bit, making this later bounded-payload failure deterministic without corrupting tile entropy.
        byte[] malformedPayload =
        [
            .. validPayload,
            0x7A, // Padding OBU with an explicit payload-size field.
            0x01, // LEB128 payload length of one byte.
            0x00, // Invalid padding payload with no trailing-one bit.
        ];

        using Av1Decoder decoder = new(Configuration.Default);

        Assert.Throws<InvalidImageContentException>(
            () => decoder.DecodeFrameBuffer(malformedPayload, null, null, out _).Dispose());

        Assert.Null(decoder.SequenceHeader);
        Assert.Null(decoder.FrameHeader);
        Assert.Null(decoder.FrameInfo);

        using Av1FrameBuffer<byte> recoveredFrameBuffer = decoder.DecodeFrameBuffer(validPayload, null, null, out _);

        Assert.Equal(33, recoveredFrameBuffer.Width);
        Assert.Equal(11, recoveredFrameBuffer.Height);
        Assert.Equal(RequiredPaletteCoverage, GetPaletteCoverage(decoder));
    }

    /// <summary>
    /// Verifies selected intra-block-copy prediction and exact native samples against scalar libaom for an
    /// independently encoded AV1 still pictures across every available vector width and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeWithIntraBlockCopyMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateIntraBlockCopyNativeFixtures, IntraBlockCopyConfigurations);

    /// <summary>
    /// Verifies exact presented pixels for independently encoded intra-block-copy AVIF images across the available
    /// vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1IntraBlockCopy8BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1IntraBlockCopy10BitAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1IntraBlockCopy12BitAvif, PixelTypes.Rgba32)]
    public void DecodeWithIntraBlockCopyMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);

    /// <summary>
    /// Verifies the production single-reference inter-reconstruction path against exact native and presentation
    /// references across the available vector widths and scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Progressive8BitAvif, PixelTypes.Rgba32)]
    public void DecodeProgressiveSingleReferenceMatchesPinnedReferences(
        TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateProgressiveSingleReferenceFixtureWithDefaultConfiguration,
            PresentationConfigurations,
            provider);

    /// <summary>
    /// Verifies production single-reference inter reconstruction with a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeProgressiveSingleReferenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateProgressiveSingleReferenceFixture(configuration);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies that an essential lsel property returns the selected base spatial layer rather than the final
    /// progressive layer, with exact pinned-libaom native planes and pinned-libavif presentation.
    /// </summary>
    /// <param name="provider">The selected-layer AVIF input and matching reference-output naming context.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1ScaledReferenceSelectedLayerAvif, PixelTypes.Rgba32)]
    public void DecodeSelectedProgressiveSpatialLayerMatchesPinnedReferences(
        TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSelectedProgressiveSpatialLayerWithDefaultConfiguration,
            PresentationConfigurations,
            provider);

    /// <summary>
    /// Verifies selected-layer native reconstruction and public presentation with constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeSelectedProgressiveSpatialLayerWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateSelectedProgressiveSpatialLayer(configuration);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies an independently encoded 40x40 retained layer scaled into an 80x80 dependent layer against exact
    /// pinned-libaom native planes and pinned-libavif presentation.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1ScaledReferenceAvif, PixelTypes.Rgba32)]
    public void DecodeScaledReferenceMatchesPinnedReferences(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateScaledReferenceFixtureWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies scaled-reference reconstruction with constrained tracked allocation and contiguous frame planes.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeScaledReferenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateScaledReferenceFixture(configuration);

        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies that the production dependent-frame result owns its motion-field storage until decoder disposal.
    /// </summary>
    [Fact]
    public void DecodeProgressiveSingleReferenceTracksMotionFieldResultOwnership()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        byte[] payload = TestFile.Create(TestImages.Heif.Av1Progressive8BitPayload).Bytes;

        using Av1Decoder decoder = new(configuration);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(
            payload,
            null,
            null,
            out _,
            new Av1LayeredImageIndex(ProgressiveFirstLayerSize, 0, 0));

        TestMemoryAllocator.AllocationRequest retainedMotionField = Assert.Single(
            allocator.AllocationLog,
            request => request.ElementType.Name == "RetainedMotionFieldEntry");

        TestMemoryAllocator.AllocationRequest temporalMotionField = Assert.Single(
            allocator.AllocationLog,
            request => request.ElementType.Name == "TemporalMotionFieldEntry");

        // Reference-slot and presentation owners are released while DecodeFrameBuffer transfers the native planes.
        // The decoder's inspectable FrameInfo result remains the final motion-field owner until decoder disposal.
        Assert.DoesNotContain(
            allocator.ReturnLog,
            returned => returned.AllocationId == retainedMotionField.AllocationId);

        Assert.DoesNotContain(
            allocator.ReturnLog,
            returned => returned.AllocationId == temporalMotionField.AllocationId);

        frameBuffer.Dispose();

        Assert.DoesNotContain(
            allocator.ReturnLog,
            returned => returned.AllocationId == retainedMotionField.AllocationId);

        Assert.DoesNotContain(
            allocator.ReturnLog,
            returned => returned.AllocationId == temporalMotionField.AllocationId);

        decoder.Dispose();
        decoder.Dispose();

        Assert.Single(
            allocator.ReturnLog,
            returned => returned.AllocationId == retainedMotionField.AllocationId);

        Assert.Single(allocator.ReturnLog, returned => returned.AllocationId == temporalMotionField.AllocationId);
    }

    /// <summary>
    /// Verifies exact native reconstruction and presentation for a genuine pinned-libavif image sequence that uses
    /// equal-weight compound prediction.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.Av1AverageCompoundSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeRealLibavifSequenceWithEqualAverageCompoundMatchesPinnedReferences(
        TestImageProvider<Rgba32> provider)

        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateAverageCompoundSequenceWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies the complete compound sequence through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifSequenceWithEqualAverageCompoundUsesContiguousPlanes()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateAverageCompoundSequence(configuration);

        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Runs the exact compound-sequence comparisons with the default configuration.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateAverageCompoundSequenceWithDefaultConfiguration(string providerDump)
    {
        ValidateAverageCompoundSequence(Configuration.Default);
        ValidateFinalSequencePresentation(providerDump);
    }

    /// <summary>
    /// Validates the complete compound sequence with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    private static void ValidateAverageCompoundSequence(Configuration configuration)
    {
        byte[] fileBytes = TestFile.Create(TestImages.Heif.Av1AverageCompoundSequenceAvif).Bytes;
        byte[] referenceBytes = TestFile.Create(TestImages.Heif.Av1AverageCompoundSequenceNativeReference).Bytes;
        ReadOnlySpan<byte> fileHeader =
            "YUV4MPEG2 W80 H80 F25:1 Ip A0:0 C444 XYSCSS=444 XCOLORRANGE=LIMITED\n"u8;

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;

        ReadOnlySpan<byte> nativeReference = referenceBytes;
        Assert.True(nativeReference.StartsWith(fileHeader));
        nativeReference = nativeReference[fileHeader.Length..];
        Assert.True(nativeReference.StartsWith(frameHeader));
        nativeReference = nativeReference[frameHeader.Length..];
        Assert.Equal(AverageCompoundFixtureSize * AverageCompoundFixtureSize * 3, nativeReference.Length);

        HeifSequence sequence = ParseImageSequence(fileBytes);
        HeifSequenceTrack track = sequence.ColorTrack;
        int compoundBlockCount = 0;
        int visibleFrameCount = 0;
        bool nativeCompared = false;

        using Av1Decoder decoder = new(configuration);
        for (int sampleIndex = 0; sampleIndex < track.Samples.Length; sampleIndex++)
        {
            HeifSequenceSample sample = track.Samples[sampleIndex];
            Span<byte> sampleData = fileBytes.AsSpan((int)sample.Offset, sample.Length);
            if (sample.IsHidden)
            {
                decoder.DecodeSequenceReference(
                    sampleData,
                    track.CicpProfile,
                    track.Av1CodecConfiguration);

                continue;
            }

            ImageFrame<Rgba32> decodedFrame;
            try
            {
                decodedFrame = decoder.DecodeSequenceFrame<Rgba32>(
                    sampleData,
                    track.CicpProfile,
                    track.Av1CodecConfiguration);
            }
            catch (InvalidImageContentException exception)
            {
                throw new InvalidImageContentException($"The pinned compound fixture failed at sample {sampleIndex}.", exception);
            }

            using ImageFrame<Rgba32> frame = decodedFrame;

            ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
            _ = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);

            // Inter prediction addresses padding with one base span and a logical row stride. The frame owner must
            // preserve that contract even when the configured allocator would ordinarily split a large buffer.
            Assert.Equal(1, frameBuffer.BufferY!.FastMemoryGroup.Count);
            Assert.Equal(1, frameBuffer.BufferCb!.FastMemoryGroup.Count);
            Assert.Equal(1, frameBuffer.BufferCr!.FastMemoryGroup.Count);

            int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
            int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
            int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;

            for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
            {
                for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
                {
                    Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                    foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
                    {
                        if (modeInfo.ReferenceFrames[1] <= Av1ReferenceFrameType.Intra)
                        {
                            continue;
                        }

                        Assert.Equal(Av1CompoundType.Average, modeInfo.CompoundType);
                        compoundBlockCount++;
                    }
                }
            }

            if (visibleFrameCount == AverageCompoundFixtureFrameCount - 1)
            {
                Assert.Equal(AverageCompoundFixtureSize, frameBuffer.Width);
                Assert.Equal(AverageCompoundFixtureSize, frameBuffer.Height);
                Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
                Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
                AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
                nativeCompared = true;
            }

            visibleFrameCount++;
        }

        Assert.Equal(AverageCompoundFixtureFrameCount, visibleFrameCount);
        Assert.NotEqual(0, compoundBlockCount);
        Assert.True(nativeCompared);
    }

    /// <summary>
    /// Verifies every selectable compound and inter-intra production branch against pinned native and presentation references.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.Av1DistanceWeightedCompoundSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1WedgeCompoundSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1DifferenceWeightedCompoundSequenceAvif, PixelTypes.Rgba32)]
    [WithFile(TestImages.Heif.Av1InterIntraSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeRealLibavifSequencesWithSelectableCompoundAndInterIntraMatchesPinnedReferences(
        TestImageProvider<Rgba32> provider)

        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSelectableCompoundSequenceWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies selectable compound and inter-intra reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifSequencesWithSelectableCompoundAndInterIntraUseContiguousPlanes()
    {
        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1DistanceWeightedCompoundSequenceAvif,
            TestImages.Heif.Av1DistanceWeightedCompoundSequenceNativeReference,
            DistanceWeightedCompoundCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1WedgeCompoundSequenceAvif,
            TestImages.Heif.Av1WedgeCompoundSequenceNativeReference,
            WedgeCompoundCoverage | InvertedWedgeCompoundCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceAvif,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceNativeReference,
            DifferenceWeightedCompoundCoverage | InvertedDifferenceWeightedCompoundCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1InterIntraSequenceAvif,
            TestImages.Heif.Av1InterIntraSequenceNativeReference,
            SmoothInterIntraCoverage | WedgeInterIntraCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);
    }

    /// <summary>
    /// Verifies production OBMC reconstruction against pinned native and presentation references.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.Av1ObmcSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeRealLibavifObmcSequenceMatchesPinnedReferences(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateObmcSequenceWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies production OBMC reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifObmcSequenceUsesContiguousPlanes()
        => ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1ObmcSequenceAvif,
            TestImages.Heif.Av1ObmcSequenceNativeReference,
            ObmcCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

    /// <summary>
    /// Verifies production local warped-motion reconstruction against pinned native and presentation references.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.Av1LocalWarpSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeRealLibavifLocalWarpSequenceMatchesPinnedReferences(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLocalWarpSequenceWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies production local warped-motion reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifLocalWarpSequenceUsesContiguousPlanes()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateInterPredictionSequence(
            configuration,
            TestImages.Heif.Av1LocalWarpSequenceAvif,
            TestImages.Heif.Av1LocalWarpSequenceNativeReference,
            LocalWarpCoverage,
            256,
            2);

        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies production non-translational global-motion reconstruction against pinned native and presentation references.
    /// </summary>
    [Theory]
    [WithFile(TestImages.Heif.Av1GlobalWarpSequenceAvif, PixelTypes.Rgba32)]
    public void DecodeRealLibavifGlobalWarpSequenceMatchesPinnedReferences(TestImageProvider<Rgba32> provider)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateGlobalWarpSequenceWithDefaultConfiguration,
            ReconstructionConfigurations,
            provider);

    /// <summary>
    /// Verifies production non-translational global-motion reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifGlobalWarpSequenceUsesContiguousPlanes()
        => ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1GlobalWarpSequenceAvif,
            TestImages.Heif.Av1GlobalWarpSequenceNativeReference,
            GlobalWarpCoverage,
            256,
            2);

    /// <summary>
    /// Verifies every intra prediction mode and the fixture's seven transform types against the official
    /// pinned-libaom all-intra conformance sequence and its exact native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialAllIntraSequenceMatchesPinnedLibaomReference() => ValidateOfficialAllIntraFixture();

    /// <summary>
    /// Decodes every all-intra IVF sample in one session, compares each frame exactly, and records the syntax
    /// selections that make the fixture authoritative for prediction and transform coverage.
    /// </summary>
    private static void ValidateOfficialAllIntraFixture()
    {
        byte[] ivf = TestFile.Create(TestImages.Heif.Av1OfficialAllIntraSequence).Bytes;
        byte[] nativeReference = TestFile.Create(TestImages.Heif.Av1OfficialAllIntraSequenceNativeReference).Bytes;
        ReadOnlySpan<byte> y4mFileHeader = "YUV4MPEG2 W352 H288 F3:1 Ip C420jpeg\n"u8;
        ReadOnlySpan<byte> y4mFrameHeader = "FRAME\n"u8;

        Assert.True(ivf.AsSpan(0, 4).SequenceEqual("DKIF"u8));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(4, 2)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(6, 2)));
        Assert.True(ivf.AsSpan(8, 4).SequenceEqual("AV01"u8));
        Assert.Equal(OfficialMotionVectorFixtureWidth, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(12, 2)));
        Assert.Equal(OfficialMotionVectorFixtureHeight, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(14, 2)));
        Assert.Equal(
            OfficialAllIntraFixtureFrameCount,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(24, 4))));

        Assert.True(nativeReference.AsSpan().StartsWith(y4mFileHeader));

        int ivfOffset = 32;
        int nativeOffset = y4mFileHeader.Length;
        int nativeFrameLength =
            (OfficialMotionVectorFixtureWidth * OfficialMotionVectorFixtureHeight) +
            (2 * (OfficialMotionVectorFixtureWidth >> 1) * (OfficialMotionVectorFixtureHeight >> 1));

        int intraModeCoverage = 0;
        int transformTypeCoverage = 0;
        using Av1Decoder decoder = new(Configuration.Default);
        for (int frameIndex = 0; frameIndex < OfficialAllIntraFixtureFrameCount; frameIndex++)
        {
            int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(ivfOffset, 4)));
            ivfOffset += 12;
            using ImageFrame<Rgba32> frame = decoder.DecodeSequenceFrame<Rgba32>(
                ivf.AsSpan(ivfOffset, payloadLength),
                null,
                null);

            ivfOffset += payloadLength;
            Assert.Equal(OfficialMotionVectorFixtureWidth, frame.Width);
            Assert.Equal(OfficialMotionVectorFixtureHeight, frame.Height);
            Assert.True(nativeReference.AsSpan(nativeOffset).StartsWith(y4mFrameHeader));
            nativeOffset += y4mFrameHeader.Length;

            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(OfficialMotionVectorFixtureWidth, frameBuffer.Width);
            Assert.Equal(OfficialMotionVectorFixtureHeight, frameBuffer.Height);
            Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
            Assert.Equal(Av1ColorFormat.Yuv420, frameBuffer.ColorFormat);
            AssertNativePlanesEqual(
                decoder,
                frameBuffer,
                nativeReference.AsSpan(nativeOffset, nativeFrameLength));

            nativeOffset += nativeFrameLength;

            ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
            Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
            int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, sequenceHeader.SuperblockSizeLog2)
                >> sequenceHeader.SuperblockSizeLog2;
            int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, sequenceHeader.SuperblockSizeLog2)
                >> sequenceHeader.SuperblockSizeLog2;

            for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
            {
                for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
                {
                    Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                    foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
                    {
                        if (modeInfo.YMode is >= Av1PredictionMode.IntraModeStart and < Av1PredictionMode.IntraModeEnd)
                        {
                            intraModeCoverage |= 1 << ((int)modeInfo.YMode - (int)Av1PredictionMode.IntraModeStart);
                        }

                        int firstTransformLocation = modeInfo.GetFirstTransformLocation(Av1Plane.Y);
                        int transformUnitCount = modeInfo.GetTransformUnitCount(Av1Plane.Y);
                        foreach (Av1TransformInfo transformInfo in
                            superblockInfo.GetTransformInfoY().Slice(firstTransformLocation, transformUnitCount))
                        {
                            transformTypeCoverage |= 1 << (int)transformInfo.Type;
                        }
                    }
                }
            }
        }

        Assert.Equal(ivf.Length, ivfOffset);
        Assert.Equal(nativeReference.Length, nativeOffset);
        Assert.Equal(RequiredIntraModeCoverage, intraModeCoverage);
        Assert.Equal(RequiredAllIntraTransformTypeCoverage, transformTypeCoverage);
    }

    /// <summary>
    /// Verifies adaptive tile and frame-end CDF updates against the official pinned-libaom sequence and exact native
    /// output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialCdfUpdateSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialCdfUpdateFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies temporal reference-motion-vector projection against the official pinned-libaom sequence and exact
    /// native output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialMotionFieldSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialMotionFieldFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies extreme intra-block-copy displacement vectors against the official pinned-libaom sequence and exact
    /// native output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialIntraBlockCopySequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialIntraBlockCopyFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes the official intra-block-copy sequence and proves that the active copied blocks reconstruct exactly.
    /// </summary>
    private static void ValidateOfficialIntraBlockCopyFixture()
    {
        byte[] ivf = TestFile.Create(TestImages.Heif.Av1OfficialIntraBlockCopySequence).Bytes;
        byte[] nativeReference = TestFile.Create(TestImages.Heif.Av1OfficialIntraBlockCopySequenceNativeReference).Bytes;
        ReadOnlySpan<byte> y4mFileHeader = "YUV4MPEG2 W1920 H1080 F30:1 Ip C420jpeg\n"u8;
        ReadOnlySpan<byte> y4mFrameHeader = "FRAME\n"u8;

        Assert.True(ivf.AsSpan(0, 4).SequenceEqual("DKIF"u8));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(4, 2)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(6, 2)));
        Assert.True(ivf.AsSpan(8, 4).SequenceEqual("AV01"u8));
        Assert.Equal(OfficialIntraBlockCopyFixtureWidth, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(12, 2)));
        Assert.Equal(OfficialIntraBlockCopyFixtureHeight, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(14, 2)));
        Assert.Equal(
            OfficialIntraBlockCopyFixtureFrameCount,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(24, 4))));

        Assert.True(nativeReference.AsSpan().StartsWith(y4mFileHeader));

        int ivfOffset = 32;
        int nativeOffset = y4mFileHeader.Length;
        int nativeFrameLength =
            (OfficialIntraBlockCopyFixtureWidth * OfficialIntraBlockCopyFixtureHeight) +
            (2 * (OfficialIntraBlockCopyFixtureWidth >> 1) * (OfficialIntraBlockCopyFixtureHeight >> 1));

        int intraBlockCopyBlockCount = 0;
        bool allowIntraBlockCopy = false;
        using Av1Decoder decoder = new(Configuration.Default);
        for (int frameIndex = 0; frameIndex < OfficialIntraBlockCopyFixtureFrameCount; frameIndex++)
        {
            int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(ivfOffset, 4)));
            ivfOffset += 12;
            using ImageFrame<Rgba32> frame = decoder.DecodeSequenceFrame<Rgba32>(
                ivf.AsSpan(ivfOffset, payloadLength),
                null,
                null);

            ivfOffset += payloadLength;
            Assert.Equal(OfficialIntraBlockCopyFixtureWidth, frame.Width);
            Assert.Equal(OfficialIntraBlockCopyFixtureHeight, frame.Height);
            Assert.True(nativeReference.AsSpan(nativeOffset).StartsWith(y4mFrameHeader));
            nativeOffset += y4mFrameHeader.Length;

            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(OfficialIntraBlockCopyFixtureWidth, frameBuffer.Width);
            Assert.Equal(OfficialIntraBlockCopyFixtureHeight, frameBuffer.Height);
            Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
            Assert.Equal(Av1ColorFormat.Yuv420, frameBuffer.ColorFormat);
            AssertNativePlanesEqual(
                decoder,
                frameBuffer,
                nativeReference.AsSpan(nativeOffset, nativeFrameLength));

            nativeOffset += nativeFrameLength;
            ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
            allowIntraBlockCopy |= frameHeader.AllowIntraBlockCopy;
            intraBlockCopyBlockCount += GetIntraBlockCopyBlockCount(decoder);
        }

        Assert.Equal(ivf.Length, ivfOffset);
        Assert.Equal(nativeReference.Length, nativeOffset);
        Assert.True(allowIntraBlockCopy);
        Assert.NotEqual(0, intraBlockCopyBlockCount);
    }

    /// <summary>
    /// Verifies exact temporal motion-field reconstruction and balanced ownership with a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialMotionFieldSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        int coverage = ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMotionFieldSequence,
            TestImages.Heif.Av1OfficialMotionFieldSequenceNativeReference,
            OfficialMotionFieldFixtureFrameCount);

        Assert.NotEqual(0, coverage & ReferenceFrameMotionVectorCoverage);
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Validates that the official CDF-update fixture selects both adaptive update boundaries.
    /// </summary>
    private static void ValidateOfficialCdfUpdateFixture()
    {
        int coverage = ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialCdfUpdateSequence,
            TestImages.Heif.Av1OfficialCdfUpdateSequenceNativeReference,
            OfficialCdfUpdateFixtureFrameCount);

        Assert.Equal(TileCdfUpdateCoverage | FrameEndCdfUpdateCoverage, coverage & 3);
    }

    /// <summary>
    /// Validates that the official temporal motion-field fixture enables projected reference motion vectors.
    /// </summary>
    private static void ValidateOfficialMotionFieldFixture()
    {
        int coverage = ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialMotionFieldSequence,
            TestImages.Heif.Av1OfficialMotionFieldSequenceNativeReference,
            OfficialMotionFieldFixtureFrameCount);

        Assert.NotEqual(0, coverage & ReferenceFrameMotionVectorCoverage);
    }

    /// <summary>
    /// Verifies the default operating point of an official two-spatial-layer sequence against exact pinned-libaom
    /// native output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialTwoSpatialLayerSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialTwoSpatialLayerFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official two-spatial-layer sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialTwoSpatialLayerSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 8_192 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTwoSpatialLayerSequence,
            TestImages.Heif.Av1OfficialTwoSpatialLayerSequenceNativeReference,
            OfficialTwoSpatialLayerFixtureFrameCount,
            OfficialTwoSpatialLayerFixtureWidth,
            OfficialTwoSpatialLayerFixtureHeight);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes the default operating point of the official two-spatial-layer sequence.
    /// </summary>
    private static void ValidateOfficialTwoSpatialLayerFixture()
        => ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialTwoSpatialLayerSequence,
            TestImages.Heif.Av1OfficialTwoSpatialLayerSequenceNativeReference,
            OfficialTwoSpatialLayerFixtureFrameCount,
            OfficialTwoSpatialLayerFixtureWidth,
            OfficialTwoSpatialLayerFixtureHeight);

    /// <summary>
    /// Verifies the default operating point of an official two-temporal-layer sequence against exact pinned-libaom
    /// native output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialTwoTemporalLayerSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialTwoTemporalLayerFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official two-temporal-layer sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialTwoTemporalLayerSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 8_192 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTwoTemporalLayerSequence,
            TestImages.Heif.Av1OfficialTwoTemporalLayerSequenceNativeReference,
            OfficialTwoTemporalLayerFixtureFrameCount,
            OfficialTwoTemporalLayerFixtureWidth,
            OfficialTwoTemporalLayerFixtureHeight);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes the default operating point of the official two-temporal-layer sequence.
    /// </summary>
    private static void ValidateOfficialTwoTemporalLayerFixture()
        => ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialTwoTemporalLayerSequence,
            TestImages.Heif.Av1OfficialTwoTemporalLayerSequenceNativeReference,
            OfficialTwoTemporalLayerFixtureFrameCount,
            OfficialTwoTemporalLayerFixtureWidth,
            OfficialTwoTemporalLayerFixtureHeight);

    /// <summary>
    /// Verifies the default operating point of an official spatial-and-temporal-layer sequence against exact
    /// pinned-libaom native output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialSpatialTemporalLayerSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialSpatialTemporalLayerFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official spatial-and-temporal-layer sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialSpatialTemporalLayerSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 8_192 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialSpatialTemporalLayerSequence,
            TestImages.Heif.Av1OfficialSpatialTemporalLayerSequenceNativeReference,
            OfficialSpatialTemporalLayerFixtureFrameCount,
            OfficialSpatialTemporalLayerFixtureWidth,
            OfficialSpatialTemporalLayerFixtureHeight);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes the default operating point of the official spatial-and-temporal-layer sequence.
    /// </summary>
    private static void ValidateOfficialSpatialTemporalLayerFixture()
        => ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialSpatialTemporalLayerSequence,
            TestImages.Heif.Av1OfficialSpatialTemporalLayerSequenceNativeReference,
            OfficialSpatialTemporalLayerFixtureFrameCount,
            OfficialSpatialTemporalLayerFixtureWidth,
            OfficialSpatialTemporalLayerFixtureHeight);

    /// <summary>
    /// Verifies active film-grain presentation and dependent-frame reconstruction against exact pinned-libaom native
    /// output under normal and scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialFilmGrainSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialFilmGrainFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official film-grain sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialFilmGrainSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        int coverage = ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialFilmGrainSequence,
            TestImages.Heif.Av1OfficialFilmGrainSequenceNativeReference,
            OfficialFilmGrainFixtureFrameCount);

        Assert.NotEqual(0, coverage & FilmGrainCoverage);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes the official film-grain sequence and verifies that synthesis is active.
    /// </summary>
    private static void ValidateOfficialFilmGrainFixture()
    {
        int coverage = ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialFilmGrainSequence,
            TestImages.Heif.Av1OfficialFilmGrainSequenceNativeReference,
            OfficialFilmGrainFixtureFrameCount);

        Assert.NotEqual(0, coverage & FilmGrainCoverage);
    }

    /// <summary>
    /// Verifies the official ten-bit film-grain sequence against exact pinned-libaom native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialTenBitFilmGrainSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialTenBitFilmGrainFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes the official ten-bit film-grain sequence and verifies that synthesis is active.
    /// </summary>
    private static void ValidateOfficialTenBitFilmGrainFixture()
    {
        int coverage = ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialTenBitFilmGrainSequence,
            TestImages.Heif.Av1OfficialTenBitFilmGrainSequenceNativeReference,
            OfficialFilmGrainFixtureFrameCount,
            OfficialMotionVectorFixtureWidth,
            OfficialMotionVectorFixtureHeight,
            Av1ColorFormat.Yuv420,
            Av1BitDepth.TenBit);

        Assert.NotEqual(0, coverage & FilmGrainCoverage);
    }

    /// <summary>
    /// Verifies the official eight-bit monochrome sequence against exact pinned-libaom native output under normal and
    /// scalar dispatch.
    /// </summary>
    [Fact]
    public void DecodeOfficialMonochromeSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialMonochromeFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official eight-bit monochrome sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialMonochromeSequenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMonochromeSequence,
            TestImages.Heif.Av1OfficialMonochromeSequenceNativeReference,
            OfficialMonochromeFixtureFrameCount,
            OfficialMonochromeFixtureWidth,
            OfficialMonochromeFixtureHeight,
            Av1ColorFormat.Yuv400);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes the official eight-bit monochrome sequence.
    /// </summary>
    private static void ValidateOfficialMonochromeFixture()
        => ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialMonochromeSequence,
            TestImages.Heif.Av1OfficialMonochromeSequenceNativeReference,
            OfficialMonochromeFixtureFrameCount,
            OfficialMonochromeFixtureWidth,
            OfficialMonochromeFixtureHeight,
            Av1ColorFormat.Yuv400);

    /// <summary>
    /// Verifies the official ten-bit monochrome sequence against exact pinned-libaom native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialTenBitMonochromeSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialTenBitMonochromeFixture,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes the official ten-bit monochrome sequence.
    /// </summary>
    private static void ValidateOfficialTenBitMonochromeFixture()
        => ValidateOfficialCompactSequence(
            Configuration.Default,
            TestImages.Heif.Av1OfficialTenBitMonochromeSequence,
            TestImages.Heif.Av1OfficialTenBitMonochromeSequenceNativeReference,
            OfficialMonochromeFixtureFrameCount,
            OfficialMonochromeFixtureWidth,
            OfficialMonochromeFixtureHeight,
            Av1ColorFormat.Yuv400,
            Av1BitDepth.TenBit);

    /// <summary>
    /// Verifies both official ten-bit sequences through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialTenBitSequencesWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        int filmGrainCoverage = ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTenBitFilmGrainSequence,
            TestImages.Heif.Av1OfficialTenBitFilmGrainSequenceNativeReference,
            OfficialFilmGrainFixtureFrameCount,
            OfficialMotionVectorFixtureWidth,
            OfficialMotionVectorFixtureHeight,
            Av1ColorFormat.Yuv420,
            Av1BitDepth.TenBit);

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTenBitMonochromeSequence,
            TestImages.Heif.Av1OfficialTenBitMonochromeSequenceNativeReference,
            OfficialMonochromeFixtureFrameCount,
            OfficialMonochromeFixtureWidth,
            OfficialMonochromeFixtureHeight,
            Av1ColorFormat.Yuv400,
            Av1BitDepth.TenBit);

        Assert.NotEqual(0, filmGrainCoverage & FilmGrainCoverage);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies the official eight-bit quantizer boundaries against exact pinned-libaom native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialEightBitQuantizerBoundarySequencesMatchPinnedLibaomReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialEightBitQuantizerBoundaryFixtures,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes the official eight-bit quantizer boundaries.
    /// </summary>
    private static void ValidateOfficialEightBitQuantizerBoundaryFixtures()
        => ValidateOfficialEightBitQuantizerBoundaryFixturesWithConfiguration(Configuration.Default);

    /// <summary>
    /// Verifies the official ten-bit quantizer boundaries against exact pinned-libaom native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialTenBitQuantizerBoundarySequencesMatchPinnedLibaomReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialTenBitQuantizerBoundaryFixtures,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes the official ten-bit quantizer boundaries.
    /// </summary>
    private static void ValidateOfficialTenBitQuantizerBoundaryFixtures()
        => ValidateOfficialTenBitQuantizerBoundaryFixturesWithConfiguration(Configuration.Default);

    /// <summary>
    /// Verifies the official quantizer boundaries through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialQuantizerBoundarySequencesWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_560 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialEightBitQuantizerBoundaryFixturesWithConfiguration(configuration);
        ValidateOfficialTenBitQuantizerBoundaryFixturesWithConfiguration(configuration);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes both retained official eight-bit quantizer-boundary fixtures.
    /// </summary>
    private static void ValidateOfficialEightBitQuantizerBoundaryFixturesWithConfiguration(Configuration configuration)
    {
        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialEightBitMinimumQuantizerSequence,
            TestImages.Heif.Av1OfficialEightBitMinimumQuantizerSequenceNativeReference,
            OfficialQuantizerFixtureFrameCount,
            OfficialEightBitQuantizerFixtureWidth,
            OfficialEightBitQuantizerFixtureHeight);

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialEightBitMaximumQuantizerSequence,
            TestImages.Heif.Av1OfficialEightBitMaximumQuantizerSequenceNativeReference,
            OfficialQuantizerFixtureFrameCount,
            OfficialEightBitQuantizerFixtureWidth,
            OfficialEightBitQuantizerFixtureHeight);
    }

    /// <summary>
    /// Decodes both retained official ten-bit quantizer-boundary fixtures.
    /// </summary>
    private static void ValidateOfficialTenBitQuantizerBoundaryFixturesWithConfiguration(Configuration configuration)
    {
        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTenBitMinimumQuantizerSequence,
            TestImages.Heif.Av1OfficialTenBitMinimumQuantizerSequenceNativeReference,
            OfficialQuantizerFixtureFrameCount,
            OfficialTenBitQuantizerFixtureWidth,
            OfficialTenBitQuantizerFixtureHeight,
            Av1ColorFormat.Yuv420,
            Av1BitDepth.TenBit,
            "60:1");

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialTenBitMaximumQuantizerSequence,
            TestImages.Heif.Av1OfficialTenBitMaximumQuantizerSequenceNativeReference,
            OfficialQuantizerFixtureFrameCount,
            OfficialTenBitQuantizerFixtureWidth,
            OfficialTenBitQuantizerFixtureHeight,
            Av1ColorFormat.Yuv420,
            Av1BitDepth.TenBit,
            "60:1");
    }

    /// <summary>
    /// Verifies all four corners of the official frame-size matrix against exact pinned-libaom native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialFrameSizeCornerSequencesMatchPinnedLibaomReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialFrameSizeCornerFixtures,
            ReconstructionConfigurations);

    /// <summary>
    /// Decodes all four retained frame-size corners.
    /// </summary>
    private static void ValidateOfficialFrameSizeCornerFixtures()
        => ValidateOfficialFrameSizeCornerFixturesWithConfiguration(Configuration.Default);

    /// <summary>
    /// Verifies all four frame-size corners through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialFrameSizeCornerSequencesWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialFrameSizeCornerFixturesWithConfiguration(configuration);

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Decodes every retained official frame-size fixture and compares every native sample.
    /// </summary>
    private static void ValidateOfficialFrameSizeCornerFixturesWithConfiguration(Configuration configuration)
    {
        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMinimumFrameSizeSequence,
            TestImages.Heif.Av1OfficialMinimumFrameSizeSequenceNativeReference,
            OfficialFrameSizeFixtureFrameCount,
            OfficialFrameSizeFixtureMinimumDimension,
            OfficialFrameSizeFixtureMinimumDimension);

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMinimumWidthMaximumHeightSequence,
            TestImages.Heif.Av1OfficialMinimumWidthMaximumHeightSequenceNativeReference,
            OfficialFrameSizeFixtureFrameCount,
            OfficialFrameSizeFixtureMinimumDimension,
            OfficialFrameSizeFixtureMaximumDimension);

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMaximumWidthMinimumHeightSequence,
            TestImages.Heif.Av1OfficialMaximumWidthMinimumHeightSequenceNativeReference,
            OfficialFrameSizeFixtureFrameCount,
            OfficialFrameSizeFixtureMaximumDimension,
            OfficialFrameSizeFixtureMinimumDimension);

        ValidateOfficialCompactSequence(
            configuration,
            TestImages.Heif.Av1OfficialMaximumFrameSizeSequence,
            TestImages.Heif.Av1OfficialMaximumFrameSizeSequenceNativeReference,
            OfficialFrameSizeFixtureFrameCount,
            OfficialFrameSizeFixtureMaximumDimension,
            OfficialFrameSizeFixtureMaximumDimension);
    }

    /// <summary>
    /// Decodes one compact official IVF sequence, compares every native sample, and returns its active frame-state
    /// coverage mask.
    /// </summary>
    private static int ValidateOfficialCompactSequence(
        Configuration configuration,
        string fixturePath,
        string nativeReferencePath,
        int expectedFrameCount,
        int expectedWidth = OfficialMotionVectorFixtureWidth,
        int expectedHeight = OfficialMotionVectorFixtureHeight,
        Av1ColorFormat expectedColorFormat = Av1ColorFormat.Yuv420,
        Av1BitDepth expectedBitDepth = Av1BitDepth.EightBit,
        string expectedFrameRate = "30:1")
    {
        byte[] ivf = TestFile.Create(fixturePath).Bytes;
        byte[] nativeReference = TestFile.Create(nativeReferencePath).Bytes;
        bool hasY4mHeaders = nativeReference.AsSpan().StartsWith("YUV4MPEG2 "u8);
        int bitDepth = expectedBitDepth switch
        {
            Av1BitDepth.EightBit => 8,
            Av1BitDepth.TenBit => 10,
            Av1BitDepth.TwelveBit => 12,
            _ => throw new InvalidOperationException("The compact official sequence oracle requires a valid AV1 bit depth.")
        };

        string y4mColorSpace = expectedColorFormat switch
        {
            Av1ColorFormat.Yuv400 => expectedBitDepth == Av1BitDepth.EightBit
                ? "Cmono"
                : $"Cmono{bitDepth}",
            Av1ColorFormat.Yuv420 => expectedBitDepth == Av1BitDepth.EightBit
                ? "C420jpeg"
                : $"C420p{bitDepth} XYSCSS=420P{bitDepth}",
            _ => throw new InvalidOperationException("The compact official sequence oracle supports YUV400 and YUV420 references.")
        };

        ReadOnlySpan<byte> y4mFileHeader = hasY4mHeaders
            ? Encoding.ASCII.GetBytes(
                $"YUV4MPEG2 W{expectedWidth} H{expectedHeight} F{expectedFrameRate} Ip {y4mColorSpace}\n")
            : [];

        ReadOnlySpan<byte> y4mFrameHeader = "FRAME\n"u8;

        Assert.True(ivf.AsSpan(0, 4).SequenceEqual("DKIF"u8));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(4, 2)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(6, 2)));
        Assert.True(ivf.AsSpan(8, 4).SequenceEqual("AV01"u8));
        Assert.Equal(expectedWidth, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(12, 2)));
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(14, 2)));
        Assert.Equal(
            expectedFrameCount,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(24, 4))));

        Assert.True(nativeReference.AsSpan().StartsWith(y4mFileHeader));

        int ivfOffset = 32;
        int nativeOffset = y4mFileHeader.Length;
        int nativeSampleCount = expectedColorFormat == Av1ColorFormat.Yuv400
            ? expectedWidth * expectedHeight
            : (expectedWidth * expectedHeight) +
                (2 * GetSubsampledSize(expectedWidth, 1) * GetSubsampledSize(expectedHeight, 1));

        int nativeFrameLength = nativeSampleCount * (expectedBitDepth == Av1BitDepth.EightBit ? 1 : sizeof(ushort));

        int coverage = 0;
        using Av1Decoder decoder = new(configuration);
        for (int frameIndex = 0; frameIndex < expectedFrameCount; frameIndex++)
        {
            int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(ivfOffset, 4)));
            ivfOffset += 12;
            using ImageFrame<Rgba32> frame = decoder.DecodeSequenceFrame<Rgba32>(
                ivf.AsSpan(ivfOffset, payloadLength),
                null,
                null);

            ivfOffset += payloadLength;
            Assert.Equal(expectedWidth, frame.Width);
            Assert.Equal(expectedHeight, frame.Height);
            if (hasY4mHeaders)
            {
                Assert.True(nativeReference.AsSpan(nativeOffset).StartsWith(y4mFrameHeader));
                nativeOffset += y4mFrameHeader.Length;
            }

            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(expectedBitDepth, frameBuffer.BitDepth);
            Assert.Equal(expectedColorFormat, frameBuffer.ColorFormat);
            AssertNativePlanesEqual(
                decoder,
                frameBuffer,
                nativeReference.AsSpan(nativeOffset, nativeFrameLength),
                frameIndex);

            nativeOffset += nativeFrameLength;

            ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
            coverage |= frameHeader.DisableCdfUpdate ? 0 : TileCdfUpdateCoverage;
            coverage |= frameHeader.DisableFrameEndUpdateCdf ? 0 : FrameEndCdfUpdateCoverage;
            coverage |= frameHeader.UseReferenceFrameMotionVectors ? ReferenceFrameMotionVectorCoverage : 0;
            coverage |= frameHeader.FilmGrainParameters.ApplyGrain ? FilmGrainCoverage : 0;
        }

        Assert.Equal(ivf.Length, ivfOffset);
        Assert.Equal(nativeReference.Length, nativeOffset);
        return coverage;
    }

    /// <summary>
    /// Verifies every ordinary inter mode, motion mode, and switchable dual-filter pair against the official
    /// pinned-libaom motion-vector conformance sequence and its exact native output.
    /// </summary>
    [Fact]
    public void DecodeOfficialMotionVectorSequenceMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateOfficialMotionVectorFixtureWithDefaultConfiguration,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies the official motion-vector conformance sequence through constrained tracked allocation.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeOfficialMotionVectorSequenceUsesContiguousPlanes()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 2_048 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateOfficialMotionVectorFixture(configuration);

        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Runs the official motion-vector fixture with the default decoder configuration.
    /// </summary>
    private static void ValidateOfficialMotionVectorFixtureWithDefaultConfiguration()
        => ValidateOfficialMotionVectorFixture(Configuration.Default);

    /// <summary>
    /// Decodes every IVF sample in one retained session, compares each shown frame exactly, and records the
    /// syntax selections that make the vector authoritative for ordinary inter-mode and filter coverage.
    /// </summary>
    private static void ValidateOfficialMotionVectorFixture(Configuration configuration)
    {
        byte[] ivf = TestFile.Create(TestImages.Heif.Av1OfficialMotionVectorSequence).Bytes;
        byte[] nativeReference = TestFile.Create(TestImages.Heif.Av1OfficialMotionVectorSequenceNativeReference).Bytes;
        ReadOnlySpan<byte> y4mFileHeader = "YUV4MPEG2 W352 H288 F30:1 Ip C420jpeg\n"u8;
        ReadOnlySpan<byte> y4mFrameHeader = "FRAME\n"u8;

        Assert.True(ivf.AsSpan(0, 4).SequenceEqual("DKIF"u8));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(4, 2)));
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(6, 2)));
        Assert.True(ivf.AsSpan(8, 4).SequenceEqual("AV01"u8));
        Assert.Equal(OfficialMotionVectorFixtureWidth, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(12, 2)));
        Assert.Equal(OfficialMotionVectorFixtureHeight, BinaryPrimitives.ReadUInt16LittleEndian(ivf.AsSpan(14, 2)));
        Assert.Equal(
            OfficialMotionVectorFixtureFrameCount,
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(24, 4))));

        Assert.True(nativeReference.AsSpan().StartsWith(y4mFileHeader));

        int ivfOffset = 32;
        int nativeOffset = y4mFileHeader.Length;
        int nativeFrameLength =
            (OfficialMotionVectorFixtureWidth * OfficialMotionVectorFixtureHeight) +
            (2 * (OfficialMotionVectorFixtureWidth >> 1) * (OfficialMotionVectorFixtureHeight >> 1));

        int interModeCoverage = 0;
        int motionModeCoverage = 0;
        int switchableFilterPairCoverage = 0;
        using Av1Decoder decoder = new(configuration);
        for (int frameIndex = 0; frameIndex < OfficialMotionVectorFixtureFrameCount; frameIndex++)
        {
            int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ivf.AsSpan(ivfOffset, 4)));
            ivfOffset += 12;
            ImageFrame<Rgba32> decodedFrame = decoder.DecodeSequenceFrame<Rgba32>(
                ivf.AsSpan(ivfOffset, payloadLength),
                null,
                null);

            using ImageFrame<Rgba32> frame = decodedFrame;

            ivfOffset += payloadLength;
            Assert.Equal(OfficialMotionVectorFixtureWidth, frame.Width);
            Assert.Equal(OfficialMotionVectorFixtureHeight, frame.Height);
            Assert.True(nativeReference.AsSpan(nativeOffset).StartsWith(y4mFrameHeader));
            nativeOffset += y4mFrameHeader.Length;

            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            Assert.Equal(OfficialMotionVectorFixtureWidth, frameBuffer.Width);
            Assert.Equal(OfficialMotionVectorFixtureHeight, frameBuffer.Height);
            Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
            Assert.Equal(Av1ColorFormat.Yuv420, frameBuffer.ColorFormat);
            AssertNativePlanesEqual(
                decoder,
                frameBuffer,
                nativeReference.AsSpan(nativeOffset, nativeFrameLength));

            nativeOffset += nativeFrameLength;

            ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
            Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
            int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, sequenceHeader.SuperblockSizeLog2)
                >> sequenceHeader.SuperblockSizeLog2;
            int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, sequenceHeader.SuperblockSizeLog2)
                >> sequenceHeader.SuperblockSizeLog2;

            for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
            {
                for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
                {
                    Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                    foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
                    {
                        if (modeInfo.YMode is < Av1PredictionMode.InterModeStart or >= Av1PredictionMode.InterModeEnd)
                        {
                            continue;
                        }

                        interModeCoverage |= 1 << ((int)modeInfo.YMode - (int)Av1PredictionMode.InterModeStart);
                        motionModeCoverage |= 1 << (int)modeInfo.MotionMode;
                        int verticalFilter = (int)modeInfo.InterpolationFilters[0];
                        int horizontalFilter = (int)modeInfo.InterpolationFilters[1];
                        switchableFilterPairCoverage |= 1 << ((verticalFilter * 3) + horizontalFilter);
                    }
                }
            }
        }

        Assert.Equal(ivf.Length, ivfOffset);
        Assert.Equal(nativeReference.Length, nativeOffset);
        Assert.Equal(RequiredInterModeCoverage, interModeCoverage);
        Assert.Equal(RequiredMotionModeCoverage, motionModeCoverage);
        Assert.Equal(RequiredSwitchableFilterPairCoverage, switchableFilterPairCoverage);
    }

    /// <summary>
    /// Verifies one complete inter-prediction sequence with a separately tracked constrained allocator.
    /// </summary>
    private static void ValidateInterPredictionSequenceWithConstrainedAllocator(
        string imagePath,
        string nativeReferencePath,
        int requiredCoverage,
        int fixtureSize,
        int visibleFrameCount)
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateInterPredictionSequence(
            configuration,
            imagePath,
            nativeReferencePath,
            requiredCoverage,
            fixtureSize,
            visibleFrameCount);

        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Runs one selectable compound fixture with exact native and final presentation comparisons.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateSelectableCompoundSequenceWithDefaultConfiguration(string providerDump)
    {
        TestImageProvider<Rgba32> provider =
            FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);

        (string NativeReferencePath, int RequiredCoverage) expected =
            provider.SourceFileOrDescription switch
            {
                TestImages.Heif.Av1DistanceWeightedCompoundSequenceAvif =>
                    (TestImages.Heif.Av1DistanceWeightedCompoundSequenceNativeReference, DistanceWeightedCompoundCoverage),
                TestImages.Heif.Av1WedgeCompoundSequenceAvif =>
                    (TestImages.Heif.Av1WedgeCompoundSequenceNativeReference, WedgeCompoundCoverage | InvertedWedgeCompoundCoverage),
                TestImages.Heif.Av1DifferenceWeightedCompoundSequenceAvif =>
                    (TestImages.Heif.Av1DifferenceWeightedCompoundSequenceNativeReference, DifferenceWeightedCompoundCoverage | InvertedDifferenceWeightedCompoundCoverage),
                TestImages.Heif.Av1InterIntraSequenceAvif =>
                    (TestImages.Heif.Av1InterIntraSequenceNativeReference, SmoothInterIntraCoverage | WedgeInterIntraCoverage),
                _ => throw new InvalidOperationException($"Unexpected selectable-compound fixture: {provider.SourceFileOrDescription}.")
            };

        ValidateInterPredictionSequence(
            Configuration.Default,
            provider.SourceFileOrDescription,
            expected.NativeReferencePath,
            expected.RequiredCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

        ValidateFinalSequencePresentation(providerDump);
    }

    /// <summary>
    /// Runs the OBMC sequence with exact final presentation comparison.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateObmcSequenceWithDefaultConfiguration(string providerDump)
    {
        ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1ObmcSequenceAvif,
            TestImages.Heif.Av1ObmcSequenceNativeReference,
            ObmcCoverage,
            AverageCompoundFixtureSize,
            AverageCompoundFixtureFrameCount);

        ValidateFinalSequencePresentation(providerDump);
    }

    /// <summary>
    /// Runs the local warped-motion sequence with exact final presentation comparison.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateLocalWarpSequenceWithDefaultConfiguration(string providerDump)
    {
        ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1LocalWarpSequenceAvif,
            TestImages.Heif.Av1LocalWarpSequenceNativeReference,
            LocalWarpCoverage,
            256,
            2);

        ValidateFinalSequencePresentation(providerDump);
    }

    /// <summary>
    /// Runs the non-translational global-motion sequence with exact final presentation comparison.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateGlobalWarpSequenceWithDefaultConfiguration(string providerDump)
    {
        ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1GlobalWarpSequenceAvif,
            TestImages.Heif.Av1GlobalWarpSequenceNativeReference,
            GlobalWarpCoverage,
            256,
            2);

        ValidateFinalSequencePresentation(providerDump);
    }

    /// <summary>
    /// Decodes one complete retained-reference sequence and compares its final native samples exactly.
    /// </summary>
    private static void ValidateInterPredictionSequence(
        Configuration configuration,
        string imagePath,
        string nativeReferencePath,
        int requiredCoverage,
        int fixtureSize,
        int visibleFrameCount)
    {
        byte[] fileBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(nativeReferencePath).Bytes;
        string fileHeaderText =
            $"YUV4MPEG2 W{fixtureSize} H{fixtureSize} F25:1 Ip A0:0 C444 XYSCSS=444 XCOLORRANGE=LIMITED\n";

        ReadOnlySpan<byte> fileHeader = Encoding.ASCII.GetBytes(fileHeaderText);

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;

        ReadOnlySpan<byte> nativeReference = referenceBytes;
        Assert.True(nativeReference.StartsWith(fileHeader));
        nativeReference = nativeReference[fileHeader.Length..];
        Assert.True(nativeReference.StartsWith(frameHeader));
        nativeReference = nativeReference[frameHeader.Length..];
        Assert.Equal(fixtureSize * fixtureSize * 3, nativeReference.Length);

        HeifSequence sequence = ParseImageSequence(fileBytes);
        HeifSequenceTrack track = sequence.ColorTrack;
        int coverage = 0;
        int decodedVisibleFrameCount = 0;
        bool nativeCompared = false;

        using Av1Decoder decoder = new(configuration);
        for (int sampleIndex = 0; sampleIndex < track.Samples.Length; sampleIndex++)
        {
            HeifSequenceSample sample = track.Samples[sampleIndex];
            Span<byte> sampleData = fileBytes.AsSpan((int)sample.Offset, sample.Length);
            if (sample.IsHidden)
            {
                decoder.DecodeSequenceReference(
                    sampleData,
                    track.CicpProfile,
                    track.Av1CodecConfiguration);

                coverage |= GetInterPredictionCoverage(decoder);
                continue;
            }

            using ImageFrame<Rgba32> frame = decoder.DecodeSequenceFrame<Rgba32>(
                sampleData,
                track.CicpProfile,
                track.Av1CodecConfiguration);

            Av1FrameBuffer<byte> frameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(decoder.FrameBuffer);
            coverage |= GetInterPredictionCoverage(decoder);

            // Every inter-prediction branch retains the same row-addressed plane contract under constrained allocators.
            Assert.Equal(1, frameBuffer.BufferY!.FastMemoryGroup.Count);
            Assert.Equal(1, frameBuffer.BufferCb!.FastMemoryGroup.Count);
            Assert.Equal(1, frameBuffer.BufferCr!.FastMemoryGroup.Count);

            if (decodedVisibleFrameCount == visibleFrameCount - 1)
            {
                AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
                nativeCompared = true;
            }

            decodedVisibleFrameCount++;
        }

        Assert.Equal(visibleFrameCount, decodedVisibleFrameCount);
        Assert.Equal(requiredCoverage, coverage & requiredCoverage);
        Assert.True(nativeCompared);
    }

    /// <summary>
    /// Collects the compound, inter-intra, OBMC, and warped modes retained in one decoded frame.
    /// </summary>
    private static int GetInterPredictionCoverage(Av1Decoder decoder)
    {
        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        int coverage = 0;
        for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
            {
                Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
                {
                    if (modeInfo.MotionMode == Av1MotionMode.Obmc)
                    {
                        coverage |= ObmcCoverage;
                    }

                    if (modeInfo.MotionMode == Av1MotionMode.Warped)
                    {
                        coverage |= LocalWarpCoverage;
                    }

                    if (modeInfo.YMode is Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector &&
                        Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) >= 8)
                    {
                        int referenceCount = modeInfo.ReferenceFrames[1] > Av1ReferenceFrameType.Intra ? 2 : 1;
                        for (int referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++)
                        {
                            int canonicalReferenceIndex =
                                (int)modeInfo.ReferenceFrames[referenceIndex] - (int)Av1ReferenceFrameType.Last;

                            Av1GlobalMotionParameters globalMotionParameters =
                                frameHeader.GetGlobalMotionParameters()[canonicalReferenceIndex];

                            if (globalMotionParameters.Type > Av1GlobalMotionType.Translation &&
                                !globalMotionParameters.IsInvalid)
                            {
                                coverage |= GlobalWarpCoverage;
                            }
                        }
                    }

                    if (modeInfo.ReferenceFrames[1] == Av1ReferenceFrameType.Intra)
                    {
                        coverage |= modeInfo.UseInterIntraWedge ? WedgeInterIntraCoverage : SmoothInterIntraCoverage;
                        continue;
                    }

                    if (modeInfo.ReferenceFrames[1] <= Av1ReferenceFrameType.Intra)
                    {
                        continue;
                    }

                    coverage |= modeInfo.CompoundType switch
                    {
                        Av1CompoundType.DistanceWeighted => DistanceWeightedCompoundCoverage,
                        Av1CompoundType.Wedge => modeInfo.CompoundWedgeSign
                            ? InvertedWedgeCompoundCoverage
                            : WedgeCompoundCoverage,
                        Av1CompoundType.DifferenceWeighted => modeInfo.DifferenceWeightedMaskType == Av1DifferenceWeightedMaskType.Type38Inverse
                            ? InvertedDifferenceWeightedCompoundCoverage
                            : DifferenceWeightedCompoundCoverage,
                        _ => 0,
                    };
                }
            }
        }

        return coverage;
    }

    /// <summary>
    /// Verifies lossless syntax, residual reconstruction, and exact native samples against scalar libaom for
    /// independently encoded eight-, ten-, and twelve-bit AVIF images.
    /// </summary>
    [Fact]
    public void DecodeLosslessMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateLosslessFixtures, LosslessConfigurations);

    /// <summary>
    /// Verifies exact presented pixels for independently encoded lossless eight-, ten-, and twelve-bit AVIF images
    /// across the available vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    /// <param name="bitDepth">The expected public sample precision.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Lossless8BitAvif, PixelTypes.Rgba32, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Lossless10BitAvif, PixelTypes.Rgba32, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Lossless12BitAvif, PixelTypes.Rgba32, HeifBitDepth.Bit12)]
    public void DecodeLosslessMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider,
        HeifBitDepth bitDepth)
    {
        AssertPresentedMetadata(provider, LosslessFixtureWidth, LosslessFixtureHeight, bitDepth);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);
    }

    /// <summary>
    /// Verifies active normative super-resolution, chroma-width rounding, replicated edges, and exact native samples
    /// against scalar libaom for independently encoded eight-, ten-, and twelve-bit still-picture streams.
    /// </summary>
    [Fact]
    public void DecodeWithSuperResolutionMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSuperResolutionFixtures, ReconstructionConfigurations);

    /// <summary>
    /// Verifies exact presented pixels and public metadata for independently packaged eight-, ten-, and twelve-bit
    /// active-super-resolution AVIF images across the available vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    /// <param name="width">The expected presented width.</param>
    /// <param name="height">The expected presented height.</param>
    /// <param name="bitDepth">The expected public sample precision.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1SuperResolution8BitAvif, PixelTypes.Rgba32, 768, 512, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1SuperResolution10BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1SuperResolution12BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit12)]
    public void DecodeWithSuperResolutionMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider,
        int width,
        int height,
        HeifBitDepth bitDepth)
    {
        AssertPresentedMetadata(provider, width, height, bitDepth);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);
    }

    /// <summary>
    /// Verifies active normative loop restoration and exact native samples against scalar libaom for independently
    /// encoded eight-, ten-, and twelve-bit still-picture streams.
    /// </summary>
    [Fact]
    public void DecodeWithLoopRestorationMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateLoopRestorationFixtures, LoopRestorationConfigurations);

    /// <summary>
    /// Verifies combined super-resolution and loop-restoration geometry for independently encoded 8-bit 4:2:0 content.
    /// </summary>
    [Fact]
    public void DecodeWithLoopRestorationAndSuperResolutionMatchesPinnedLibaomReference8Bit420()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLoopRestorationAndSuperResolution8Bit420,
            LoopRestorationConfigurations);

    /// <summary>
    /// Verifies combined super-resolution and loop-restoration geometry for independently encoded 10-bit 4:2:2 content.
    /// </summary>
    [Fact]
    public void DecodeWithLoopRestorationAndSuperResolutionMatchesPinnedLibaomReference10Bit422()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLoopRestorationAndSuperResolution10Bit422,
            LoopRestorationConfigurations);

    /// <summary>
    /// Verifies combined super-resolution and loop-restoration geometry for independently encoded 12-bit 4:4:4 content.
    /// </summary>
    [Fact]
    public void DecodeWithLoopRestorationAndSuperResolutionMatchesPinnedLibaomReference12Bit444()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLoopRestorationAndSuperResolution12Bit444,
            LoopRestorationConfigurations);

    /// <summary>
    /// Verifies exact presented pixels and public metadata for independently encoded eight-, ten-, and twelve-bit
    /// active-restoration AVIF images across the available vector widths and the scalar fallback.
    /// </summary>
    /// <param name="provider">The AVIF input and matching reference-output naming context.</param>
    /// <param name="width">The expected presented width.</param>
    /// <param name="height">The expected presented height.</param>
    /// <param name="bitDepth">The expected public sample precision.</param>
    [Theory]
    [WithFile(TestImages.Heif.Av1Restoration8BitAvif, PixelTypes.Rgba32, 768, 512, HeifBitDepth.Bit8)]
    [WithFile(TestImages.Heif.Av1Restoration10BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit10)]
    [WithFile(TestImages.Heif.Av1Restoration12BitAvif, PixelTypes.Rgba32, 1024, 428, HeifBitDepth.Bit12)]
    public void DecodeWithLoopRestorationMatchesPinnedLibavifPresentation(
        TestImageProvider<Rgba32> provider,
        int width,
        int height,
        HeifBitDepth bitDepth)
    {
        AssertPresentedMetadata(provider, width, height, bitDepth);

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePresentedFixture,
            PresentationConfigurations,
            provider);
    }

    /// <summary>
    /// Verifies that the independently encoded AVIF presentation fixtures collectively select both restoration algorithms.
    /// </summary>
    [Fact]
    public void LoopRestorationPresentationFixturesSelectBothAlgorithms()
    {
        int restorationCoverage = GetRestorationCoverageFromAvif(TestFile.Create(TestImages.Heif.Av1Restoration8BitAvif).Bytes);
        restorationCoverage |= GetRestorationCoverageFromAvif(TestFile.Create(TestImages.Heif.Av1Restoration10BitAvif).Bytes);
        restorationCoverage |= GetRestorationCoverageFromAvif(TestFile.Create(TestImages.Heif.Av1Restoration12BitAvif).Bytes);

        int requiredCoverage = WienerRestorationCoverage | SelfGuidedRestorationCoverage;
        Assert.Equal(requiredCoverage, restorationCoverage & requiredCoverage);
    }

    /// <summary>
    /// Verifies film-grain template generation, block selection, overlap, chroma scaling, subsampling, high-bit-depth
    /// arithmetic, and exact native presentation samples against scalar libaom.
    /// </summary>
    [Fact]
    public void DecodeWithFilmGrainMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateFilmGrainFixtures, LoopRestorationConfigurations);

    /// <summary>
    /// Verifies that independently encoded AV1 streams exercise every normative coding-block partition shape.
    /// </summary>
    [Fact]
    public void IndependentFixturesCoverEveryPartitionType()
    {
        int coverage = GetPartitionCoverage(TestImages.Heif.Av1Cdef8BitPayload);
        coverage |= GetPartitionCoverage(TestImages.Heif.Av1Cdef10BitPayload);
        coverage |= GetPartitionCoverage(TestImages.Heif.Av1Cdef12BitPayload);

        Assert.Equal(RequiredPartitionCoverage, coverage & RequiredPartitionCoverage);
    }

    /// <summary>
    /// Validates every native profile fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateProfileNativeFixtures()
    {
        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile8BitMonochromeAvif,
            TestImages.Heif.Av1Profile8BitMonochromeReference,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv400,
            ObuSequenceProfile.Main);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile8Bit420Avif,
            TestImages.Heif.Av1Profile8Bit420Reference,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420,
            ObuSequenceProfile.Main);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile8Bit422Avif,
            TestImages.Heif.Av1Profile8Bit422Reference,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv422,
            ObuSequenceProfile.Professional);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile8Bit444Avif,
            TestImages.Heif.Av1Profile8Bit444Reference,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv444,
            ObuSequenceProfile.High);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile10BitMonochromeAvif,
            TestImages.Heif.Av1Profile10BitMonochromeReference,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv400,
            ObuSequenceProfile.Main);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile10Bit420Avif,
            TestImages.Heif.Av1Profile10Bit420Reference,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv420,
            ObuSequenceProfile.Main);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile10Bit422Avif,
            TestImages.Heif.Av1Profile10Bit422Reference,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv422,
            ObuSequenceProfile.Professional);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile10Bit444Avif,
            TestImages.Heif.Av1Profile10Bit444Reference,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv444,
            ObuSequenceProfile.High);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile12BitMonochromeAvif,
            TestImages.Heif.Av1Profile12BitMonochromeReference,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv400,
            ObuSequenceProfile.Professional);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile12Bit420Avif,
            TestImages.Heif.Av1Profile12Bit420Reference,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv420,
            ObuSequenceProfile.Professional);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile12Bit422Avif,
            TestImages.Heif.Av1Profile12Bit422Reference,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv422,
            ObuSequenceProfile.Professional);

        ValidateProfileNativeFixture(
            TestImages.Heif.Av1Profile12Bit444Avif,
            TestImages.Heif.Av1Profile12Bit444Reference,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444,
            ObuSequenceProfile.Professional);
    }

    /// <summary>
    /// Validates one independently encoded AVIF against its native Y4M reference and signaled sequence profile.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="referencePath">The native Y4M output produced by the pinned scalar libaom-backed decoder.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    /// <param name="sequenceProfile">The AV1 profile required by the bit-depth and chroma-format combination.</param>
    private static void ValidateProfileNativeFixture(
        string imagePath,
        string referencePath,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        ObuSequenceProfile sequenceProfile)
    {
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;
        (string chromaTag, string extendedChromaTag) = GetY4mColorSpace(bitDepth, colorFormat);
        string expectedHeader =
            $"YUV4MPEG2 W{ProfileFixtureWidth} H{ProfileFixtureHeight} F25:1 Ip A0:0 C{chromaTag} XYSCSS={extendedChromaTag} XCOLORRANGE=FULL\n";

        int headerTerminator = referenceBytes.AsSpan().IndexOf((byte)'\n');
        Assert.NotEqual(-1, headerTerminator);
        int fileHeaderLength = headerTerminator + 1;
        Assert.Equal(expectedHeader, Encoding.ASCII.GetString(referenceBytes, 0, fileHeaderLength));

        ReadOnlySpan<byte> nativeReference = referenceBytes.AsSpan(fileHeaderLength);
        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;
        Assert.True(nativeReference.StartsWith(frameHeader));
        nativeReference = nativeReference[frameHeader.Length..];

        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(ProfileFixtureWidth, frameBuffer.Width);
        Assert.Equal(ProfileFixtureHeight, frameBuffer.Height);
        Assert.Equal(bitDepth, frameBuffer.BitDepth);
        Assert.Equal(colorFormat, frameBuffer.ColorFormat);

        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        Assert.Equal(sequenceProfile, sequenceHeader.SequenceProfile);
        Assert.Equal(bitDepth, colorConfig.BitDepth);
        Assert.Equal(colorFormat, colorConfig.GetColorFormat());
        Assert.Equal(colorFormat == Av1ColorFormat.Yuv400, colorConfig.IsMonochrome);
        Assert.True(colorConfig.IsColorDescriptionPresent);
        Assert.Equal(ObuColorPrimaries.Bt709, colorConfig.ColorPrimaries);
        Assert.Equal(ObuTransferCharacteristics.Srgb, colorConfig.TransferCharacteristics);
        Assert.Equal(ObuMatrixCoefficients.Bt601, colorConfig.MatrixCoefficients);
        Assert.True(colorConfig.ColorRange);
        AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
    }

    /// <summary>
    /// Gets the Y4M chroma tags that encode one AV1 bit-depth and sampling-layout combination.
    /// </summary>
    /// <param name="bitDepth">The encoded AV1 sample precision.</param>
    /// <param name="colorFormat">The encoded AV1 chroma-sampling layout.</param>
    /// <returns>The Y4M <c>C</c> tag and extended <c>XYSCSS</c> tag.</returns>
    private static (string ChromaTag, string ExtendedChromaTag) GetY4mColorSpace(Av1BitDepth bitDepth, Av1ColorFormat colorFormat)
    {
        // Y4M uses a legacy 420jpeg name at eight bits, lowercase p in high-depth C tags, and uppercase P in the
        // corresponding XYSCSS tags. Keeping the exact spellings detects a reference generated with different layout.
        return (bitDepth, colorFormat) switch
        {
            (Av1BitDepth.EightBit, Av1ColorFormat.Yuv400) => ("mono", "400"),
            (Av1BitDepth.EightBit, Av1ColorFormat.Yuv420) => ("420jpeg", "420JPEG"),
            (Av1BitDepth.EightBit, Av1ColorFormat.Yuv422) => ("422", "422"),
            (Av1BitDepth.EightBit, Av1ColorFormat.Yuv444) => ("444", "444"),
            (Av1BitDepth.TenBit, Av1ColorFormat.Yuv400) => ("mono10", "400"),
            (Av1BitDepth.TenBit, Av1ColorFormat.Yuv420) => ("420p10", "420P10"),
            (Av1BitDepth.TenBit, Av1ColorFormat.Yuv422) => ("422p10", "422P10"),
            (Av1BitDepth.TenBit, Av1ColorFormat.Yuv444) => ("444p10", "444P10"),
            (Av1BitDepth.TwelveBit, Av1ColorFormat.Yuv400) => ("mono12", "400"),
            (Av1BitDepth.TwelveBit, Av1ColorFormat.Yuv420) => ("420p12", "420P12"),
            (Av1BitDepth.TwelveBit, Av1ColorFormat.Yuv422) => ("422p12", "422P12"),
            _ => ("444p12", "444P12")
        };
    }

    /// <summary>
    /// Validates every active-CDEF fixture under the hardware configuration selected by <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateActiveCdefFixtures()
    {
        ValidateActiveCdefFixture(
            TestImages.Heif.Av1Cdef8BitPayload,
            TestImages.Heif.Av1Cdef8BitReference,
            768,
            512,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420);

        ValidateActiveCdefFixture(
            TestImages.Heif.Av1Cdef10BitPayload,
            TestImages.Heif.Av1Cdef10BitReference,
            1024,
            428,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv444);

        ValidateActiveCdefFixture(
            TestImages.Heif.Av1Cdef12BitPayload,
            TestImages.Heif.Av1Cdef12BitReference,
            1024,
            428,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444);
    }

    /// <summary>
    /// Validates the active-palette native fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidatePaletteNativeFixture()
        => ValidateNativeFixture(
            TestImages.Heif.Av1Palette8BitPayload,
            TestImages.Heif.Av1Palette8BitReference,
            33,
            11,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv444,
            requireActiveCdef: false,
            requireActiveLoopFilter: false,
            requirePalette: true);

    /// <summary>
    /// Validates every active intra-block-copy native fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateIntraBlockCopyNativeFixtures()
    {
        ValidateIntraBlockCopyNativeFixture(
            TestImages.Heif.Av1IntraBlockCopy8BitAvif,
            TestImages.Heif.Av1IntraBlockCopy8BitReference,
            Av1BitDepth.EightBit);

        ValidateIntraBlockCopyNativeFixture(
            TestImages.Heif.Av1IntraBlockCopy10BitAvif,
            TestImages.Heif.Av1IntraBlockCopy10BitReference,
            Av1BitDepth.TenBit);

        ValidateIntraBlockCopyNativeFixture(
            TestImages.Heif.Av1IntraBlockCopy12BitAvif,
            TestImages.Heif.Av1IntraBlockCopy12BitReference,
            Av1BitDepth.TwelveBit);
    }

    /// <summary>
    /// Validates one independently encoded intra-block-copy AVIF against its native Y4M reference.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="referencePath">The native Y4M output produced by the pinned scalar libaom-backed decoder.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    private static void ValidateIntraBlockCopyNativeFixture(string imagePath, string referencePath, Av1BitDepth bitDepth)
    {
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;
        ReadOnlySpan<byte> fileHeader = bitDepth switch
        {
            Av1BitDepth.EightBit => "YUV4MPEG2 W512 H256 F25:1 Ip A0:0 C444 XYSCSS=444 XCOLORRANGE=FULL\n"u8,
            Av1BitDepth.TenBit => "YUV4MPEG2 W512 H256 F25:1 Ip A0:0 C444p10 XYSCSS=444P10 XCOLORRANGE=FULL\n"u8,
            _ => "YUV4MPEG2 W512 H256 F25:1 Ip A0:0 C444p12 XYSCSS=444P12 XCOLORRANGE=FULL\n"u8
        };

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;

        // The retained Y4M header locks the independently decoded reference to the expected dimensions, sampling,
        // bit depth, and full range. Only the following frame payload contains the planar Y, U, and V samples.
        ReadOnlySpan<byte> nativeReference = referenceBytes;
        Assert.True(nativeReference.StartsWith(fileHeader));
        nativeReference = nativeReference[fileHeader.Length..];
        Assert.True(nativeReference.StartsWith(frameHeader));
        nativeReference = nativeReference[frameHeader.Length..];

        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(512, frameBuffer.Width);
        Assert.Equal(256, frameBuffer.Height);
        Assert.Equal(bitDepth, frameBuffer.BitDepth);
        Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
        Assert.NotNull(decoder.FrameHeader);
        Assert.True(decoder.FrameHeader.AllowIntraBlockCopy);
        Assert.NotEqual(0, GetIntraBlockCopyBlockCount(decoder));
        AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
    }

    /// <summary>
    /// Runs the exact final-layer native and presentation comparisons with the default configuration.
    /// </summary>
    /// <param name="providerDump">The serialized AVIF input provider and reference-output naming context.</param>
    private static void ValidateProgressiveSingleReferenceFixtureWithDefaultConfiguration(string providerDump)
    {
        ValidateProgressiveSingleReferenceFixture(Configuration.Default);
        ValidatePresentedFixture(providerDump);
    }

    /// <summary>
    /// Runs the selected-spatial-layer native and presentation comparisons with the default configuration.
    /// </summary>
    /// <param name="providerDump">The serialized selected-layer provider and reference-output naming context.</param>
    private static void ValidateSelectedProgressiveSpatialLayerWithDefaultConfiguration(string providerDump)
    {
        ValidateSelectedProgressiveSpatialLayer(Configuration.Default);
        ValidatePresentedFixture(providerDump);
    }

    /// <summary>
    /// Runs the exact scaled-reference native and presentation comparison with the default configuration.
    /// </summary>
    /// <param name="providerDump">The serialized AVIF input provider and reference-output naming context.</param>
    private static void ValidateScaledReferenceFixtureWithDefaultConfiguration(string providerDump)
    {
        ValidateScaledReferenceFixture(Configuration.Default);
        ValidatePresentedFixture(providerDump);
    }

    /// <summary>
    /// Verifies the genuine size-changing layered fixture with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    private static void ValidateScaledReferenceFixture(Configuration configuration)
    {
        byte[] payload = TestFile.Create(TestImages.Heif.Av1ScaledReferencePayload).Bytes;
        byte[] baseReferenceBytes = TestFile.Create(TestImages.Heif.Av1ScaledReferenceBaseNativeReference).Bytes;
        byte[] referenceBytes = TestFile.Create(TestImages.Heif.Av1ScaledReferenceNativeReference).Bytes;
        ReadOnlySpan<byte> fileHeader =
            "YUV4MPEG2 W80 H80 F25:1 Ip A0:0 C444 XYSCSS=444 XCOLORRANGE=LIMITED\n"u8;

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;
        ReadOnlySpan<byte> nativeReference = referenceBytes;
        Assert.True(nativeReference.StartsWith(fileHeader));
        nativeReference = nativeReference[fileHeader.Length..];
        Assert.True(nativeReference.StartsWith(frameHeader));
        nativeReference = nativeReference[frameHeader.Length..];
        Assert.Equal(ScaledReferenceFixtureSize * ScaledReferenceFixtureSize * 3, nativeReference.Length);

        // Decode the independently declared base extent alone to prove that the retained reference is 40x40 rather
        // than relying on the 80x80 item presentation dimensions recorded by the container.
        using (Av1Decoder baseDecoder = new(configuration))
        using (Av1FrameBuffer<byte> baseFrameBuffer = baseDecoder.DecodeFrameBuffer(
            payload.AsSpan(0, ScaledReferenceFirstLayerSize),
            null,
            null,
            out _))
        {
            Assert.Equal(ScaledReferenceBaseLayerSize, baseFrameBuffer.Width);
            Assert.Equal(ScaledReferenceBaseLayerSize, baseFrameBuffer.Height);
            Assert.Equal(ScaledReferenceBaseLayerSize * ScaledReferenceBaseLayerSize * 3, baseReferenceBytes.Length);
            AssertNativePlanesEqual(baseDecoder, baseFrameBuffer, baseReferenceBytes);
        }

        // Exercise the same retained owner across two calls so the independently verified base samples are checked
        // in the exact decoder session that supplies the size-changing reference to the dependent frame.
        using (Av1Decoder sequenceDecoder = new(configuration))
        {
            sequenceDecoder.DecodeSequenceReference(
                payload.AsSpan(0, ScaledReferenceFirstLayerSize),
                null,
                null);

            Av1FrameBuffer<byte> retainedBaseFrameBuffer = Assert.IsType<Av1FrameBuffer<byte>>(sequenceDecoder.FrameBuffer);
            AssertNativePlanesEqual(sequenceDecoder, retainedBaseFrameBuffer, baseReferenceBytes);
            using Av1FrameBuffer<byte> sequenceFrameBuffer = sequenceDecoder.DecodeFrameBuffer(
                payload.AsSpan(ScaledReferenceFirstLayerSize),
                null,
                null,
                out _);

            AssertNativePlanesEqual(sequenceDecoder, sequenceFrameBuffer, nativeReference);
        }

        using Av1Decoder decoder = new(configuration);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(
            payload,
            null,
            null,
            out _,
            new Av1LayeredImageIndex(ScaledReferenceFirstLayerSize, 0, 0));

        Assert.Equal(ScaledReferenceFixtureSize, frameBuffer.Width);
        Assert.Equal(ScaledReferenceFixtureSize, frameBuffer.Height);
        Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
        Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
        Assert.Equal(1, frameBuffer.BufferY!.FastMemoryGroup.Count);
        Assert.Equal(1, frameBuffer.BufferCb!.FastMemoryGroup.Count);
        Assert.Equal(1, frameBuffer.BufferCr!.FastMemoryGroup.Count);

        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
        ObuFrameHeader finalFrameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);

        Assert.Equal(ScaledReferenceFixtureSize, sequenceHeader.MaxFrameWidth);
        Assert.Equal(ScaledReferenceFixtureSize, sequenceHeader.MaxFrameHeight);
        Assert.Equal(ObuFrameType.InterFrame, finalFrameHeader.FrameType);
        Assert.True(finalFrameHeader.LoopFilterParameters.ReferenceDeltaModeEnabled);
        Assert.NotEqual(0, finalFrameHeader.LoopFilterParameters.FilterLevelU);
        Assert.NotEqual(0, finalFrameHeader.LoopFilterParameters.FilterLevelV);
        int interBlockCount = 0;
        int intraBlockCount = 0;
        int skippedInterBlockCount = 0;
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
            {
                Av1SuperblockInfo superblock = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                foreach (Av1BlockModeInfo modeInfo in superblock.GetModeInfos())
                {
                    if (modeInfo.ReferenceFrames[0] >= Av1ReferenceFrameType.Last)
                    {
                        interBlockCount++;
                        if (modeInfo.Skip)
                        {
                            skippedInterBlockCount++;
                        }
                    }
                    else
                    {
                        intraBlockCount++;
                    }
                }
            }
        }

        Assert.NotEqual(0, interBlockCount);
        Assert.NotEqual(0, intraBlockCount);
        Assert.NotEqual(0, skippedInterBlockCount);
        AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
    }

    /// <summary>
    /// Verifies the selected base spatial layer with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    private static void ValidateSelectedProgressiveSpatialLayer(Configuration configuration)
    {
        byte[] payload = TestFile.Create(TestImages.Heif.Av1ScaledReferencePayload).Bytes;
        byte[] nativeReference =
            TestFile.Create(TestImages.Heif.Av1ScaledReferenceBaseNativeReference).Bytes;

        Assert.Equal(ScaledReferenceBaseLayerSize * ScaledReferenceBaseLayerSize * 3, nativeReference.Length);

        Av1LayeredImageIndex layeredImageIndex = new(ScaledReferenceFirstLayerSize, 0, 0);
        int selectedPayloadLength = layeredImageIndex.GetPayloadLength(
            payload.Length,
            new Av1LayerSelector(0));

        Assert.Equal(ScaledReferenceFirstLayerSize, selectedPayloadLength);

        using Av1Decoder decoder = new(configuration);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(
            payload.AsSpan(0, selectedPayloadLength),
            null,
            null,
            out _,
            layeredImageIndex);

        Assert.Equal(ScaledReferenceBaseLayerSize, frameBuffer.Width);
        Assert.Equal(ScaledReferenceBaseLayerSize, frameBuffer.Height);
        Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
        Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
        Assert.Equal(ObuFrameType.KeyFrame, Assert.IsType<ObuFrameHeader>(decoder.FrameHeader).FrameType);
        AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
    }

    /// <summary>
    /// Verifies the final dependent layer with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    private static void ValidateProgressiveSingleReferenceFixture(Configuration configuration)
    {
        byte[] payload = TestFile.Create(TestImages.Heif.Av1Progressive8BitPayload).Bytes;
        byte[] referenceBytes = TestFile.Create(TestImages.Heif.Av1Progressive8BitReference).Bytes;
        ReadOnlySpan<byte> fileHeader =
            "YUV4MPEG2 W33 H11 F25:1 Ip A0:0 C444alpha XYSCSS=444 XCOLORRANGE=FULL\n"u8;

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;

        int planeSampleCount = ProgressiveFixtureWidth * ProgressiveFixtureHeight;
        int frameSampleCount = planeSampleCount * 4;

        // The reference stores both progressive YUV444-alpha outputs in decode order. Select the second frame so this
        // assertion cannot pass by comparing only the independently decodable base layer.
        ReadOnlySpan<byte> nativeReference = referenceBytes;
        Assert.True(nativeReference.StartsWith(fileHeader));
        nativeReference = nativeReference[fileHeader.Length..];
        Assert.True(nativeReference.StartsWith(frameHeader));
        int storedFrameSize = frameHeader.Length + frameSampleCount;
        Assert.Equal(storedFrameSize * 2, nativeReference.Length);

        ReadOnlySpan<byte> finalFrameReference = nativeReference[storedFrameSize..];
        Assert.True(finalFrameReference.StartsWith(frameHeader));
        finalFrameReference = finalFrameReference[frameHeader.Length..];
        Assert.Equal(frameSampleCount, finalFrameReference.Length);

        // The Y4M stores the color item's Y, U, and V planes before the auxiliary alpha plane. Native AV1 reconstruction
        // is compared with exactly those first three planes of the final dependent frame.
        ReadOnlySpan<byte> colorReference = finalFrameReference[..(planeSampleCount * 3)];

        using Av1Decoder decoder = new(configuration);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(
            payload,
            null,
            null,
            out _,
            new Av1LayeredImageIndex(ProgressiveFirstLayerSize, 0, 0));

        Assert.Equal(ProgressiveFixtureWidth, frameBuffer.Width);
        Assert.Equal(ProgressiveFixtureHeight, frameBuffer.Height);
        Assert.Equal(Av1BitDepth.EightBit, frameBuffer.BitDepth);
        Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
        Assert.Equal(1, frameBuffer.BufferY!.FastMemoryGroup.Count);
        Assert.Equal(1, frameBuffer.BufferCb!.FastMemoryGroup.Count);
        Assert.Equal(1, frameBuffer.BufferCr!.FastMemoryGroup.Count);

        ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
        ObuFrameHeader finalFrameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);

        Assert.Equal(ObuFrameType.InterFrame, finalFrameHeader.FrameType);

        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        int interBlockCount = 0;

        // Traverse the final coding-block records once rather than revisiting every 4x4 map cell covered by each
        // block. The syntax assertions ensure that this fixture reaches only the completed single-reference path.
        for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
            {
                Av1SuperblockInfo superblock = frameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                foreach (Av1BlockModeInfo modeInfo in superblock.GetModeInfos())
                {
                    if (modeInfo.ReferenceFrames[0] < Av1ReferenceFrameType.Last)
                    {
                        continue;
                    }

                    Assert.Equal(Av1ReferenceFrameType.None, modeInfo.ReferenceFrames[1]);
                    Assert.Equal(Av1MotionMode.SimpleTranslation, modeInfo.MotionMode);
                    interBlockCount++;
                }
            }
        }

        Assert.NotEqual(0, interBlockCount);
        AssertNativePlanesEqual(decoder, frameBuffer, colorReference);

        DecoderOptions options = new() { Configuration = configuration, MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(TestImages.Heif.Av1Progressive8BitAvif).Bytes;
        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);

        Assert.Equal(ProgressiveFixtureWidth, image.Width);
        Assert.Equal(ProgressiveFixtureHeight, image.Height);
        Assert.Single(image.Frames);
        Assert.Equal(HeifBitDepth.Bit8, image.Metadata.GetHeifMetadata().BitDepth);
    }

    /// <summary>
    /// Parses the selected image-sequence tracks from a complete HEIF fixture.
    /// </summary>
    /// <param name="fileBytes">The complete HEIF file.</param>
    /// <returns>The bounded image-sequence model.</returns>
    private static HeifSequence ParseImageSequence(byte[] fileBytes)
    {
        using MemoryStream stream = new(fileBytes, false);
        Span<byte> scratch = stackalloc byte[32];
        while (stream.Position < stream.Length)
        {
            long boxLength = HeifBoxReader.ReadHeader(
                stream,
                stream.Length,
                scratch,
                out Heif4CharCode boxType,
                topLevel: true);

            long boxStart = stream.Position;
            if (boxType == Heif4CharCode.Moov)
            {
                HeifSequenceParser parser = new(new DecoderOptions { MaxFrames = 32 });
                return parser.Parse(stream, boxLength);
            }

            stream.Position = checked(boxStart + boxLength);
        }

        throw new InvalidImageContentException("The HEIF fixture contains no image sequence.");
    }

    /// <summary>
    /// Validates every lossless native fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateLosslessFixtures()
    {
        ValidateLosslessFixture(
            TestImages.Heif.Av1Lossless8BitAvif,
            TestImages.Heif.Av1Lossless8BitReference,
            Av1BitDepth.EightBit);

        ValidateLosslessFixture(
            TestImages.Heif.Av1Lossless10BitAvif,
            TestImages.Heif.Av1Lossless10BitReference,
            Av1BitDepth.TenBit);

        ValidateLosslessFixture(
            TestImages.Heif.Av1Lossless12BitAvif,
            TestImages.Heif.Av1Lossless12BitReference,
            Av1BitDepth.TwelveBit);
    }

    /// <summary>
    /// Validates every active super-resolution fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateSuperResolutionFixtures()
    {
        ValidateSuperResolutionFixture(
            TestImages.Heif.Av1SuperResolution8BitPayload,
            TestImages.Heif.Av1SuperResolution8BitReference,
            768,
            512,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420);

        ValidateSuperResolutionFixture(
            TestImages.Heif.Av1SuperResolution10BitPayload,
            TestImages.Heif.Av1SuperResolution10BitReference,
            1024,
            428,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv444);

        ValidateSuperResolutionFixture(
            TestImages.Heif.Av1SuperResolution12BitPayload,
            TestImages.Heif.Av1SuperResolution12BitReference,
            1024,
            428,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444);
    }

    /// <summary>
    /// Validates every active loop-restoration fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateLoopRestorationFixtures()
    {
        int restorationCoverage = ValidateLoopRestorationFixture(
            TestImages.Heif.Av1Restoration8BitPayload,
            TestImages.Heif.Av1Restoration8BitReference,
            768,
            512,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420);

        restorationCoverage |= ValidateLoopRestorationFixture(
            TestImages.Heif.Av1Restoration10BitPayload,
            TestImages.Heif.Av1Restoration10BitReference,
            1024,
            428,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv444);

        restorationCoverage |= ValidateLoopRestorationFixture(
            TestImages.Heif.Av1Restoration12BitPayload,
            TestImages.Heif.Av1Restoration12BitReference,
            1024,
            428,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444);

        // Exact output only proves both restoration algorithms when the independent fixture set
        // actually selects at least one unit of each type during every feature-runner invocation.
        int requiredCoverage = WienerRestorationCoverage | SelfGuidedRestorationCoverage;
        Assert.Equal(requiredCoverage, restorationCoverage & requiredCoverage);
    }

    /// <summary>
    /// Validates active restoration after super-resolution for 8-bit 4:2:0 content.
    /// </summary>
    private static void ValidateLoopRestorationAndSuperResolution8Bit420()
        => ValidateLoopRestorationFixture(
            TestImages.Heif.Av1RestorationSuperResolution8BitPayload,
            TestImages.Heif.Av1RestorationSuperResolution8BitReference,
            768,
            512,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420,
            requireSuperResolution: true);

    /// <summary>
    /// Validates active restoration after super-resolution for 10-bit 4:2:2 content.
    /// </summary>
    private static void ValidateLoopRestorationAndSuperResolution10Bit422()
        => ValidateLoopRestorationFixture(
            TestImages.Heif.Av1RestorationSuperResolution10BitPayload,
            TestImages.Heif.Av1RestorationSuperResolution10BitReference,
            512,
            256,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv422,
            requireSuperResolution: true);

    /// <summary>
    /// Validates active restoration after super-resolution for 12-bit 4:4:4 content.
    /// </summary>
    private static void ValidateLoopRestorationAndSuperResolution12Bit444()
        => ValidateLoopRestorationFixture(
            TestImages.Heif.Av1RestorationSuperResolution12BitPayload,
            TestImages.Heif.Av1RestorationSuperResolution12BitReference,
            1024,
            428,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444,
            requireSuperResolution: true);

    /// <summary>
    /// Validates every active film-grain fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateFilmGrainFixtures()
    {
        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain8BitPayload,
            TestImages.Heif.Av1FilmGrain8BitReference,
            100,
            60,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain10BitPayload,
            TestImages.Heif.Av1FilmGrain10BitReference,
            100,
            60,
            Av1BitDepth.TenBit,
            Av1ColorFormat.Yuv422);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain12BitPayload,
            TestImages.Heif.Av1FilmGrain12BitReference,
            100,
            60,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain8BitRestrictedPayload,
            TestImages.Heif.Av1FilmGrain8BitRestrictedReference,
            100,
            60,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420,
            requireRestrictedRange: true);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain8BitMonochromePayload,
            TestImages.Heif.Av1FilmGrain8BitMonochromeReference,
            100,
            60,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv400,
            requireRestrictedRange: true);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrain12BitIdentityPayload,
            TestImages.Heif.Av1FilmGrain12BitIdentityReference,
            100,
            60,
            Av1BitDepth.TwelveBit,
            Av1ColorFormat.Yuv444,
            requireRestrictedRange: true,
            requireIdentityMatrix: true);

        ValidateFilmGrainFixture(
            TestImages.Heif.Av1FilmGrainOddDimensionsPayload,
            TestImages.Heif.Av1FilmGrainOddDimensionsReference,
            33,
            11,
            Av1BitDepth.EightBit,
            Av1ColorFormat.Yuv420);
    }

    /// <summary>
    /// Validates one elementary-stream sample and its containing AVIF image.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="payloadPath">The AV1 elementary-stream sample extracted from the container.</param>
    /// <param name="referencePath">The native planar output produced by the pinned libaom decoder.</param>
    /// <param name="width">The expected displayed width.</param>
    /// <param name="height">The expected displayed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    /// <param name="metadataBitDepth">The expected public HEIF sample precision.</param>
    private static void ValidateFixture(
        string imagePath,
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        HeifBitDepth metadataBitDepth)
    {
        ValidateNativeFixture(payloadPath, referencePath, width, height, bitDepth, colorFormat, false);
        ValidatePresentedImage(imagePath, width, height, metadataBitDepth);
    }

    /// <summary>
    /// Validates complete native-plane reconstruction for one AV1 elementary-stream sample.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <param name="referencePath">The native planar output produced by the pinned libaom decoder.</param>
    /// <param name="width">The expected reconstructed width.</param>
    /// <param name="height">The expected reconstructed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    /// <param name="requireActiveCdef">Indicates whether the stream must signal and select nonzero CDEF strengths.</param>
    /// <param name="requireSuperResolution">Indicates whether the stream must use normative horizontal upscaling.</param>
    /// <param name="requireLoopRestoration">Indicates whether the stream must select at least one loop-restoration unit.</param>
    /// <param name="requireFilmGrain">Indicates whether the displayed frame must synthesize signaled film grain.</param>
    /// <param name="requireRestrictedRange">Indicates whether film grain must clip every plane to its restricted range.</param>
    /// <param name="requireIdentityMatrix">Indicates whether restricted chroma clipping must use the luma endpoints.</param>
    /// <param name="requireActiveLoopFilter">Indicates whether the stream must signal a nonzero deblocking strength.</param>
    /// <param name="requirePalette">Indicates whether the stream must select palette prediction for luma and chroma.</param>
    /// <returns>A bit mask containing every selected loop-restoration filter type.</returns>
    private static int ValidateNativeFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        bool requireActiveCdef,
        bool requireSuperResolution = false,
        bool requireLoopRestoration = false,
        bool requireFilmGrain = false,
        bool requireRestrictedRange = false,
        bool requireIdentityMatrix = false,
        bool requireActiveLoopFilter = true,
        bool requirePalette = false)
    {
        int restorationCoverage = 0;
        byte[] payload = TestFile.Create(payloadPath).Bytes;
        byte[] reference = TestFile.Create(referencePath).Bytes;
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(width, frameBuffer.Width);
        Assert.Equal(height, frameBuffer.Height);
        Assert.Equal(bitDepth, frameBuffer.BitDepth);
        Assert.Equal(colorFormat, frameBuffer.ColorFormat);
        Assert.NotNull(decoder.FrameHeader);

        if (requireSuperResolution)
        {
            ObuFrameSize frameSize = decoder.FrameHeader.FrameSize;
            Assert.True(frameSize.FrameWidth < frameSize.SuperResolutionUpscaledWidth);
            Assert.Equal(width, frameSize.SuperResolutionUpscaledWidth);
            if (!requireLoopRestoration)
            {
                // The original super-resolution fixtures isolate upscaling by disabling restoration.
                Assert.False(decoder.FrameHeader.LoopRestorationParameters.UsesLoopRestoration);
            }
        }

        if (requireActiveLoopFilter)
        {
            ObuLoopFilterParameters filterParameters = decoder.FrameHeader.LoopFilterParameters;
            Assert.True(
                filterParameters.FilterLevel[0] != 0
                || filterParameters.FilterLevel[1] != 0
                || filterParameters.FilterLevelU != 0
                || filterParameters.FilterLevelV != 0);
        }

        if (requireActiveCdef)
        {
            Assert.NotNull(decoder.SequenceHeader);
            Assert.True(decoder.SequenceHeader.EnableCdef);
            Assert.False(decoder.FrameHeader.LoopRestorationParameters.UsesLoopRestoration);
            Assert.NotNull(decoder.FrameInfo);
            ObuConstraintDirectionalEnhancementFilterParameters parameters = decoder.FrameHeader.CdefParameters;
            bool hasActiveStrength = false;
            int superblockSizeLog2 = decoder.SequenceHeader.SuperblockSizeLog2;
            int superblockColumnCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
            int superblockRowCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
            for (int superblockRow = 0; superblockRow < superblockRowCount && !hasActiveStrength; superblockRow++)
            {
                for (int superblockColumn = 0; superblockColumn < superblockColumnCount && !hasActiveStrength; superblockColumn++)
                {
                    Span<int> selectedStrengths = decoder.FrameInfo.GetCdefStrength(new Point(superblockColumn, superblockRow));

                    // Unassigned entries belong to completely skipped units. Every assigned index must resolve through
                    // the signaled table before the exact output can establish that CDEF changed reconstructed samples.
                    foreach (int selectedStrength in selectedStrengths)
                    {
                        if (selectedStrength >= 0
                            && (parameters.YStrength[selectedStrength] != 0 || parameters.UvStrength[selectedStrength] != 0))
                        {
                            hasActiveStrength = true;
                            break;
                        }
                    }
                }
            }

            // The independent output only proves CDEF when the encoded frame selects at least one nonzero strength.
            Assert.True(hasActiveStrength);
        }

        if (requireLoopRestoration)
        {
            Assert.True(decoder.FrameHeader.LoopRestorationParameters.UsesLoopRestoration);
            Assert.NotNull(decoder.FrameInfo);
            restorationCoverage = GetRestorationCoverage(decoder);
            Assert.NotEqual(0, restorationCoverage);
        }

        if (requireFilmGrain)
        {
            Assert.True(decoder.FrameHeader.FilmGrainParameters.ApplyGrain);
        }

        if (requireRestrictedRange)
        {
            Assert.True(decoder.FrameHeader.FilmGrainParameters.ClipToRestrictedRange);
        }

        if (requireIdentityMatrix)
        {
            Assert.NotNull(decoder.SequenceHeader);
            Assert.Equal(ObuMatrixCoefficients.Identity, decoder.SequenceHeader.ColorConfig.MatrixCoefficients);
        }

        if (requirePalette)
        {
            Assert.Equal(RequiredPaletteCoverage, GetPaletteCoverage(decoder));
        }

        AssertNativePlanesEqual(decoder, frameBuffer, reference);
        return restorationCoverage;
    }

    /// <summary>
    /// Validates lossless frame syntax and complete native reconstruction for one AVIF image.
    /// </summary>
    /// <param name="imagePath">The independently encoded AVIF container.</param>
    /// <param name="referencePath">The raw planar output produced by the pinned scalar libaom decoder.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    private static void ValidateLosslessFixture(string imagePath, string referencePath, Av1BitDepth bitDepth)
    {
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;
        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        ReadOnlySpan<byte> nativeReference = referenceBytes;
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(LosslessFixtureWidth, frameBuffer.Width);
        Assert.Equal(LosslessFixtureHeight, frameBuffer.Height);
        Assert.Equal(bitDepth, frameBuffer.BitDepth);
        Assert.Equal(Av1ColorFormat.Yuv444, frameBuffer.ColorFormat);
        Assert.NotNull(decoder.SequenceHeader);
        Assert.NotNull(decoder.FrameHeader);
        Assert.NotNull(decoder.FrameInfo);
        Assert.True(decoder.FrameHeader.CodedLossless);
        Assert.True(decoder.FrameHeader.AllLossless);
        Assert.Equal(0, decoder.FrameHeader.QuantizationParameters.BaseQIndex);
        Assert.Equal(ObuMatrixCoefficients.Identity, decoder.SequenceHeader.ColorConfig.MatrixCoefficients);
        Assert.False(decoder.FrameHeader.AllowIntraBlockCopy);
        Assert.Equal(0, GetPaletteCoverage(decoder));

        bool hasCodedResidual = false;
        int superblockSizeLog2 = decoder.SequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        ReadOnlySpan<Av1Plane> planes = [Av1Plane.Y, Av1Plane.U, Av1Plane.V];
        for (int superblockRow = 0; superblockRow < superblockRowCount && !hasCodedResidual; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount && !hasCodedResidual; superblockColumn++)
            {
                Point superblock = new(superblockColumn, superblockRow);
                foreach (Av1Plane plane in planes)
                {
                    Span<int> coefficients = plane switch
                    {
                        Av1Plane.Y => decoder.FrameInfo.GetCoefficientsY(superblock),
                        Av1Plane.U => decoder.FrameInfo.GetCoefficientsU(superblock),
                        _ => decoder.FrameInfo.GetCoefficientsV(superblock)
                    };

                    // Each transform reserves an end index followed by its coefficients. Any nonzero stored value
                    // proves that exact output traversed coefficient decoding, inverse quantization, and lossless WHT.
                    foreach (int coefficient in coefficients)
                    {
                        if (coefficient != 0)
                        {
                            hasCodedResidual = true;
                            break;
                        }
                    }

                    if (hasCodedResidual)
                    {
                        break;
                    }
                }
            }
        }

        Assert.True(hasCodedResidual);

        AssertNativePlanesEqual(decoder, frameBuffer, nativeReference);
    }

    /// <summary>
    /// Validates one independently encoded stream that activates constrained directional enhancement filtering.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <param name="referencePath">The native planar output produced by the pinned scalar libaom decoder.</param>
    /// <param name="width">The expected reconstructed width.</param>
    /// <param name="height">The expected reconstructed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    private static void ValidateActiveCdefFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat)
        => ValidateNativeFixture(payloadPath, referencePath, width, height, bitDepth, colorFormat, requireActiveCdef: true);

    /// <summary>
    /// Validates one independently encoded stream that activates normative super-resolution.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <param name="referencePath">The native planar output produced by the pinned scalar libaom decoder.</param>
    /// <param name="width">The expected upscaled width.</param>
    /// <param name="height">The expected reconstructed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    private static void ValidateSuperResolutionFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat)
        => ValidateNativeFixture(
            payloadPath,
            referencePath,
            width,
            height,
            bitDepth,
            colorFormat,
            requireActiveCdef: false,
            requireSuperResolution: true);

    /// <summary>
    /// Validates one independently encoded stream that activates normative loop restoration.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <param name="referencePath">The native planar output produced by the pinned scalar libaom decoder.</param>
    /// <param name="width">The expected reconstructed width.</param>
    /// <param name="height">The expected reconstructed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    /// <param name="requireSuperResolution">Whether the stream must upscale from a narrower coded frame.</param>
    /// <returns>A bit mask containing every selected loop-restoration filter type.</returns>
    private static int ValidateLoopRestorationFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        bool requireSuperResolution = false)
        => ValidateNativeFixture(
            payloadPath,
            referencePath,
            width,
            height,
            bitDepth,
            colorFormat,
            requireActiveCdef: false,
            requireSuperResolution: requireSuperResolution,
            requireLoopRestoration: true);

    /// <summary>
    /// Validates one independently encoded stream that applies film grain to the displayed samples.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <param name="referencePath">The native planar output produced by the pinned scalar libaom decoder.</param>
    /// <param name="width">The expected displayed width.</param>
    /// <param name="height">The expected displayed height.</param>
    /// <param name="bitDepth">The expected AV1 sample precision.</param>
    /// <param name="colorFormat">The expected native chroma-sampling layout.</param>
    /// <param name="requireRestrictedRange">Whether film grain must clip every plane to its restricted range.</param>
    /// <param name="requireIdentityMatrix">Whether restricted chroma clipping must use the luma endpoints.</param>
    private static void ValidateFilmGrainFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        bool requireRestrictedRange = false,
        bool requireIdentityMatrix = false)
        => ValidateNativeFixture(
            payloadPath,
            referencePath,
            width,
            height,
            bitDepth,
            colorFormat,
            requireActiveCdef: false,
            requireFilmGrain: true,
            requireRestrictedRange: requireRestrictedRange,
            requireIdentityMatrix: requireIdentityMatrix,
            requireActiveLoopFilter: false);

    /// <summary>
    /// Validates the public presentation and metadata produced from one complete AVIF container.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="width">The expected displayed width.</param>
    /// <param name="height">The expected displayed height.</param>
    /// <param name="metadataBitDepth">The expected public HEIF sample precision.</param>
    private static void ValidatePresentedImage(string imagePath, int width, int height, HeifBitDepth metadataBitDepth)
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        using Image<Rgba64> image = Image.Load<Rgba64>(options, imageBytes);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Single(image.Frames);
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(metadataBitDepth, metadata.BitDepth);
    }

    /// <summary>
    /// Verifies the public dimensions, frame count, compression method, and sample precision of one AVIF input.
    /// </summary>
    /// <param name="provider">The AVIF input provider.</param>
    /// <param name="width">The expected displayed width.</param>
    /// <param name="height">The expected displayed height.</param>
    /// <param name="bitDepth">The expected public HEIF sample precision.</param>
    private static void AssertPresentedMetadata(
        TestImageProvider<Rgba32> provider,
        int width,
        int height,
        HeifBitDepth bitDepth)
    {
        using Image<Rgba32> image = provider.GetImage();
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Single(image.Frames);

        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(bitDepth, metadata.BitDepth);
    }

    /// <summary>
    /// Compares one AVIF presentation with its retained output through the repository reference-image contract.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidatePresentedFixture(string providerDump)
    {
        TestImageProvider<Rgba32> provider =
            FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);

        using Image<Rgba32> image = provider.GetImage();

        // CICP records the AVIF source component layout, but PNG permits only the identity matrix. The debug image
        // is a pixel artifact; the test verifies source metadata independently where that is part of the contract.
        image.DebugSave(provider, new PngEncoder { SkipMetadata = true });

        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Compares the final visible frame of one AVIF sequence through the repository reference-image contract.
    /// </summary>
    /// <param name="providerDump">The serialized input provider and reference-output naming context.</param>
    private static void ValidateFinalSequencePresentation(string providerDump)
    {
        TestImageProvider<Rgba32> provider =
            FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);

        using Image<Rgba32> sequence = provider.GetImage();
        using Image<Rgba32> finalFrame = sequence.Frames.CloneFrame(sequence.Frames.Count - 1);

        // The retained source CICP matrix cannot be represented in a PNG cICP chunk. Omit metadata only from the
        // diagnostic output; the exact reference comparison below still consumes the original decoded image.
        finalFrame.DebugSave(provider, new PngEncoder { SkipMetadata = true });

        finalFrame.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    /// <summary>
    /// Verifies that the sole AV1 image item in an independently packaged AVIF uses normative super-resolution.
    /// </summary>
    /// <param name="imageBytes">The complete AVIF file.</param>
    private static void AssertUsesSuperResolution(Span<byte> imageBytes)
    {
        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.NotNull(decoder.FrameHeader);
        ObuFrameSize frameSize = decoder.FrameHeader.FrameSize;
        Assert.True(frameSize.FrameWidth < frameSize.SuperResolutionUpscaledWidth);
        Assert.Equal(frameBuffer.Width, frameSize.SuperResolutionUpscaledWidth);
    }

    /// <summary>
    /// Verifies that the sole AV1 image item in an independently encoded AVIF selects luma and chroma palettes.
    /// </summary>
    /// <param name="imageBytes">The complete AVIF file.</param>
    private static void AssertUsesPalette(Span<byte> imageBytes)
    {
        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(RequiredPaletteCoverage, GetPaletteCoverage(decoder));
    }

    /// <summary>
    /// Verifies that the sole AV1 image item in an independently encoded AVIF selects intra-block-copy prediction.
    /// </summary>
    /// <param name="imageBytes">The complete AVIF file.</param>
    private static void AssertUsesIntraBlockCopy(Span<byte> imageBytes)
    {
        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.NotNull(decoder.FrameHeader);
        Assert.True(decoder.FrameHeader.AllowIntraBlockCopy);
        Assert.NotEqual(0, GetIntraBlockCopyBlockCount(decoder));
    }

    /// <summary>
    /// Decodes the sole image item in an independently generated AVIF fixture and returns its restoration coverage.
    /// </summary>
    /// <param name="imageBytes">The complete AVIF file.</param>
    /// <returns>A bit mask containing every selected loop-restoration filter type.</returns>
    private static int GetRestorationCoverageFromAvif(Span<byte> imageBytes)
    {
        Span<byte> payload = GetSoleAv1ItemPayload(imageBytes);
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.NotNull(decoder.FrameHeader);
        Assert.True(decoder.FrameHeader.LoopRestorationParameters.UsesLoopRestoration);
        Assert.NotNull(decoder.FrameInfo);
        int restorationCoverage = GetRestorationCoverage(decoder);
        Assert.NotEqual(0, restorationCoverage);
        return restorationCoverage;
    }

    /// <summary>
    /// Gets the partition types selected by one independently encoded AV1 elementary stream.
    /// </summary>
    /// <param name="payloadPath">The AV1 elementary-stream sample.</param>
    /// <returns>A bit mask containing every selected partition type.</returns>
    private static int GetPartitionCoverage(string payloadPath)
    {
        byte[] payload = TestFile.Create(payloadPath).Bytes;
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.NotNull(decoder.SequenceHeader);
        Assert.NotNull(decoder.FrameInfo);
        int superblockSizeLog2 = decoder.SequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        int halfSuperblockSize = 1 << (superblockSizeLog2 - 1);
        int coverage = 0;

        // Mode records retain their bitstream traversal order and store each final coding block once, so iterating
        // the parsed count observes every selected leaf partition without repeatedly visiting its covered 4x4 cells.
        // Split itself creates no mode record. All other partition types are terminal, so a leaf below half the
        // superblock size on both axes proves that the parser reached it through at least one recursive split.
        for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
            {
                Av1SuperblockInfo superblock = decoder.FrameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                foreach (Av1BlockModeInfo modeInfo in superblock.GetModeInfos())
                {
                    coverage |= 1 << (int)modeInfo.PartitionType;
                    if (modeInfo.BlockSize.GetWidth() < halfSuperblockSize && modeInfo.BlockSize.GetHeight() < halfSuperblockSize)
                    {
                        coverage |= 1 << (int)Av1PartitionType.Split;
                    }
                }
            }
        }

        return coverage;
    }

    /// <summary>
    /// Gets the complete media-data payload from a single-item AVIF conformance fixture.
    /// </summary>
    /// <param name="imageBytes">The complete AVIF file.</param>
    /// <returns>The sole AV1 image-item payload.</returns>
    private static Span<byte> GetSoleAv1ItemPayload(Span<byte> imageBytes)
    {
        int offset = 0;
        while (offset < imageBytes.Length)
        {
            int headerLength = HeifBoxReader.ParseHeader(imageBytes[offset..], out long payloadLength, out Heif4CharCode boxType);
            Assert.InRange(payloadLength, 0, int.MaxValue);
            int payloadLength32 = (int)payloadLength;

            if (boxType == Heif4CharCode.Mdat)
            {
                // Every conformance container passed here deliberately stores its sole AV1 item as the complete
                // mdat payload, so feature assertions inspect the exact bytes used by public presentation decoding.
                return imageBytes.Slice(offset + headerLength, payloadLength32);
            }

            offset = checked(offset + headerLength + payloadLength32);
        }

        Assert.Fail("The AVIF fixture does not contain a media-data box.");
        return [];
    }

    /// <summary>
    /// Returns the luma and chroma palette classes selected by a decoded frame.
    /// </summary>
    /// <param name="decoder">The decoder after tile parsing and reconstruction.</param>
    /// <returns>A bit mask containing the selected plane classes.</returns>
    private static int GetPaletteCoverage(Av1Decoder decoder)
    {
        Assert.NotNull(decoder.FrameHeader);
        Assert.NotNull(decoder.FrameInfo);
        int modeInfoWidth = Av1Math.DivideLog2Ceiling(decoder.FrameHeader.FrameSize.FrameWidth, Av1Constants.ModeInfoSizeLog2);
        int modeInfoHeight = Av1Math.DivideLog2Ceiling(decoder.FrameHeader.FrameSize.FrameHeight, Av1Constants.ModeInfoSizeLog2);
        int paletteCoverage = 0;
        for (int y = 0; y < modeInfoHeight; y++)
        {
            for (int x = 0; x < modeInfoWidth; x++)
            {
                Av1BlockModeInfo modeInfo = decoder.FrameInfo.GetModeInfoAt(new Point(x, y));
                if (modeInfo.GetPaletteSize(Av1PlaneType.Y) != 0)
                {
                    paletteCoverage |= LumaPaletteCoverage;
                }

                if (modeInfo.GetPaletteSize(Av1PlaneType.Uv) != 0)
                {
                    paletteCoverage |= ChromaPaletteCoverage;
                }
            }
        }

        return paletteCoverage;
    }

    /// <summary>
    /// Counts the final coding blocks that select intra-block-copy prediction.
    /// </summary>
    /// <param name="decoder">The decoder after tile parsing and reconstruction.</param>
    /// <returns>The number of selected intra-block-copy coding blocks.</returns>
    private static int GetIntraBlockCopyBlockCount(Av1Decoder decoder)
    {
        Assert.NotNull(decoder.SequenceHeader);
        Assert.NotNull(decoder.FrameInfo);
        int superblockSizeLog2 = decoder.SequenceHeader.SuperblockSizeLog2;
        int superblockColumnCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameWidth, superblockSizeLog2) >> superblockSizeLog2;
        int superblockRowCount = Av1Math.AlignPowerOf2(decoder.SequenceHeader.MaxFrameHeight, superblockSizeLog2) >> superblockSizeLog2;
        int blockCount = 0;

        // Mode records retain final coding blocks in bitstream order. Traversing each record once counts selected
        // intra-block-copy operations without repeatedly visiting the 4x4 cells covered by a larger block.
        for (int superblockRow = 0; superblockRow < superblockRowCount; superblockRow++)
        {
            for (int superblockColumn = 0; superblockColumn < superblockColumnCount; superblockColumn++)
            {
                Av1SuperblockInfo superblock = decoder.FrameInfo.GetSuperblock(new Point(superblockColumn, superblockRow));
                foreach (Av1BlockModeInfo modeInfo in superblock.GetModeInfos())
                {
                    if (modeInfo.UseIntraBlockCopy)
                    {
                        blockCount++;
                    }
                }
            }
        }

        return blockCount;
    }

    /// <summary>
    /// Returns the restoration algorithms selected by the decoded frame's unit grids.
    /// </summary>
    /// <param name="decoder">The decoder after tile parsing and reconstruction.</param>
    /// <returns>A bit mask containing every selected loop-restoration filter type.</returns>
    private static int GetRestorationCoverage(Av1Decoder decoder)
    {
        int restorationCoverage = 0;
        for (int plane = 0; plane < decoder.SequenceHeader!.ColorConfig.PlaneCount; plane++)
        {
            int rowCount = decoder.FrameInfo!.GetLoopRestorationUnitRowCount(plane);
            int columnCount = decoder.FrameInfo.GetLoopRestorationUnitColumnCount(plane);
            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    Av1RestorationFilterType filterType = decoder.FrameInfo.GetLoopRestorationUnit(plane, row, column).FilterType;
                    if (filterType != Av1RestorationFilterType.None)
                    {
                        restorationCoverage |= 1 << (int)filterType;
                    }
                }
            }
        }

        return restorationCoverage;
    }

    /// <summary>
    /// Compares every visible native component sample with the independent planar reference.
    /// </summary>
    /// <param name="decoder">The decoder state used to identify the coded block containing a mismatch.</param>
    /// <param name="frameBuffer">The reconstructed AV1 component planes.</param>
    /// <param name="reference">The planar Y, U, and V samples produced by the current-main libaom decoder.</param>
    /// <param name="frameIndex">The zero-based sequence-frame index, or -1 for a standalone sample.</param>
    private static void AssertNativePlanesEqual(
        Av1Decoder decoder,
        Av1FrameBuffer<byte> frameBuffer,
        ReadOnlySpan<byte> reference,
        int frameIndex = -1)
    {
        (int chromaSubsamplingX, int chromaSubsamplingY) = frameBuffer.ColorFormat switch
        {
            Av1ColorFormat.Yuv420 => (1, 1),
            Av1ColorFormat.Yuv422 => (1, 0),
            _ => (0, 0)
        };

        int referenceOffset = 0;
        ReadOnlySpan<Av1Plane> planes = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400
            ? [Av1Plane.Y]
            : [Av1Plane.Y, Av1Plane.U, Av1Plane.V];
        int mismatchCount = 0;
        Av1Plane largestMismatchPlane = default;
        int largestMismatchX = 0;
        int largestMismatchY = 0;
        ushort largestExpected = 0;
        ushort largestActual = 0;
        StringBuilder mismatchDescription = null;

        foreach (Av1Plane plane in planes)
        {
            int subsamplingX = plane == Av1Plane.Y ? 0 : chromaSubsamplingX;
            int subsamplingY = plane == Av1Plane.Y ? 0 : chromaSubsamplingY;
            int planeWidth = GetSubsampledSize(frameBuffer.Width, subsamplingX);
            int planeHeight = GetSubsampledSize(frameBuffer.Height, subsamplingY);

            if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
            {
                Buffer2DRegion<byte> actualPlane = frameBuffer.DeriveBlockPointer(plane, subsamplingX, subsamplingY);
                for (int y = 0; y < planeHeight; y++)
                {
                    Span<byte> actualRow = actualPlane.DangerousGetRowSpan(y)[..planeWidth];
                    ReadOnlySpan<byte> expectedRow = reference.Slice(referenceOffset, planeWidth);
                    for (int x = 0; x < planeWidth; x++)
                    {
                        if (expectedRow[x] != actualRow[x])
                        {
                            if (mismatchCount < 16)
                            {
                                mismatchDescription ??= new StringBuilder();
                                mismatchDescription.Append(CultureInfo.InvariantCulture, $" {plane}({x},{y})={expectedRow[x]}/{actualRow[x]}");
                            }

                            if (mismatchCount == 0 || Math.Abs(expectedRow[x] - actualRow[x]) > Math.Abs(largestExpected - largestActual))
                            {
                                largestMismatchPlane = plane;
                                largestMismatchX = x;
                                largestMismatchY = y;
                                largestExpected = expectedRow[x];
                                largestActual = actualRow[x];
                            }

                            mismatchCount++;
                        }
                    }

                    referenceOffset += planeWidth;
                }
            }
            else
            {
                // aomdec writes high-bit-depth YUV as little-endian 16-bit values, independently of host endianness.
                for (int y = 0; y < planeHeight; y++)
                {
                    Span<ushort> actualRow = frameBuffer.GetHighBitDepthRowSpan(plane, y, subsamplingX, subsamplingY);
                    for (int x = 0; x < planeWidth; x++)
                    {
                        ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(reference.Slice(referenceOffset, sizeof(ushort)));
                        if (expected != actualRow[x])
                        {
                            if (mismatchCount < 16)
                            {
                                mismatchDescription ??= new StringBuilder();
                                mismatchDescription.Append(CultureInfo.InvariantCulture, $" {plane}({x},{y})={expected}/{actualRow[x]}");
                            }

                            if (mismatchCount == 0 || Math.Abs(expected - actualRow[x]) > Math.Abs(largestExpected - largestActual))
                            {
                                largestMismatchPlane = plane;
                                largestMismatchX = x;
                                largestMismatchY = y;
                                largestExpected = expected;
                                largestActual = actualRow[x];
                            }

                            mismatchCount++;
                        }

                        referenceOffset += sizeof(ushort);
                    }
                }
            }
        }

        Assert.Equal(reference.Length, referenceOffset);
        AssertSampleEqual(
            decoder,
            largestMismatchPlane,
            largestMismatchX,
            largestMismatchY,
            largestExpected,
            largestActual,
            mismatchCount,
            mismatchDescription?.ToString() ?? string.Empty,
            frameIndex);
    }

    /// <summary>
    /// Calculates a component dimension after chroma subsampling with the AV1 rounding rule.
    /// </summary>
    /// <param name="size">The luma dimension.</param>
    /// <param name="subsampling">The component subsampling shift.</param>
    /// <returns>The subsampled component dimension.</returns>
    private static int GetSubsampledSize(int size, int subsampling)
        => (size + (1 << subsampling) - 1) >> subsampling;

    /// <summary>
    /// Reports the exact component coordinate when independently decoded samples differ.
    /// </summary>
    /// <param name="decoder">The decoder state used to identify the coded block containing the sample.</param>
    /// <param name="plane">The compared component plane.</param>
    /// <param name="x">The sample X coordinate.</param>
    /// <param name="y">The sample Y coordinate.</param>
    /// <param name="expected">The reference sample.</param>
    /// <param name="actual">The reconstructed sample.</param>
    /// <param name="mismatchCount">The total number of unequal native samples.</param>
    /// <param name="mismatchDescription">The first unequal samples in plane traversal order.</param>
    /// <param name="frameIndex">The zero-based sequence-frame index, or -1 for a standalone sample.</param>
    private static void AssertSampleEqual(
        Av1Decoder decoder,
        Av1Plane plane,
        int x,
        int y,
        ushort expected,
        ushort actual,
        int mismatchCount,
        string mismatchDescription,
        int frameIndex)
    {
        if (expected != actual)
        {
            ObuSequenceHeader sequenceHeader = Assert.IsType<ObuSequenceHeader>(decoder.SequenceHeader);
            int subsamplingX = plane == Av1Plane.Y || !sequenceHeader.ColorConfig.SubSamplingX ? 0 : 1;
            int subsamplingY = plane == Av1Plane.Y || !sequenceHeader.ColorConfig.SubSamplingY ? 0 : 1;
            Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
            int modeInfoColumn = (x << subsamplingX) >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoRow = (y << subsamplingY) >> Av1Constants.ModeInfoSizeLog2;
            Av1BlockModeInfo modeInfo = frameInfo.GetModeInfoAt(new Point(modeInfoColumn, modeInfoRow));
            int blockColumn = modeInfoColumn;
            while (blockColumn > 0 && ReferenceEquals(frameInfo.GetModeInfoAt(new Point(blockColumn - 1, modeInfoRow)), modeInfo))
            {
                blockColumn--;
            }

            int blockRow = modeInfoRow;
            while (blockRow > 0 && ReferenceEquals(frameInfo.GetModeInfoAt(new Point(modeInfoColumn, blockRow - 1)), modeInfo))
            {
                blockRow--;
            }

            int superblockSize = frameInfo.SuperblockModeInfoSize;
            Av1SuperblockInfo superblock = frameInfo.GetSuperblock(new Point(blockColumn / superblockSize, blockRow / superblockSize));
            Span<Av1TransformInfo> transforms = superblock.GetTransformInfoY().Slice(
                modeInfo.GetFirstTransformLocation(Av1Plane.Y),
                modeInfo.GetTransformUnitCount(Av1Plane.Y));

            Av1TransformInfo containingTransform = transforms[0];
            int containingTransformIndex = 0;
            int transformColumn = modeInfoColumn - blockColumn;
            int transformRow = modeInfoRow - blockRow;
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Av1TransformInfo transform = transforms[transformIndex];
                if (transformColumn >= transform.OffsetX && transformColumn < transform.OffsetX + transform.Size.Get4x4WideCount()
                    && transformRow >= transform.OffsetY && transformRow < transform.OffsetY + transform.Size.Get4x4HighCount())
                {
                    containingTransform = transform;
                    containingTransformIndex = transformIndex;
                    break;
                }
            }

            int superblockTransformIndex = modeInfo.GetFirstTransformLocation(Av1Plane.Y) + containingTransformIndex;
            Span<Av1TransformInfo> superblockTransforms = superblock.GetTransformInfoY();
            Span<int> superblockCoefficients = superblock.CoefficientsY;
            int coefficientOffset = 0;
            for (int transformIndex = 0; transformIndex < superblockTransformIndex; transformIndex++)
            {
                if (superblockTransforms[transformIndex].CodeBlockFlag)
                {
                    coefficientOffset += superblockCoefficients[coefficientOffset] + 1;
                }
            }

            StringBuilder coefficientDescription = new();
            if (containingTransform.CodeBlockFlag)
            {
                int coefficientCount = superblockCoefficients[coefficientOffset];
                coefficientDescription.Append(CultureInfo.InvariantCulture, $", quantized-coefficients={coefficientCount}:[");
                for (int coefficientIndex = 0; coefficientIndex < coefficientCount; coefficientIndex++)
                {
                    if (coefficientIndex != 0)
                    {
                        coefficientDescription.Append(',');
                    }

                    coefficientDescription.Append(superblockCoefficients[coefficientOffset + coefficientIndex + 1]);
                }

                coefficientDescription.Append(']');
            }

            ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
            int cdefUnitColumn = (modeInfoColumn % superblockSize) / CdefUnitModeInfoSize;
            int cdefUnitRow = (modeInfoRow % superblockSize) / CdefUnitModeInfoSize;
            int cdefStrengthIndex = frameInfo.GetCdefStrength(superblock.Position)[cdefUnitColumn + (cdefUnitRow << 1)];
            int cdefStrength = cdefStrengthIndex < 0 ? -1 : frameHeader.CdefParameters.YStrength[cdefStrengthIndex];
            int nextModeInfoRow = Math.Min(modeInfoRow + 1, frameHeader.ModeInfoRowCount - 1);
            Av1BlockModeInfo nextRowModeInfo = frameInfo.GetModeInfoAt(new Point(modeInfoColumn, nextModeInfoRow));
            Av1BlockModeInfo aboveModeInfo = frameInfo.GetModeInfoAt(new Point(modeInfoColumn, Math.Max(blockRow - 1, 0)));
            Av1BlockModeInfo leftModeInfo = frameInfo.GetModeInfoAt(new Point(Math.Max(blockColumn - 1, 0), modeInfoRow));
            string frameDescription = frameIndex < 0 ? string.Empty : $"Frame {frameIndex}, ";
            StringBuilder filmGrainCoefficientDescription = new();
            ReadOnlySpan<byte> filmGrainCoefficients = frameHeader.FilmGrainParameters.ArCoeffsYPlus128;
            int filmGrainCoefficientCount = 2 * (int)frameHeader.FilmGrainParameters.ArCoeffLag *
                ((int)frameHeader.FilmGrainParameters.ArCoeffLag + 1);

            for (int coefficientIndex = 0; coefficientIndex < filmGrainCoefficientCount; coefficientIndex++)
            {
                if (coefficientIndex != 0)
                {
                    filmGrainCoefficientDescription.Append(',');
                }

                filmGrainCoefficientDescription.Append((int)filmGrainCoefficients[coefficientIndex] - 128);
            }

            // Exact conformance failures need the owning syntax state. A coordinate alone does not distinguish
            // prediction, residual reconstruction, and in-loop filtering failures inside a large coded frame.
            Assert.Fail(
                $"{frameDescription}plane {plane} differs at ({x}, {y}): expected {expected}, actual {actual}. "
                + $"Total unequal samples={mismatchCount}:{mismatchDescription}. "
                + $"Block={modeInfo.BlockSize}, mode={modeInfo.YMode}, partition={modeInfo.PartitionType}, skip={modeInfo.Skip}, "
                + $"refs={modeInfo.ReferenceFrames[0]}/{modeInfo.ReferenceFrames[1]}, "
                + $"mvs={modeInfo.MotionVectors[0].Row},{modeInfo.MotionVectors[0].Column}/"
                + $"{modeInfo.MotionVectors[1].Row},{modeInfo.MotionVectors[1].Column}, compound={modeInfo.CompoundType}, "
                + $"filters={modeInfo.InterpolationFilters[0]}/{modeInfo.InterpolationFilters[1]}, motion={modeInfo.MotionMode}, "
                + $"filter-intra={modeInfo.UseFilterIntra}/{modeInfo.FilterIntraMode}, angle-delta={modeInfo.GetAngleDelta(plane)}, "
                + $"palette-size={modeInfo.GetPaletteSize(plane)}, transforms={modeInfo.GetTransformUnitCount(plane)}, "
                + $"transform={containingTransform.Size}/{containingTransform.Type}/coded={containingTransform.CodeBlockFlag} "
                + $"at ({containingTransform.OffsetX}, {containingTransform.OffsetY}), block-origin=({blockColumn}, {blockRow}). "
                + $"Loop-filter={frameHeader.LoopFilterParameters.FilterLevel[0]}/{frameHeader.LoopFilterParameters.FilterLevel[1]}, "
                + $"sharpness={frameHeader.LoopFilterParameters.SharpnessLevel}, delta-q={frameHeader.DeltaQParameters.IsPresent}, "
                + $"superblock-q={superblock.SuperblockQuantizerIndex}{coefficientDescription}, "
                + $"delta-lf={frameHeader.DeltaLoopFilterParameters.IsPresent}/{frameHeader.DeltaLoopFilterParameters.IsMulti}, "
                + $"CDEF={cdefStrengthIndex}/{cdefStrength}, restoration={frameHeader.LoopRestorationParameters.Items[0].Type}, "
                + $"film-grain={frameHeader.FilmGrainParameters.ApplyGrain}/"
                + $"overlap={frameHeader.FilmGrainParameters.OverlapFlag}/"
                + $"update={frameHeader.FilmGrainParameters.UpdateGrain}/seed={frameHeader.FilmGrainParameters.GrainSeed}, "
                + $"grain-points={frameHeader.FilmGrainParameters.NumYPoints}/"
                + $"{frameHeader.FilmGrainParameters.NumCbPoints}/{frameHeader.FilmGrainParameters.NumCrPoints}, "
                + $"grain-ar={frameHeader.FilmGrainParameters.ArCoeffLag}/"
                + $"{frameHeader.FilmGrainParameters.ArCoeffShiftMinus6}, "
                + $"grain-y-coefficients=[{filmGrainCoefficientDescription}], "
                + $"grain-scale={frameHeader.FilmGrainParameters.GrainScalingMinus8}/"
                + $"{frameHeader.FilmGrainParameters.GrainScaleShift}, "
                + $"tiles={frameHeader.TilesInfo.TileColumnCount}x{frameHeader.TilesInfo.TileRowCount}, "
                + $"first-tile-end=({frameHeader.TilesInfo.TileColumnStartModeInfo[1]}, {frameHeader.TilesInfo.TileRowStartModeInfo[1]}). "
                + $"Neighbors: above={aboveModeInfo.BlockSize}/{aboveModeInfo.YMode}/skip={aboveModeInfo.Skip}, "
                + $"left={leftModeInfo.BlockSize}/{leftModeInfo.YMode}/skip={leftModeInfo.Skip}, "
                + $"next-row={nextRowModeInfo.BlockSize}/{nextRowModeInfo.YMode}/skip={nextRowModeInfo.Skip}/"
                + $"angle-delta={nextRowModeInfo.GetAngleDelta(plane)}/transforms={nextRowModeInfo.GetTransformUnitCount(plane)}.");
        }
    }
}
