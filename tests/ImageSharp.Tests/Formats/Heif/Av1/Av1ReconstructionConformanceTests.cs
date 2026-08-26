// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
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
    private static void ValidateNativeFixture(
        string payloadPath,
        string referencePath,
        int width,
        int height,
        Av1BitDepth bitDepth,
        Av1ColorFormat colorFormat,
        bool requireActiveCdef)
    {
        byte[] payload = TestFile.Create(payloadPath).Bytes;
        byte[] reference = TestFile.Create(referencePath).Bytes;
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> frameBuffer = decoder.DecodeFrameBuffer(payload, null, null, out _);

        Assert.Equal(width, frameBuffer.Width);
        Assert.Equal(height, frameBuffer.Height);
        Assert.Equal(bitDepth, frameBuffer.BitDepth);
        Assert.Equal(colorFormat, frameBuffer.ColorFormat);
        Assert.NotNull(decoder.FrameHeader);
        ObuLoopFilterParameters filterParameters = decoder.FrameHeader.LoopFilterParameters;
        Assert.True(
            filterParameters.FilterLevel[0] != 0
            || filterParameters.FilterLevel[1] != 0
            || filterParameters.FilterLevelU != 0
            || filterParameters.FilterLevelV != 0);

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

        AssertNativePlanesEqual(frameBuffer, reference);
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
    private static void ValidatePresentedFixture(
        string imagePath,
        string referencePath,
        int width,
        int height,
        HeifBitDepth metadataBitDepth)
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        byte[] imageBytes = TestFile.Create(imagePath).Bytes;
        byte[] referenceBytes = TestFile.Create(referencePath).Bytes;
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
        foreach (Av1Plane plane in new[] { Av1Plane.Y, Av1Plane.U, Av1Plane.V })
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
