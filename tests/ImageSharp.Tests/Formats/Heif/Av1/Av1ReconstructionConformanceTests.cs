// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
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
    [Fact]
    public void DecodeWithActiveCdefMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePresentedFixtures, PresentationConfigurations);

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
    [Fact]
    public void DecodeProfileMatrixMatchesPinnedLibavifPresentation()
        => ValidateProfilePresentedFixtures();

    /// <summary>
    /// Verifies exact presented pixels, public bit-depth metadata, and CICP signaling under each narrower vector width
    /// and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeProfileMatrixFallbacksMatchPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateProfilePresentedFixtures, ProfileFallbackConfigurations);

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
            Assert.True((obuHeader & 0x02) != 0);

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
    [Fact]
    public void DecodeWithPaletteMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePalettePresentedFixture, PresentationConfigurations);

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
    [Fact]
    public void DecodeWithIntraBlockCopyMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateIntraBlockCopyPresentedFixtures, PresentationConfigurations);

    /// <summary>
    /// Verifies the production single-reference inter-reconstruction path against exact native and presentation
    /// references across the available vector widths and scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeProgressiveSingleReferenceMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateProgressiveSingleReferenceFixtureWithDefaultConfiguration,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies production single-reference inter reconstruction with a constrained allocator.
    /// </summary>
    [Fact]
    public void DecodeProgressiveSingleReferenceWithConstrainedAllocator()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateProgressiveSingleReferenceFixture(configuration, verifyPresentation: false);
    }

    /// <summary>
    /// Verifies that an essential lsel property returns the selected base spatial layer rather than the final
    /// progressive layer, with exact pinned-libaom native planes and pinned-libavif presentation.
    /// </summary>
    [Fact]
    public void DecodeSelectedProgressiveSpatialLayerMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSelectedProgressiveSpatialLayerWithDefaultConfiguration,
            ReconstructionConfigurations);

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
    [Fact]
    public void DecodeScaledReferenceMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateScaledReferenceFixtureWithDefaultConfiguration,
            ReconstructionConfigurations);

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

        ValidateScaledReferenceFixture(configuration, verifyPresentation: false);

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
    [Fact]
    public void DecodeRealLibavifSequenceWithEqualAverageCompoundMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateAverageCompoundSequenceWithDefaultConfiguration,
            ReconstructionConfigurations);

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

        ValidateAverageCompoundSequence(configuration, null);

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
    private static void ValidateAverageCompoundSequenceWithDefaultConfiguration()
    {
        byte[] presentationBytes = TestFile.Create(TestImages.Heif.Av1AverageCompoundSequencePresentationReference).Bytes;
        using Image<Rgba32> presentationReference = Image.Load<Rgba32>(presentationBytes);

        ValidateAverageCompoundSequence(Configuration.Default, presentationReference.Frames.RootFrame);
    }

    /// <summary>
    /// Validates the complete compound sequence with the requested allocator and optional presentation reference.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="presentationReference">The exact final presented frame, or <see langword="null"/>.</param>
    private static void ValidateAverageCompoundSequence(
        Configuration configuration,
        ImageFrame<Rgba32> presentationReference)
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
        bool presentationCompared = false;

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

                if (presentationReference is not null)
                {
                    ImageSimilarityReport<Rgba32, Rgba32> report =
                        ImageComparer.Exact.CompareImagesOrFrames(visibleFrameCount, presentationReference, frame);

                    Assert.True(report.IsEmpty, report.ToString());
                    presentationCompared = true;
                }
            }

            visibleFrameCount++;
        }

        Assert.Equal(AverageCompoundFixtureFrameCount, visibleFrameCount);
        Assert.NotEqual(0, compoundBlockCount);
        Assert.True(nativeCompared);
        Assert.Equal(presentationReference is not null, presentationCompared);
    }

    /// <summary>
    /// Verifies every selectable compound and inter-intra production branch against pinned native and presentation references.
    /// </summary>
    [Fact]
    public void DecodeRealLibavifSequencesWithSelectableCompoundAndInterIntraMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSelectableCompoundSequencesWithDefaultConfiguration,
            ReconstructionConfigurations);

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
            TestImages.Heif.Av1DistanceWeightedCompoundSequencePresentationReference,
            DistanceWeightedCompoundCoverage);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1WedgeCompoundSequenceAvif,
            TestImages.Heif.Av1WedgeCompoundSequenceNativeReference,
            TestImages.Heif.Av1WedgeCompoundSequencePresentationReference,
            WedgeCompoundCoverage | InvertedWedgeCompoundCoverage);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceAvif,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceNativeReference,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequencePresentationReference,
            DifferenceWeightedCompoundCoverage | InvertedDifferenceWeightedCompoundCoverage);

        ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1InterIntraSequenceAvif,
            TestImages.Heif.Av1InterIntraSequenceNativeReference,
            TestImages.Heif.Av1InterIntraSequencePresentationReference,
            SmoothInterIntraCoverage | WedgeInterIntraCoverage);
    }

    /// <summary>
    /// Verifies production OBMC reconstruction against pinned native and presentation references.
    /// </summary>
    [Fact]
    public void DecodeRealLibavifObmcSequenceMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateObmcSequenceWithDefaultConfiguration,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies production OBMC reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifObmcSequenceUsesContiguousPlanes()
        => ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1ObmcSequenceAvif,
            TestImages.Heif.Av1ObmcSequenceNativeReference,
            TestImages.Heif.Av1ObmcSequencePresentationReference,
            ObmcCoverage);

    /// <summary>
    /// Verifies production local warped-motion reconstruction against pinned native and presentation references.
    /// </summary>
    [Fact]
    public void DecodeRealLibavifLocalWarpSequenceMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLocalWarpSequenceWithDefaultConfiguration,
            ReconstructionConfigurations);

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
            TestImages.Heif.Av1LocalWarpSequencePresentationReference,
            LocalWarpCoverage,
            comparePresentation: false,
            fixtureSize: 256,
            visibleFrameCount: 2);

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
    [Fact]
    public void DecodeRealLibavifGlobalWarpSequenceMatchesPinnedReferences()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateGlobalWarpSequenceWithDefaultConfiguration,
            ReconstructionConfigurations);

    /// <summary>
    /// Verifies production non-translational global-motion reconstruction through a constrained allocator.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void DecodeRealLibavifGlobalWarpSequenceUsesContiguousPlanes()
        => ValidateInterPredictionSequenceWithConstrainedAllocator(
            TestImages.Heif.Av1GlobalWarpSequenceAvif,
            TestImages.Heif.Av1GlobalWarpSequenceNativeReference,
            TestImages.Heif.Av1GlobalWarpSequencePresentationReference,
            GlobalWarpCoverage,
            fixtureSize: 256,
            visibleFrameCount: 2);

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
        string presentationReferencePath,
        int requiredCoverage,
        int fixtureSize = AverageCompoundFixtureSize,
        int visibleFrameCount = AverageCompoundFixtureFrameCount)
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 1_024 };
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        ValidateInterPredictionSequence(
            configuration,
            imagePath,
            nativeReferencePath,
            presentationReferencePath,
            requiredCoverage,
            comparePresentation: false,
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
    /// Runs every selectable compound fixture with exact final presentation comparison.
    /// </summary>
    private static void ValidateSelectableCompoundSequencesWithDefaultConfiguration()
        => ValidateSelectableCompoundSequences(Configuration.Default, comparePresentation: true);

    /// <summary>
    /// Runs the OBMC sequence with exact final presentation comparison.
    /// </summary>
    private static void ValidateObmcSequenceWithDefaultConfiguration()
        => ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1ObmcSequenceAvif,
            TestImages.Heif.Av1ObmcSequenceNativeReference,
            TestImages.Heif.Av1ObmcSequencePresentationReference,
            ObmcCoverage,
            comparePresentation: true);

    /// <summary>
    /// Runs the local warped-motion sequence with exact final presentation comparison.
    /// </summary>
    private static void ValidateLocalWarpSequenceWithDefaultConfiguration()
        => ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1LocalWarpSequenceAvif,
            TestImages.Heif.Av1LocalWarpSequenceNativeReference,
            TestImages.Heif.Av1LocalWarpSequencePresentationReference,
            LocalWarpCoverage,
            comparePresentation: true,
            fixtureSize: 256,
            visibleFrameCount: 2);

    /// <summary>
    /// Runs the non-translational global-motion sequence with exact final presentation comparison.
    /// </summary>
    private static void ValidateGlobalWarpSequenceWithDefaultConfiguration()
        => ValidateInterPredictionSequence(
            Configuration.Default,
            TestImages.Heif.Av1GlobalWarpSequenceAvif,
            TestImages.Heif.Av1GlobalWarpSequenceNativeReference,
            TestImages.Heif.Av1GlobalWarpSequencePresentationReference,
            GlobalWarpCoverage,
            comparePresentation: true,
            fixtureSize: 256,
            visibleFrameCount: 2);

    /// <summary>
    /// Validates every selectable compound fixture with the requested decoder configuration.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="comparePresentation">Whether to compare the final presented frame.</param>
    private static void ValidateSelectableCompoundSequences(Configuration configuration, bool comparePresentation)
    {
        ValidateInterPredictionSequence(
            configuration,
            TestImages.Heif.Av1DistanceWeightedCompoundSequenceAvif,
            TestImages.Heif.Av1DistanceWeightedCompoundSequenceNativeReference,
            TestImages.Heif.Av1DistanceWeightedCompoundSequencePresentationReference,
            DistanceWeightedCompoundCoverage,
            comparePresentation);

        ValidateInterPredictionSequence(
            configuration,
            TestImages.Heif.Av1WedgeCompoundSequenceAvif,
            TestImages.Heif.Av1WedgeCompoundSequenceNativeReference,
            TestImages.Heif.Av1WedgeCompoundSequencePresentationReference,
            WedgeCompoundCoverage | InvertedWedgeCompoundCoverage,
            comparePresentation);

        ValidateInterPredictionSequence(
            configuration,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceAvif,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequenceNativeReference,
            TestImages.Heif.Av1DifferenceWeightedCompoundSequencePresentationReference,
            DifferenceWeightedCompoundCoverage | InvertedDifferenceWeightedCompoundCoverage,
            comparePresentation);

        ValidateInterPredictionSequence(
            configuration,
            TestImages.Heif.Av1InterIntraSequenceAvif,
            TestImages.Heif.Av1InterIntraSequenceNativeReference,
            TestImages.Heif.Av1InterIntraSequencePresentationReference,
            SmoothInterIntraCoverage | WedgeInterIntraCoverage,
            comparePresentation);
    }

    /// <summary>
    /// Decodes one complete retained-reference sequence and compares its final native and presented samples exactly.
    /// </summary>
    private static void ValidateInterPredictionSequence(
        Configuration configuration,
        string imagePath,
        string nativeReferencePath,
        string presentationReferencePath,
        int requiredCoverage,
        bool comparePresentation,
        int fixtureSize = AverageCompoundFixtureSize,
        int visibleFrameCount = AverageCompoundFixtureFrameCount)
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

        using Image<Rgba32> presentationReference =
            Image.Load<Rgba32>(TestFile.Create(presentationReferencePath).Bytes);

        HeifSequence sequence = ParseImageSequence(fileBytes);
        HeifSequenceTrack track = sequence.ColorTrack;
        int coverage = 0;
        int decodedVisibleFrameCount = 0;
        bool nativeCompared = false;
        bool presentationCompared = false;

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

                if (comparePresentation)
                {
                    ImageSimilarityReport<Rgba32, Rgba32> report =
                        ImageComparer.Exact.CompareImagesOrFrames(
                            decodedVisibleFrameCount,
                            presentationReference.Frames.RootFrame,
                            frame);

                    Assert.True(report.IsEmpty, report.ToString());
                    presentationCompared = true;
                }
            }

            decodedVisibleFrameCount++;
        }

        Assert.Equal(visibleFrameCount, decodedVisibleFrameCount);
        Assert.Equal(requiredCoverage, coverage & requiredCoverage);
        Assert.True(nativeCompared);
        Assert.Equal(comparePresentation, presentationCompared);
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
    [Fact]
    public void DecodeLosslessMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateLosslessPresentedFixtures, PresentationConfigurations);

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
    [Fact]
    public void DecodeWithSuperResolutionMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSuperResolutionPresentedFixtures, PresentationConfigurations);

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
    [Fact]
    public void DecodeWithLoopRestorationMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateRestorationPresentedFixtures, PresentationConfigurations);

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
    /// Validates every presented profile fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateProfilePresentedFixtures()
    {
        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile8BitMonochromeAvif,
            TestImages.Heif.Av1Profile8BitMonochromePresentationReference,
            HeifBitDepth.Bit8);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile8Bit420Avif,
            TestImages.Heif.Av1Profile8Bit420PresentationReference,
            HeifBitDepth.Bit8);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile8Bit422Avif,
            TestImages.Heif.Av1Profile8Bit422PresentationReference,
            HeifBitDepth.Bit8);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile8Bit444Avif,
            TestImages.Heif.Av1Profile8Bit444PresentationReference,
            HeifBitDepth.Bit8);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile10BitMonochromeAvif,
            TestImages.Heif.Av1Profile10BitMonochromePresentationReference,
            HeifBitDepth.Bit10);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile10Bit420Avif,
            TestImages.Heif.Av1Profile10Bit420PresentationReference,
            HeifBitDepth.Bit10);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile10Bit422Avif,
            TestImages.Heif.Av1Profile10Bit422PresentationReference,
            HeifBitDepth.Bit10);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile10Bit444Avif,
            TestImages.Heif.Av1Profile10Bit444PresentationReference,
            HeifBitDepth.Bit10);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile12BitMonochromeAvif,
            TestImages.Heif.Av1Profile12BitMonochromePresentationReference,
            HeifBitDepth.Bit12);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile12Bit420Avif,
            TestImages.Heif.Av1Profile12Bit420PresentationReference,
            HeifBitDepth.Bit12);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile12Bit422Avif,
            TestImages.Heif.Av1Profile12Bit422PresentationReference,
            HeifBitDepth.Bit12);

        ValidateProfilePresentedFixture(
            TestImages.Heif.Av1Profile12Bit444Avif,
            TestImages.Heif.Av1Profile12Bit444PresentationReference,
            HeifBitDepth.Bit12);
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
    /// Validates the exact public presentation and metadata of one independently encoded AVIF profile fixture.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="referencePath">The eight-bit RGBA output produced by the pinned scalar libavif decoder.</param>
    /// <param name="metadataBitDepth">The expected public HEIF sample precision.</param>
    private static void ValidateProfilePresentedFixture(string imagePath, string referencePath, HeifBitDepth metadataBitDepth)
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;
        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);
        using Image<Rgba32> reference = Image.Load<Rgba32>(referenceBytes);

        Assert.Equal(ProfileFixtureWidth, image.Width);
        Assert.Equal(ProfileFixtureHeight, image.Height);
        Assert.Single(image.Frames);

        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(metadataBitDepth, metadata.BitDepth);

        CicpProfile colorProfile = Assert.IsType<CicpProfile>(image.Metadata.CicpProfile);
        Assert.Equal(CicpColorPrimaries.ItuRBt709_6, colorProfile.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.Iec61966_2_1, colorProfile.TransferCharacteristics);
        Assert.Equal(CicpMatrixCoefficients.ItuRBt601_7_525, colorProfile.MatrixCoefficients);
        Assert.True(colorProfile.FullRange);
        ImageComparer.Exact.VerifySimilarity(reference, image);
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
    /// Validates every active-CDEF presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidatePresentedFixtures()
    {
        ValidatePresentedFixture(
            TestImages.Heif.Av1Cdef8BitAvif,
            TestImages.Heif.Av1Cdef8BitPresentationReference,
            768,
            512,
            HeifBitDepth.Bit8);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Cdef10BitAvif,
            TestImages.Heif.Av1Cdef10BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit10);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Cdef12BitAvif,
            TestImages.Heif.Av1Cdef12BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit12);
    }

    /// <summary>
    /// Validates the active-palette presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidatePalettePresentedFixture()
        => ValidatePresentedFixture(
            TestImages.Heif.Av1Palette8BitAvif,
            TestImages.Heif.Av1Palette8BitPresentationReference,
            33,
            11,
            HeifBitDepth.Bit8,
            requirePalette: true);

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
    /// Validates every active intra-block-copy presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateIntraBlockCopyPresentedFixtures()
    {
        ValidatePresentedFixture(
            TestImages.Heif.Av1IntraBlockCopy8BitAvif,
            TestImages.Heif.Av1IntraBlockCopy8BitPresentationReference,
            512,
            256,
            HeifBitDepth.Bit8,
            requireIntraBlockCopy: true);

        ValidatePresentedFixture(
            TestImages.Heif.Av1IntraBlockCopy10BitAvif,
            TestImages.Heif.Av1IntraBlockCopy10BitPresentationReference,
            512,
            256,
            HeifBitDepth.Bit10,
            requireIntraBlockCopy: true);

        ValidatePresentedFixture(
            TestImages.Heif.Av1IntraBlockCopy12BitAvif,
            TestImages.Heif.Av1IntraBlockCopy12BitPresentationReference,
            512,
            256,
            HeifBitDepth.Bit12,
            requireIntraBlockCopy: true);
    }

    /// <summary>
    /// Runs the exact final-layer native and presentation comparisons with the default configuration.
    /// </summary>
    private static void ValidateProgressiveSingleReferenceFixtureWithDefaultConfiguration()
        => ValidateProgressiveSingleReferenceFixture(Configuration.Default, verifyPresentation: true);

    /// <summary>
    /// Runs the selected-spatial-layer native and presentation comparisons with the default configuration.
    /// </summary>
    private static void ValidateSelectedProgressiveSpatialLayerWithDefaultConfiguration()
        => ValidateSelectedProgressiveSpatialLayer(Configuration.Default);

    /// <summary>
    /// Runs the exact scaled-reference native and presentation comparison with the default configuration.
    /// </summary>
    private static void ValidateScaledReferenceFixtureWithDefaultConfiguration()
        => ValidateScaledReferenceFixture(Configuration.Default, verifyPresentation: true);

    /// <summary>
    /// Verifies the genuine size-changing layered fixture with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="verifyPresentation">Whether to verify the public RGBA presentation.</param>
    private static void ValidateScaledReferenceFixture(Configuration configuration, bool verifyPresentation)
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

        if (!verifyPresentation)
        {
            return;
        }

        DecoderOptions options = new() { Configuration = configuration, MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(TestImages.Heif.Av1ScaledReferenceAvif).Bytes;
        byte[] presentationBytes = TestFile.Create(TestImages.Heif.Av1ScaledReferencePresentationReference).Bytes;
        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);
        using Image<Rgba32> presentationReference = Image.Load<Rgba32>(presentationBytes);

        Assert.Equal(ScaledReferenceFixtureSize, image.Width);
        Assert.Equal(ScaledReferenceFixtureSize, image.Height);
        Assert.Single(image.Frames);
        Assert.Equal(HeifBitDepth.Bit8, image.Metadata.GetHeifMetadata().BitDepth);
        ImageComparer.Exact.VerifySimilarity(presentationReference, image);
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

        DecoderOptions options = new() { Configuration = configuration, MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(TestImages.Heif.Av1ScaledReferenceSelectedLayerAvif).Bytes;
        byte[] presentationBytes =
            TestFile.Create(TestImages.Heif.Av1ScaledReferenceSelectedLayerPresentationReference).Bytes;

        byte[] finalPresentationBytes =
            TestFile.Create(TestImages.Heif.Av1ScaledReferencePresentationReference).Bytes;

        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);
        using Image<Rgba32> presentationReference = Image.Load<Rgba32>(presentationBytes);
        using Image<Rgba32> finalPresentationReference = Image.Load<Rgba32>(finalPresentationBytes);

        // HEIF presents a selected lower-resolution spatial layer at the item's ispe extent. Exact comparison with
        // libavif therefore proves both layer selection and the required 40x40-to-80x80 presentation scaling.
        Assert.Equal(ScaledReferenceFixtureSize, image.Width);
        Assert.Equal(ScaledReferenceFixtureSize, image.Height);
        Assert.Single(image.Frames);
        Assert.Equal(HeifBitDepth.Bit8, image.Metadata.GetHeifMetadata().BitDepth);
        ImageComparer.Exact.VerifySimilarity(presentationReference, image);
        Assert.NotEmpty(ImageComparer.Exact.CompareImages(finalPresentationReference, image));
    }

    /// <summary>
    /// Verifies the final dependent layer with the requested allocator.
    /// </summary>
    /// <param name="configuration">The decoder configuration.</param>
    /// <param name="verifyPresentation">Whether to verify the final public RGBA presentation.</param>
    private static void ValidateProgressiveSingleReferenceFixture(
        Configuration configuration,
        bool verifyPresentation)
    {
        byte[] payload = TestFile.Create(TestImages.Heif.Av1Progressive8BitPayload).Bytes;
        byte[] referenceBytes = TestFile.Create(TestImages.Heif.Av1Progressive8BitReference).Bytes;
        ReadOnlySpan<byte> fileHeader =
            "YUV4MPEG2 W33 H11 F25:1 Ip A0:0 C444alpha XYSCSS=444 XCOLORRANGE=FULL\n"u8;

        ReadOnlySpan<byte> frameHeader = "FRAME\n"u8;

        int planeSampleCount = ProgressiveFixtureWidth * ProgressiveFixtureHeight;
        int frameSampleCount = planeSampleCount * 4;

        // The pinned reference contains both progressive YUV444-alpha outputs in decode order. Select the second frame
        // so this assertion cannot pass by comparing only the independently decodable base layer.
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

        if (!verifyPresentation)
        {
            return;
        }

        DecoderOptions options = new() { Configuration = configuration, MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(TestImages.Heif.Av1Progressive8BitAvif).Bytes;
        byte[] presentationBytes = TestFile.Create(TestImages.Heif.Av1Progressive8BitPresentationReference).Bytes;
        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);
        using Image<Rgba32> presentationReference = Image.Load<Rgba32>(presentationBytes);

        Assert.Equal(ProgressiveFixtureWidth, image.Width);
        Assert.Equal(ProgressiveFixtureHeight, image.Height);
        Assert.Single(image.Frames);
        Assert.Equal(HeifBitDepth.Bit8, image.Metadata.GetHeifMetadata().BitDepth);
        ImageComparer.Exact.VerifySimilarity(presentationReference, image);
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
    /// Validates every lossless presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateLosslessPresentedFixtures()
    {
        ValidatePresentedFixture(
            TestImages.Heif.Av1Lossless8BitAvif,
            TestImages.Heif.Av1Lossless8BitPresentationReference,
            LosslessFixtureWidth,
            LosslessFixtureHeight,
            HeifBitDepth.Bit8);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Lossless10BitAvif,
            TestImages.Heif.Av1Lossless10BitPresentationReference,
            LosslessFixtureWidth,
            LosslessFixtureHeight,
            HeifBitDepth.Bit10);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Lossless12BitAvif,
            TestImages.Heif.Av1Lossless12BitPresentationReference,
            LosslessFixtureWidth,
            LosslessFixtureHeight,
            HeifBitDepth.Bit12);
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
    /// Validates every active-super-resolution presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateSuperResolutionPresentedFixtures()
    {
        ValidatePresentedFixture(
            TestImages.Heif.Av1SuperResolution8BitAvif,
            TestImages.Heif.Av1SuperResolution8BitPresentationReference,
            768,
            512,
            HeifBitDepth.Bit8,
            requireSuperResolution: true);

        ValidatePresentedFixture(
            TestImages.Heif.Av1SuperResolution10BitAvif,
            TestImages.Heif.Av1SuperResolution10BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit10,
            requireSuperResolution: true);

        ValidatePresentedFixture(
            TestImages.Heif.Av1SuperResolution12BitAvif,
            TestImages.Heif.Av1SuperResolution12BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit12,
            requireSuperResolution: true);
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
    /// Validates every active-restoration presentation fixture under the hardware configuration selected by
    /// <see cref="FeatureTestRunner"/>.
    /// </summary>
    private static void ValidateRestorationPresentedFixtures()
    {
        ValidatePresentedFixture(
            TestImages.Heif.Av1Restoration8BitAvif,
            TestImages.Heif.Av1Restoration8BitPresentationReference,
            768,
            512,
            HeifBitDepth.Bit8);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Restoration10BitAvif,
            TestImages.Heif.Av1Restoration10BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit10);

        ValidatePresentedFixture(
            TestImages.Heif.Av1Restoration12BitAvif,
            TestImages.Heif.Av1Restoration12BitPresentationReference,
            1024,
            428,
            HeifBitDepth.Bit12);
    }

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
    /// Validates the exact public presentation of one independently encoded AVIF image against pinned scalar-libavif output.
    /// </summary>
    /// <param name="imagePath">The complete AVIF container.</param>
    /// <param name="referencePath">The eight-bit RGBA output produced by the pinned scalar libavif decoder.</param>
    /// <param name="width">The expected displayed width.</param>
    /// <param name="height">The expected displayed height.</param>
    /// <param name="metadataBitDepth">The expected public HEIF sample precision.</param>
    /// <param name="requireSuperResolution">Whether the AV1 item must upscale from a narrower coded frame.</param>
    /// <param name="requirePalette">Whether the AV1 item must select palette prediction for luma and chroma.</param>
    /// <param name="requireIntraBlockCopy">Whether the AV1 item must select intra-block-copy prediction.</param>
    private static void ValidatePresentedFixture(
        string imagePath,
        string referencePath,
        int width,
        int height,
        HeifBitDepth metadataBitDepth,
        bool requireSuperResolution = false,
        bool requirePalette = false,
        bool requireIntraBlockCopy = false)
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;

        if (requireSuperResolution)
        {
            AssertUsesSuperResolution(imageBytes);
        }

        if (requirePalette)
        {
            AssertUsesPalette(imageBytes);
        }

        if (requireIntraBlockCopy)
        {
            AssertUsesIntraBlockCopy(imageBytes);
        }

        using Image<Rgba32> image = Image.Load<Rgba32>(options, imageBytes);
        using Image<Rgba32> reference = Image.Load<Rgba32>(referenceBytes);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Single(image.Frames);
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        Assert.Equal(HeifCompressionMethod.Av1, metadata.CompressionMethod);
        Assert.Equal(metadataBitDepth, metadata.BitDepth);
        ImageComparer.Exact.VerifySimilarity(reference, image);
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
    /// <param name="reference">The planar Y, U, and V samples produced by the pinned libaom decoder.</param>
    private static void AssertNativePlanesEqual(Av1Decoder decoder, Av1FrameBuffer<byte> frameBuffer, ReadOnlySpan<byte> reference)
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
                                mismatchDescription.Append($" {plane}({x},{y})={expectedRow[x]}/{actualRow[x]}");
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
                                mismatchDescription.Append($" {plane}({x},{y})={expected}/{actualRow[x]}");
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
            mismatchDescription?.ToString() ?? string.Empty);
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
    private static void AssertSampleEqual(
        Av1Decoder decoder,
        Av1Plane plane,
        int x,
        int y,
        ushort expected,
        ushort actual,
        int mismatchCount,
        string mismatchDescription)
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
                coefficientDescription.Append($", quantized-coefficients={coefficientCount}:[");
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

            // Exact conformance failures need the owning syntax state. A coordinate alone does not distinguish
            // prediction, residual reconstruction, and in-loop filtering failures inside a large coded frame.
            Assert.Fail(
                $"Plane {plane} differs at ({x}, {y}): expected {expected}, actual {actual}. "
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
                + $"film-grain={frameHeader.FilmGrainParameters.ApplyGrain}, "
                + $"tiles={frameHeader.TilesInfo.TileColumnCount}x{frameHeader.TilesInfo.TileRowCount}, "
                + $"first-tile-end=({frameHeader.TilesInfo.TileColumnStartModeInfo[1]}, {frameHeader.TilesInfo.TileRowStartModeInfo[1]}). "
                + $"Neighbors: above={aboveModeInfo.BlockSize}/{aboveModeInfo.YMode}/skip={aboveModeInfo.Skip}, "
                + $"left={leftModeInfo.BlockSize}/{leftModeInfo.YMode}/skip={leftModeInfo.Skip}, "
                + $"next-row={nextRowModeInfo.BlockSize}/{nextRowModeInfo.YMode}/skip={nextRowModeInfo.Skip}/"
                + $"angle-delta={nextRowModeInfo.GetAngleDelta(plane)}/transforms={nextRowModeInfo.GetTransformUnitCount(plane)}.");
        }
    }
}
