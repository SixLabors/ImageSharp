// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
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
    /// Verifies decoded luma and chroma palette syntax and exact native samples against scalar libaom for an
    /// independently encoded AV1 still-picture stream.
    /// </summary>
    [Fact]
    public void DecodeWithPaletteMatchesPinnedLibaomReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePaletteNativeFixture, PaletteConfigurations);

    /// <summary>
    /// Verifies decoded luma and chroma palette syntax and exact presented pixels for an independently encoded AVIF
    /// image across the available vector widths and the scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeWithPaletteMatchesPinnedLibavifPresentation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePalettePresentedFixture, PresentationConfigurations);

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
        AssertNativePlanesEqual(frameBuffer, nativeReference);
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

        AssertNativePlanesEqual(frameBuffer, reference);
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

        AssertNativePlanesEqual(frameBuffer, nativeReference);
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
    /// <param name="frameBuffer">The reconstructed AV1 component planes.</param>
    /// <param name="reference">The planar Y, U, and V samples produced by the pinned libaom decoder.</param>
    private static void AssertNativePlanesEqual(Av1FrameBuffer<byte> frameBuffer, ReadOnlySpan<byte> reference)
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
                        AssertSampleEqual(plane, x, y, expectedRow[x], actualRow[x]);
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
                        AssertSampleEqual(plane, x, y, expected, actualRow[x]);
                        referenceOffset += sizeof(ushort);
                    }
                }
            }
        }

        Assert.Equal(reference.Length, referenceOffset);
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
    /// <param name="plane">The compared component plane.</param>
    /// <param name="x">The sample X coordinate.</param>
    /// <param name="y">The sample Y coordinate.</param>
    /// <param name="expected">The reference sample.</param>
    /// <param name="actual">The reconstructed sample.</param>
    private static void AssertSampleEqual(Av1Plane plane, int x, int y, ushort expected, ushort actual)
    {
        if (expected != actual)
        {
            Assert.Fail($"Plane {plane} differs at ({x}, {y}): expected {expected}, actual {actual}.");
        }
    }
}
