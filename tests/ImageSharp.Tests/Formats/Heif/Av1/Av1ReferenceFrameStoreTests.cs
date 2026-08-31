// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 frame ownership, padded-buffer copying, and reference-border preservation.
/// </summary>
[Trait("Format", "Avif")]
[ValidateDisposedMemoryAllocations]
public class Av1ReferenceFrameStoreTests
{
    /// <summary>
    /// Verifies that a zero refresh mask leaves the slot map and caller ownership unchanged.
    /// </summary>
    [Fact]
    public void ApplyZeroRefreshMaskPreservesCallerOwnership()
    {
        using Av1ReferenceFrameStore store = new();
        using Av1ReferenceFrame frame = CreateFrame();

        Assert.False(store.Commit(0, frame, showFrame: false));
        Assert.Null(store.Resolve(0));
        Assert.NotNull(frame.FrameBuffer.BufferY);
    }

    /// <summary>
    /// Verifies that every selected slot shares the one transferred frame owner.
    /// </summary>
    [Fact]
    public void ApplyRefreshMaskStoresFrameInEverySelectedSlot()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame frame = CreateFrame();

        Assert.True(store.Commit(0b1000_0101, frame, showFrame: false));
        Assert.Same(frame, store.Resolve(0));
        Assert.Null(store.Resolve(1));
        Assert.Same(frame, store.Resolve(2));
        Assert.Same(frame, store.Resolve(7));
    }

    /// <summary>
    /// Verifies that a shown frame transfers to presentation ownership even when it refreshes no reference slot.
    /// </summary>
    [Fact]
    public void ShownFrameWithZeroRefreshMaskTransfersOwnership()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame frame = CreateFrame();

        Assert.True(store.Commit(0, frame, showFrame: true));
        Assert.Same(frame, store.OutputFrame);
        Assert.Null(store.Resolve(0));
    }

    /// <summary>
    /// Verifies that replacing the shown output does not release its predecessor while a reference slot retains it.
    /// </summary>
    [Fact]
    public void ReplacingOutputPreservesReferencedPredecessor()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame firstOutput = CreateFrame();
        Av1ReferenceFrame secondOutput = CreateFrame();
        Av1ReferenceFrame replacementReference = CreateFrame();
        Av1FrameBuffer<byte> firstOutputBuffer = firstOutput.FrameBuffer;
        store.Commit(0b0000_0001, firstOutput, showFrame: true);

        store.Commit(0, secondOutput, showFrame: true);

        Assert.NotNull(firstOutputBuffer.BufferY);
        Assert.Same(firstOutput, store.Resolve(0));

        store.Commit(0b0000_0001, replacementReference, showFrame: false);

        Assert.Null(firstOutputBuffer.BufferY);
    }

    /// <summary>
    /// Verifies that an independently committed presentation output does not release the ungrained owner retained by a reference slot.
    /// </summary>
    [Fact]
    public void CommitOutputPreservesReferencedPreviousOutput()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame retainedReference = CreateFrame();
        Av1ReferenceFrame presentation = CreateFrame();
        Av1ReferenceFrame nextPresentation = CreateFrame();
        Av1ReferenceFrame replacementReference = CreateFrame();
        Av1FrameBuffer<byte> retainedReferenceBuffer = retainedReference.FrameBuffer;
        Av1FrameBuffer<byte> presentationBuffer = presentation.FrameBuffer;
        store.Commit(0b0000_0001, retainedReference, showFrame: true);

        store.CommitOutput(presentation);

        Assert.Same(presentation, store.OutputFrame);
        Assert.Same(retainedReference, store.Resolve(0));
        Assert.NotNull(retainedReferenceBuffer.BufferY);

        store.CommitOutput(nextPresentation);

        Assert.Same(nextPresentation, store.OutputFrame);
        Assert.Null(presentationBuffer.BufferY);
        Assert.NotNull(retainedReferenceBuffer.BufferY);

        store.Commit(0b0000_0001, replacementReference, showFrame: false);

        Assert.Null(retainedReferenceBuffer.BufferY);
    }

    /// <summary>
    /// Verifies that replacing one alias does not release a frame retained by another slot.
    /// </summary>
    [Fact]
    public void PartialReplacementPreservesSharedOwner()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame sharedFrame = CreateFrame();
        Av1ReferenceFrame replacement = CreateFrame();
        store.Commit(0b0000_0011, sharedFrame, showFrame: false);

        store.Commit(0b0000_0001, replacement, showFrame: false);

        Assert.Same(replacement, store.Resolve(0));
        Assert.Same(sharedFrame, store.Resolve(1));
        Assert.NotNull(sharedFrame.FrameBuffer.BufferY);
    }

    /// <summary>
    /// Verifies that replacing the final alias releases the displaced frame planes.
    /// </summary>
    [Fact]
    public void FinalReplacementReleasesDisplacedOwner()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame displacedFrame = CreateFrame();
        Av1ReferenceFrame firstReplacement = CreateFrame();
        Av1ReferenceFrame secondReplacement = CreateFrame();
        Av1FrameBuffer<byte> displacedBuffer = displacedFrame.FrameBuffer;
        store.Commit(0b0000_0011, displacedFrame, showFrame: false);
        store.Commit(0b0000_0001, firstReplacement, showFrame: false);

        store.Commit(0b0000_0010, secondReplacement, showFrame: false);

        Assert.Null(displacedBuffer.BufferY);
        Assert.Same(firstReplacement, store.Resolve(0));
        Assert.Same(secondReplacement, store.Resolve(1));
    }

    /// <summary>
    /// Verifies that resetting the map releases one multiply referenced owner and clears every slot.
    /// </summary>
    [Fact]
    public void ResetReleasesUniqueOwnersAndClearsSlots()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame frame = CreateFrame();
        Av1FrameBuffer<byte> frameBuffer = frame.FrameBuffer;
        store.Commit(byte.MaxValue, frame, showFrame: false);

        store.Reset();

        Assert.Null(frameBuffer.BufferY);
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            Assert.Null(store.Resolve(slot));
        }
    }

    /// <summary>
    /// Verifies that taking the final output transfers its planes without copying and releases unrelated references.
    /// </summary>
    [Fact]
    public void TakeOutputTransfersPlanesAndReleasesOtherReferences()
    {
        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame reference = CreateFrame();
        Av1ReferenceFrame output = CreateFrame();
        Av1FrameBuffer<byte> referenceBuffer = reference.FrameBuffer;
        store.Commit(0b0000_0001, reference, showFrame: false);
        store.Commit(0b0000_0010, output, showFrame: true);

        using Av1ReferenceFrame selectedOutput = store.TakeOutput();
        using Av1FrameBuffer<byte> frameBuffer = selectedOutput.TakeFrameBuffer();

        Assert.Same(output, selectedOutput);
        Assert.Null(referenceBuffer.BufferY);
        Assert.NotNull(frameBuffer.BufferY);
        Assert.Null(store.OutputFrame);
        Assert.Null(store.Resolve(0));
        Assert.Null(store.Resolve(1));
    }

    /// <summary>
    /// Verifies motion-field ownership across frame initialization, reference aliases, shown output, and final disposal.
    /// </summary>
    [Fact]
    public void MotionFieldsFollowAliasesAndPresentationOwnership()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(64, 64, Av1BitDepth.EightBit, true, false, false);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = true;

        using Av1ReferenceFrameStore sourceReferences = new();
        using Av1FrameInfo sourceFrameInfo = new(sequenceHeader);
        ObuFrameHeader sourceHeader = new()
        {
            FrameType = ObuFrameType.KeyFrame,
            ShowFrame = true,
            OrderHint = 0,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16
        };

        Av1ReferenceFrame sourceFrame = new(
            new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false),
            sourceHeader,
            sourceFrameInfo);

        Assert.True(sourceReferences.Commit(byte.MaxValue, sourceFrame, showFrame: false));

        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            OrderHint = 1,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16,
            UseReferenceFrameMotionVectors = true
        };

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        frameInfo.InitializeMotionField(configuration, sequenceHeader, frameHeader, sourceReferences);

        Assert.Equal(2, allocator.AllocationLog.Count);
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "RetainedMotionFieldEntry");
        Assert.Contains(allocator.AllocationLog, request => request.ElementType.Name == "TemporalMotionFieldEntry");

        using Av1ReferenceFrameStore store = new();
        Av1ReferenceFrame frame = new(
            new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false),
            frameHeader,
            frameInfo);

        Assert.True(store.Commit(byte.MaxValue, frame, showFrame: true));

        // The reference frame owns the shared FrameInfo after the tile-reader lease ends. Physical reference slots
        // and the shown-output pointer are aliases of that owner and must not release either motion field early.
        frameInfo.Dispose();
        Assert.Empty(allocator.ReturnLog);

        Av1ReferenceFrame output = store.TakeOutput();
        Assert.Empty(allocator.ReturnLog);

        output.Dispose();
        output.Dispose();
        store.Dispose();

        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));

        Assert.Equal(2, allocator.ReturnLog.Count);
    }

    /// <summary>
    /// Verifies that tile-reader construction unwinds every successful allocation when temporal-field allocation fails.
    /// </summary>
    [Fact]
    public void MotionFieldAllocationFailureUnwindsTileReaderOwnership()
    {
        FailingTemporalMotionFieldAllocator allocator = new();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(64, 64, Av1BitDepth.EightBit, true, false, false);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.EnableReferenceFrameMotionVectors = true;

        using Av1ReferenceFrameStore referenceFrames = new();
        using Av1FrameInfo retainedFrameInfo = new(sequenceHeader);
        ObuFrameHeader retainedHeader = new()
        {
            FrameType = ObuFrameType.KeyFrame,
            ShowFrame = true,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16
        };

        Av1ReferenceFrame retainedFrame = new(
            new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false),
            retainedHeader,
            retainedFrameInfo);

        Assert.True(referenceFrames.Commit(byte.MaxValue, retainedFrame, showFrame: false));

        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            OrderHint = 1,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16,
            UseReferenceFrameMotionVectors = true
        };

        Av1FrameEntropyContexts entropyContexts = new(0);

        Assert.Throws<InvalidMemoryOperationException>(
            () => new Av1TileReader(
                configuration,
                sequenceHeader,
                frameHeader,
                entropyContexts,
                null,
                referenceFrames));

        Assert.NotEmpty(allocator.AllocationLog);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
    }

    /// <summary>
    /// Verifies that an eight-bit presentation copy contains every byte of each padded plane and the complete active geometry.
    /// </summary>
    [Fact]
    public void CopyToCopiesCompletePaddedEightBitFrame()
        => ValidateCompleteFrameCopy(Av1BitDepth.EightBit);

    /// <summary>
    /// Verifies that a high-bit-depth presentation copy contains every native sample of each padded plane and the complete active geometry.
    /// </summary>
    [Fact]
    public void CopyToCopiesCompletePaddedHighBitDepthFrame()
        => ValidateCompleteFrameCopy(Av1BitDepth.TwelveBit);

    /// <summary>
    /// Verifies that luma and subsampled chroma allocations cover the greatest legal unscaled UMV prediction extent.
    /// </summary>
    [Fact]
    public void PaddedPlanesCoverMaximumUnscaledMotionVectorExtent()
    {
        const int maximumLumaExtent = 135;
        const int maximumSubsampledExtent = 71;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(128, 128, Av1BitDepth.EightBit, false, true, true);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);

        Span<byte> luma = frameBuffer.GetPaddedPlaneSpan(Av1Plane.Y, 0, 0, out int lumaStride, out Point lumaOrigin);
        int lumaHeight = luma.Length / lumaStride;

        Assert.True(lumaOrigin.X >= maximumLumaExtent);
        Assert.True(lumaOrigin.Y >= maximumLumaExtent);
        Assert.True(lumaStride - lumaOrigin.X - frameBuffer.Width >= maximumLumaExtent);
        Assert.True(lumaHeight - lumaOrigin.Y - frameBuffer.Height >= maximumLumaExtent);

        Span<byte> chroma = frameBuffer.GetPaddedPlaneSpan(Av1Plane.U, 1, 1, out int chromaStride, out Point chromaOrigin);
        int chromaWidth = Av1Math.DivideLog2Ceiling(frameBuffer.Width, 1);
        int chromaHeight = Av1Math.DivideLog2Ceiling(frameBuffer.Height, 1);
        int chromaAllocationHeight = chroma.Length / chromaStride;

        Assert.True(chromaOrigin.X >= maximumSubsampledExtent);
        Assert.True(chromaOrigin.Y >= maximumSubsampledExtent);
        Assert.True(chromaStride - chromaOrigin.X - chromaWidth >= maximumSubsampledExtent);
        Assert.True(chromaAllocationHeight - chromaOrigin.Y - chromaHeight >= maximumSubsampledExtent);
    }

    /// <summary>
    /// Verifies that AV1 reference-border extension repeats the nearest visible edge across every allocated plane sample.
    /// </summary>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma.</param>
    /// <param name="subsamplingX">Whether chroma is horizontally subsampled.</param>
    /// <param name="subsamplingY">Whether chroma is vertically subsampled.</param>
    [Theory]
    [InlineData(Av1BitDepth.EightBit, true, false, false)]
    [InlineData(Av1BitDepth.EightBit, false, true, true)]
    [InlineData(Av1BitDepth.TenBit, true, false, false)]
    [InlineData(Av1BitDepth.TenBit, false, true, false)]
    [InlineData(Av1BitDepth.TwelveBit, true, false, false)]
    [InlineData(Av1BitDepth.TwelveBit, false, true, true)]
    public void ExtendRepeatsVisibleEdgesAcrossCompletePadding(
        int bitDepth,
        bool isMonochrome,
        bool subsamplingX,
        bool subsamplingY)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            5,
            3,
            (Av1BitDepth)bitDepth,
            isMonochrome,
            subsamplingX,
            subsamplingY);

        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            sequenceHeader,
            sequenceHeader.ColorConfig.GetColorFormat(),
            false);

        InitializeVisiblePlane(
            frameBuffer,
            frameBuffer.BufferY!,
            frameBuffer.OriginX,
            frameBuffer.OriginY,
            frameBuffer.Width,
            frameBuffer.Height,
            0);

        if (!isMonochrome)
        {
            int subX = subsamplingX ? 1 : 0;
            int subY = subsamplingY ? 1 : 0;
            int chromaOriginX = frameBuffer.OriginX >> subX;
            int chromaOriginY = frameBuffer.OriginY >> subY;
            int chromaWidth = Av1Math.DivideLog2Ceiling(frameBuffer.Width, subX);
            int chromaHeight = Av1Math.DivideLog2Ceiling(frameBuffer.Height, subY);

            InitializeVisiblePlane(frameBuffer, frameBuffer.BufferCb!, chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 1);
            InitializeVisiblePlane(frameBuffer, frameBuffer.BufferCr!, chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 2);
        }

        Av1ReferenceFrameBorder.Extend(frameBuffer);

        AssertExtendedPlane(
            frameBuffer,
            frameBuffer.BufferY!,
            frameBuffer.OriginX,
            frameBuffer.OriginY,
            frameBuffer.Width,
            frameBuffer.Height,
            0);

        if (!isMonochrome)
        {
            int subX = subsamplingX ? 1 : 0;
            int subY = subsamplingY ? 1 : 0;
            int chromaOriginX = frameBuffer.OriginX >> subX;
            int chromaOriginY = frameBuffer.OriginY >> subY;
            int chromaWidth = Av1Math.DivideLog2Ceiling(frameBuffer.Width, subX);
            int chromaHeight = Av1Math.DivideLog2Ceiling(frameBuffer.Height, subY);

            AssertExtendedPlane(frameBuffer, frameBuffer.BufferCb!, chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 1);
            AssertExtendedPlane(frameBuffer, frameBuffer.BufferCr!, chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 2);
        }
    }

    /// <summary>
    /// Verifies that a refreshed shown frame retains its ungrained reconstruction while exposing an independently grained output.
    /// </summary>
    [Fact]
    public void GrainedPresentationPreservesUngrainedReference()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(8, 8, Av1BitDepth.EightBit, true, false, false);
        ObuFrameHeader frameHeader = new()
        {
            ShowFrame = true,
            RefreshFrameFlags = 1,
            FilmGrainParameters = new ObuFilmGrainParameters
            {
                ApplyGrain = true,
                GrainSeed = 7391,
                NumYPoints = 2,
                GrainScalingMinus8 = 0,
                ArCoeffLag = 0,
                ArCoeffShiftMinus6 = 0,
                GrainScaleShift = 0
            }
        };

        frameHeader.FilmGrainParameters.PointYValue[0] = 0;
        frameHeader.FilmGrainParameters.PointYValue[1] = 255;
        frameHeader.FilmGrainParameters.PointYScaling[0] = 255;
        frameHeader.FilmGrainParameters.PointYScaling[1] = 255;

        Av1FrameBuffer<byte> reconstructed = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        InitializeVisiblePlane(
            reconstructed,
            reconstructed.BufferY!,
            reconstructed.OriginX,
            reconstructed.OriginY,
            reconstructed.Width,
            reconstructed.Height,
            0);

        Av1ReferenceFrameBorder.Extend(reconstructed);
        Span<byte> reconstructedSamples = reconstructed.BufferY!.DangerousGetSingleSpan();
        byte[] ungrainedSamples = new byte[reconstructedSamples.Length];
        reconstructedSamples.CopyTo(ungrainedSamples);
        Av1FrameBuffer<byte> presentation = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        reconstructed.CopyTo(presentation);

        Av1FilmGrainDecoder filmGrainDecoder = new(sequenceHeader, frameHeader, presentation);
        filmGrainDecoder.DecodeFrame();

        Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1ReferenceFrame retainedReference = new(reconstructed, frameHeader, frameInfo);
        Av1ReferenceFrame grainedOutput = new(presentation, frameHeader, frameInfo);
        Av1FrameBuffer<byte> retainedReferenceBuffer = retainedReference.FrameBuffer;
        using Av1ReferenceFrameStore store = new();
        store.Commit(frameHeader.RefreshFrameFlags, retainedReference, showFrame: false);
        store.CommitOutput(grainedOutput);

        Assert.Same(retainedReference, store.Resolve(0));
        Assert.Same(grainedOutput, store.OutputFrame);
        Assert.NotSame(retainedReference.FrameBuffer, grainedOutput.FrameBuffer);
        Assert.True(ungrainedSamples.AsSpan().SequenceEqual(retainedReference.FrameBuffer.BufferY!.DangerousGetSingleSpan()));
        Assert.False(ungrainedSamples.AsSpan().SequenceEqual(grainedOutput.FrameBuffer.BufferY!.DangerousGetSingleSpan()));

        using Av1ReferenceFrame selectedOutput = store.TakeOutput();

        Assert.Same(grainedOutput, selectedOutput);
        Assert.Null(retainedReferenceBuffer.BufferY);
        Assert.NotNull(selectedOutput.FrameBuffer.BufferY);
        Assert.Null(store.OutputFrame);
        Assert.Null(store.Resolve(0));
    }

    /// <summary>
    /// Verifies a complete padded-plane copy for one native AV1 sample precision.
    /// </summary>
    /// <param name="bitDepth">The coded sample precision.</param>
    private static void ValidateCompleteFrameCopy(Av1BitDepth bitDepth)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(5, 3, bitDepth, false, true, true);
        using Av1FrameBuffer<byte> source = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);
        using Av1FrameBuffer<byte> destination = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);

        source.OriginX--;
        source.OriginY--;
        source.Width = 4;
        source.Height = 2;
        source.MaxWidth = 4;
        source.MaxHeight = 2;
        FillCompletePlane(source, source.BufferY!, 17);
        FillCompletePlane(source, source.BufferCb!, 53);
        FillCompletePlane(source, source.BufferCr!, 89);
        destination.BufferY!.DangerousGetSingleSpan().Fill(0xA5);
        destination.BufferCb!.DangerousGetSingleSpan().Fill(0xA5);
        destination.BufferCr!.DangerousGetSingleSpan().Fill(0xA5);

        source.CopyTo(destination);

        Assert.True(source.BufferY!.DangerousGetSingleSpan().SequenceEqual(destination.BufferY!.DangerousGetSingleSpan()));
        Assert.True(source.BufferCb!.DangerousGetSingleSpan().SequenceEqual(destination.BufferCb!.DangerousGetSingleSpan()));
        Assert.True(source.BufferCr!.DangerousGetSingleSpan().SequenceEqual(destination.BufferCr!.DangerousGetSingleSpan()));
        Assert.Equal(source.StartPosition, destination.StartPosition);
        Assert.Equal(source.OriginX, destination.OriginX);
        Assert.Equal(source.OriginY, destination.OriginY);
        Assert.Equal(source.Width, destination.Width);
        Assert.Equal(source.Height, destination.Height);
        Assert.Equal(source.MaxWidth, destination.MaxWidth);
        Assert.Equal(source.MaxHeight, destination.MaxHeight);
        Assert.Equal(source.BitDepth, destination.BitDepth);
        Assert.Equal(source.ColorFormat, destination.ColorFormat);

        byte sourceFirstSample = source.BufferY!.DangerousGetSingleSpan()[0];

        destination.BufferY!.DangerousGetSingleSpan()[0] ^= byte.MaxValue;
        Assert.Equal(sourceFirstSample, source.BufferY!.DangerousGetSingleSpan()[0]);
    }

    /// <summary>
    /// Fills every storage element of one padded plane with deterministic native sample data.
    /// </summary>
    /// <param name="frameBuffer">The frame that defines the native sample size.</param>
    /// <param name="buffer">The complete padded plane.</param>
    /// <param name="seed">The plane-specific value mixed into each sample.</param>
    private static void FillCompletePlane(Av1FrameBuffer<byte> frameBuffer, Buffer2D<byte> buffer, int seed)
    {
        if (frameBuffer.BytesPerSample == 1)
        {
            Span<byte> samples = buffer.DangerousGetSingleSpan();

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (byte)((seed + (i * 17)) & byte.MaxValue);
            }

            return;
        }

        Span<ushort> highBitDepthSamples = MemoryMarshal.Cast<byte, ushort>(buffer.DangerousGetSingleSpan());

        for (int i = 0; i < highBitDepthSamples.Length; i++)
        {
            highBitDepthSamples[i] = (ushort)((seed + (i * 29)) & 0xFFF);
        }
    }

    /// <summary>
    /// Initializes one visible plane with unique samples while leaving a distinct sentinel throughout its padding.
    /// </summary>
    /// <param name="frameBuffer">The frame that defines the native sample size.</param>
    /// <param name="buffer">The padded plane to initialize.</param>
    /// <param name="originX">The horizontal visible origin in plane samples.</param>
    /// <param name="originY">The vertical visible origin in rows.</param>
    /// <param name="width">The visible plane width.</param>
    /// <param name="height">The visible plane height.</param>
    /// <param name="planeIndex">The zero-based plane index mixed into the visible samples.</param>
    private static void InitializeVisiblePlane(
        Av1FrameBuffer<byte> frameBuffer,
        Buffer2D<byte> buffer,
        int originX,
        int originY,
        int width,
        int height,
        int planeIndex)
    {
        if (frameBuffer.BytesPerSample == 1)
        {
            Span<byte> samples = buffer.DangerousGetSingleSpan();
            samples.Fill(byte.MaxValue);
            int stride = buffer.Width;

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    samples[((originY + row) * stride) + originX + column] = (byte)GetVisibleSample(planeIndex, row, column);
                }
            }

            return;
        }

        Span<ushort> highBitDepthSamples = MemoryMarshal.Cast<byte, ushort>(buffer.DangerousGetSingleSpan());
        highBitDepthSamples.Fill(ushort.MaxValue);
        int highBitDepthStride = buffer.Width >> 1;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                highBitDepthSamples[((originY + row) * highBitDepthStride) + originX + column] =
                    (ushort)GetVisibleSample(planeIndex, row, column);
            }
        }
    }

    /// <summary>
    /// Verifies every sample in one padded plane against nearest-edge replication of the initialized visible rectangle.
    /// </summary>
    /// <param name="frameBuffer">The frame that defines the native sample size.</param>
    /// <param name="buffer">The padded plane to verify.</param>
    /// <param name="originX">The horizontal visible origin in plane samples.</param>
    /// <param name="originY">The vertical visible origin in rows.</param>
    /// <param name="width">The visible plane width.</param>
    /// <param name="height">The visible plane height.</param>
    /// <param name="planeIndex">The zero-based plane index mixed into the visible samples.</param>
    private static void AssertExtendedPlane(
        Av1FrameBuffer<byte> frameBuffer,
        Buffer2D<byte> buffer,
        int originX,
        int originY,
        int width,
        int height,
        int planeIndex)
    {
        int stride = buffer.Width / frameBuffer.BytesPerSample;
        int allocatedHeight = buffer.Height;

        if (frameBuffer.BytesPerSample == 1)
        {
            ReadOnlySpan<byte> samples = buffer.DangerousGetSingleSpan();

            for (int row = 0; row < allocatedHeight; row++)
            {
                int visibleRow = Math.Clamp(row - originY, 0, height - 1);

                for (int column = 0; column < stride; column++)
                {
                    int visibleColumn = Math.Clamp(column - originX, 0, width - 1);
                    byte expected = (byte)GetVisibleSample(planeIndex, visibleRow, visibleColumn);
                    byte actual = samples[(row * stride) + column];

                    if (actual != expected)
                    {
                        Assert.Equal(expected, actual);
                    }
                }
            }

            return;
        }

        ReadOnlySpan<ushort> highBitDepthSamples = MemoryMarshal.Cast<byte, ushort>(buffer.DangerousGetSingleSpan());

        for (int row = 0; row < allocatedHeight; row++)
        {
            int visibleRow = Math.Clamp(row - originY, 0, height - 1);

            for (int column = 0; column < stride; column++)
            {
                int visibleColumn = Math.Clamp(column - originX, 0, width - 1);
                ushort expected = (ushort)GetVisibleSample(planeIndex, visibleRow, visibleColumn);
                ushort actual = highBitDepthSamples[(row * stride) + column];

                if (actual != expected)
                {
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    /// <summary>
    /// Computes the deterministic visible sample used by the border-extension oracle.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <param name="row">The visible row.</param>
    /// <param name="column">The visible column.</param>
    /// <returns>The native sample value.</returns>
    private static int GetVisibleSample(int planeIndex, int row, int column) => ((planeIndex + 1) * 31) + (row * 11) + (column * 3);

    /// <summary>
    /// Creates the smallest valid monochrome reference-frame owner for slot-lifecycle tests.
    /// </summary>
    /// <returns>A reference frame whose sample planes are owned by the caller.</returns>
    private static Av1ReferenceFrame CreateFrame()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(1, 1, Av1BitDepth.EightBit, true, false, false);
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);

        return new Av1ReferenceFrame(frameBuffer, new ObuFrameHeader(), frameInfo);
    }

    /// <summary>
    /// Fails the temporal motion-field rent after allowing every earlier tile-reader allocation to succeed.
    /// </summary>
    private sealed class FailingTemporalMotionFieldAllocator : TestMemoryAllocator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FailingTemporalMotionFieldAllocator"/> class.
        /// </summary>
        public FailingTemporalMotionFieldAllocator() => this.EnableNonThreadSafeLogging();

        /// <inheritdoc/>
        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(
            int length,
            AllocationOptions options = AllocationOptions.None)
        {
            if (typeof(T).Name == "TemporalMotionFieldEntry")
            {
                // Fail before the owner is published so the allocation log contains only resources that the
                // Av1TileReader constructor must unwind.
                throw new InvalidMemoryOperationException("The configured temporal motion-field allocation failed.");
            }

            return base.AllocateCore<T>(length, options);
        }
    }

    /// <summary>
    /// Creates the sequence geometry and color configuration used by direct frame-buffer tests.
    /// </summary>
    /// <param name="width">The maximum coded width.</param>
    /// <param name="height">The maximum coded height.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="isMonochrome">Whether the sequence contains only luma.</param>
    /// <param name="subsamplingX">Whether chroma is horizontally subsampled.</param>
    /// <param name="subsamplingY">Whether chroma is vertically subsampled.</param>
    /// <returns>The initialized sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        Av1BitDepth bitDepth,
        bool isMonochrome,
        bool subsamplingX,
        bool subsamplingY)
        => new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = isMonochrome,
                SubSamplingX = subsamplingX,
                SubSamplingY = subsamplingY,
                BitDepth = bitDepth
            }
        };
}
