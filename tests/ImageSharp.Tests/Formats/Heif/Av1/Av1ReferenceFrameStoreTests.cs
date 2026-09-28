// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 frame ownership, padded-buffer copying, and reference-border preservation.
/// </summary>
[Trait("Format", "Avif")]
[ValidateDisposedMemoryAllocations]
public class Av1ReferenceFrameStoreTests
{
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
    /// Verifies that AV1 reference-border extension repeats the nearest visible edge across every allocated plane sample.
    /// </summary>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma.</param>
    /// <param name="subsamplingX">Whether chroma is horizontally subsampled.</param>
    /// <param name="subsamplingY">Whether chroma is vertically subsampled.</param>
    [Theory]
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
            frameBuffer.GetPlaneBuffer(Av1Plane.Y),
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

            InitializeVisiblePlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.U), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 1);
            InitializeVisiblePlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.V), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 2);
        }

        Av1ReferenceFrameBorder.Extend(frameBuffer);

        AssertExtendedPlane(
            frameBuffer,
            frameBuffer.GetPlaneBuffer(Av1Plane.Y),
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

            AssertExtendedPlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.U), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 1);
            AssertExtendedPlane(frameBuffer, frameBuffer.GetPlaneBuffer(Av1Plane.V), chromaOriginX, chromaOriginY, chromaWidth, chromaHeight, 2);
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
