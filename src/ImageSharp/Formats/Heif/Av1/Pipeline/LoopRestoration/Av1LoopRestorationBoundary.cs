// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Preserves the deblocked and frame-edge rows required by AV1 striped loop restoration.
/// </summary>
internal sealed class Av1LoopRestorationBoundary : IDisposable
{
    /// <summary>
    /// The height of a complete restoration processing stripe in luma samples.
    /// </summary>
    public const int ProcessingStripeSize = 64;

    /// <summary>
    /// The upward offset of the restoration stripe grid from the restoration-unit grid.
    /// </summary>
    public const int ProcessingStripeOffset = 8;

    /// <summary>
    /// The number of distinct deblocked rows preserved above and below a stripe.
    /// </summary>
    private const int ContextRowCount = 2;

    /// <summary>
    /// The sequence-level bit-depth and plane-layout configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The coded, reconstructed, and upscaled frame dimensions.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The reconstructed sample planes read at each pipeline boundary.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The two preserved rows above every processing stripe, stored by plane.
    /// </summary>
    private InlineArray4<IMemoryOwner<ushort>?> rowsAbove;

    /// <summary>
    /// The two preserved rows below every processing stripe, stored by plane.
    /// </summary>
    private InlineArray4<IMemoryOwner<ushort>?> rowsBelow;

    /// <summary>
    /// The upscaled sample width stored for each plane boundary row.
    /// </summary>
    private InlineArray4<int> planeWidths;

    /// <summary>
    /// The number of processing stripes represented for each plane.
    /// </summary>
    private InlineArray4<int> stripeCounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopRestorationBoundary"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining bit depth and chroma subsampling.</param>
    /// <param name="frameHeader">The frame header defining coded and upscaled dimensions.</param>
    /// <param name="frameBuffer">The reconstructed frame sampled before and after CDEF.</param>
    public Av1LoopRestorationBoundary(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;

        try
        {
            ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
            for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
            {
                if (frameHeader.LoopRestorationParameters.Items[planeIndex].Type == ObuRestorationType.None)
                {
                    continue;
                }

                Av1Plane plane = (Av1Plane)planeIndex;
                int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
                int planeWidth = Av1Math.DivideLog2Ceiling(
                    frameHeader.FrameSize.SuperResolutionUpscaledWidth,
                    subsamplingX);

                int planeHeight = Av1Math.DivideLog2Ceiling(frameHeader.FrameSize.FrameHeight, subsamplingY);
                int stripeHeight = ProcessingStripeSize >> subsamplingY;
                int stripeOffset = ProcessingStripeOffset >> subsamplingY;

                // The stripe grid begins eight luma rows above the restoration-unit grid. Including
                // that offset in the ceiling retains the short final stripe when one is present.
                int stripeCount = (planeHeight + stripeOffset + stripeHeight - 1) / stripeHeight;
                int storageLength = stripeCount * ContextRowCount * planeWidth;
                this.planeWidths[planeIndex] = planeWidth;
                this.stripeCounts[planeIndex] = stripeCount;
                this.rowsAbove[planeIndex] = frameBuffer.MemoryAllocator.Allocate<ushort>(storageLength);
                this.rowsBelow[planeIndex] = frameBuffer.MemoryAllocator.Allocate<ushort>(storageLength);
            }
        }
        catch
        {
            // The object is not available to its caller when construction fails, so release any
            // plane owners acquired before the allocator reported the failure.
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Releases the preserved plane-boundary storage.
    /// </summary>
    public void Dispose()
    {
        for (int plane = 0; plane < Av1Constants.MaxPlanes; plane++)
        {
            this.rowsAbove[plane]?.Dispose();
            this.rowsAbove[plane] = null;
            this.rowsBelow[plane]?.Dispose();
            this.rowsBelow[plane] = null;
        }
    }

    /// <summary>
    /// Preserves deblocked rows at every internal restoration-stripe boundary.
    /// </summary>
    public void SaveDeblockedRows()
    {
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        ObuFrameSize frameSize = this.frameHeader.FrameSize;
        bool usesSuperResolution = frameSize.FrameWidth != frameSize.SuperResolutionUpscaledWidth;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            if (this.stripeCounts[planeIndex] == 0)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            int codedWidth = Av1Math.DivideLog2Ceiling(frameSize.FrameWidth, subsamplingX);
            int upscaledWidth = this.planeWidths[planeIndex];
            int reconstructedWidth = this.frameHeader.ModeInfoColumnCount
                << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);

            // Super-resolution phase uses the coded width, while its filter taps can consume the
            // complete mode-info-aligned reconstruction at the right edge.
            int planeHeight = Av1Math.DivideLog2Ceiling(frameSize.FrameHeight, subsamplingY);
            int stripeHeight = ProcessingStripeSize >> subsamplingY;
            int stripeOffset = ProcessingStripeOffset >> subsamplingY;
            int sourceBorder = usesSuperResolution ? Av1SuperResolutionFilter.SourceBorder : 0;

            Span<byte> lowBitDepthPlane = default;
            Span<ushort> highBitDepthPlane = default;
            int sourceStride;
            if (this.frameBuffer.BytesPerSample == 2)
            {
                Span<short> signedPlane = this.frameBuffer.DeriveBlockPointer16(
                    plane,
                    new Point(-sourceBorder, 0),
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);

                highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane);
            }
            else
            {
                lowBitDepthPlane = this.frameBuffer.DeriveBlockPointer(
                    plane,
                    new Point(-sourceBorder, 0),
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);
            }

            int step = usesSuperResolution
                ? Av1SuperResolutionFilter.GetConvolveStep(codedWidth, upscaledWidth)
                : 0;

            int initialSubpixel = usesSuperResolution
                ? Av1SuperResolutionFilter.GetInitialSubpixel(codedWidth, upscaledWidth, step)
                : 0;

            for (int stripe = 0; stripe < this.stripeCounts[planeIndex]; stripe++)
            {
                int stripeStart = Math.Max(0, (stripe * stripeHeight) - stripeOffset);
                int stripeEnd = Math.Min(((stripe + 1) * stripeHeight) - stripeOffset, planeHeight);
                if (stripe > 0)
                {
                    // Internal top context is the two deblocked rows immediately preceding the
                    // stripe; restoration later expands the first row to fill its three-row border.
                    this.SaveDeblockedRow(
                        lowBitDepthPlane,
                        highBitDepthPlane,
                        sourceStride,
                        stripeStart - ContextRowCount,
                        reconstructedWidth,
                        step,
                        initialSubpixel,
                        sourceBorder,
                        this.GetBoundaryRow(this.rowsAbove, planeIndex, stripe, 0));

                    this.SaveDeblockedRow(
                        lowBitDepthPlane,
                        highBitDepthPlane,
                        sourceStride,
                        stripeStart - 1,
                        reconstructedWidth,
                        step,
                        initialSubpixel,
                        sourceBorder,
                        this.GetBoundaryRow(this.rowsAbove, planeIndex, stripe, 1));
                }

                if (stripeEnd < planeHeight)
                {
                    // Internal bottom context begins at the exclusive stripe end. A one-row tail
                    // duplicates its final sample row, matching AV1 crop-edge clamping.
                    this.SaveDeblockedRow(
                        lowBitDepthPlane,
                        highBitDepthPlane,
                        sourceStride,
                        stripeEnd,
                        reconstructedWidth,
                        step,
                        initialSubpixel,
                        sourceBorder,
                        this.GetBoundaryRow(this.rowsBelow, planeIndex, stripe, 0));

                    this.SaveDeblockedRow(
                        lowBitDepthPlane,
                        highBitDepthPlane,
                        sourceStride,
                        Math.Min(stripeEnd + 1, planeHeight - 1),
                        reconstructedWidth,
                        step,
                        initialSubpixel,
                        sourceBorder,
                        this.GetBoundaryRow(this.rowsBelow, planeIndex, stripe, 1));
                }
            }
        }
    }

    /// <summary>
    /// Preserves the post-CDEF and post-super-resolution rows at the top and bottom of each active plane.
    /// </summary>
    public void SaveFrameEdgeRows()
    {
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        ObuFrameSize frameSize = this.frameHeader.FrameSize;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            int stripeCount = this.stripeCounts[planeIndex];
            if (stripeCount == 0)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            int planeHeight = Av1Math.DivideLog2Ceiling(frameSize.FrameHeight, subsamplingY);
            Span<byte> lowBitDepthPlane = default;
            Span<ushort> highBitDepthPlane = default;
            int sourceStride;
            if (this.frameBuffer.BytesPerSample == 2)
            {
                Span<short> signedPlane = this.frameBuffer.DeriveBlockPointer16(
                    plane,
                    Point.Empty,
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);

                highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane);
            }
            else
            {
                lowBitDepthPlane = this.frameBuffer.DeriveBlockPointer(
                    plane,
                    Point.Empty,
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);
            }

            Span<ushort> topRow0 = this.GetBoundaryRow(this.rowsAbove, planeIndex, 0, 0);
            Span<ushort> topRow1 = this.GetBoundaryRow(this.rowsAbove, planeIndex, 0, 1);
            CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, 0, 0, topRow0);

            // Frame boundaries use post-CDEF/post-super-resolution samples and replicate the outer row.
            topRow0.CopyTo(topRow1);

            int lastStripe = stripeCount - 1;
            Span<ushort> bottomRow0 = this.GetBoundaryRow(this.rowsBelow, planeIndex, lastStripe, 0);
            Span<ushort> bottomRow1 = this.GetBoundaryRow(this.rowsBelow, planeIndex, lastStripe, 1);
            CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, planeHeight - 1, 0, bottomRow0);
            bottomRow0.CopyTo(bottomRow1);
        }
    }

    /// <summary>
    /// Gets one preserved row above a restoration processing stripe.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="stripe">The frame-relative processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved context row.</param>
    /// <returns>The preserved upscaled row.</returns>
    public ReadOnlySpan<ushort> GetRowAbove(int plane, int stripe, int contextRow)
        => this.GetBoundaryRow(this.rowsAbove, plane, stripe, contextRow);

    /// <summary>
    /// Gets one preserved row below a restoration processing stripe.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="stripe">The frame-relative processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved context row.</param>
    /// <returns>The preserved upscaled row.</returns>
    public ReadOnlySpan<ushort> GetRowBelow(int plane, int stripe, int contextRow)
        => this.GetBoundaryRow(this.rowsBelow, plane, stripe, contextRow);

    /// <summary>
    /// Preserves one deblocked row, applying normative super-resolution when the frame is scaled.
    /// </summary>
    /// <param name="lowBitDepthPlane">The byte-backed plane when the frame uses eight-bit samples.</param>
    /// <param name="highBitDepthPlane">The native 16-bit plane when the frame uses high-bit-depth samples.</param>
    /// <param name="sourceStride">The number of samples between reconstructed rows.</param>
    /// <param name="row">The reconstructed row to preserve.</param>
    /// <param name="reconstructedWidth">The mode-info-aligned width supplying super-resolution taps.</param>
    /// <param name="step">The fixed-point super-resolution source-position increment, or zero when unscaled.</param>
    /// <param name="initialSubpixel">The first super-resolution source position, or zero when unscaled.</param>
    /// <param name="sourceBorder">The decoder-padding samples preceding the visible source row.</param>
    /// <param name="destination">The preserved upscaled boundary row.</param>
    private void SaveDeblockedRow(
        Span<byte> lowBitDepthPlane,
        Span<ushort> highBitDepthPlane,
        int sourceStride,
        int row,
        int reconstructedWidth,
        int step,
        int initialSubpixel,
        int sourceBorder,
        Span<ushort> destination)
    {
        if (sourceBorder == 0)
        {
            CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, row, 0, destination);
            return;
        }

        // Boundary rows must follow the same continuous phase and aligned right-edge behavior as
        // the full-frame super-resolution stage or restoration would see different stripe context.
        int sourceOffset = sourceStride + (row * sourceStride);
        if (!highBitDepthPlane.IsEmpty)
        {
            Span<ushort> source = highBitDepthPlane.Slice(sourceOffset, reconstructedWidth + (sourceBorder * 2));
            Span<ushort> reconstructedSamples = source.Slice(sourceBorder, reconstructedWidth);

            source[..sourceBorder].Fill(reconstructedSamples[0]);
            source[(sourceBorder + reconstructedWidth)..].Fill(reconstructedSamples[^1]);
            Av1SuperResolutionFilter.UpscaleRow(
                source,
                destination,
                step,
                initialSubpixel,
                this.frameBuffer.BitDepth.GetBitCount());

            return;
        }

        Span<byte> lowBitDepthSource = lowBitDepthPlane.Slice(sourceOffset, reconstructedWidth + (sourceBorder * 2));
        Span<byte> lowBitDepthReconstructedSamples = lowBitDepthSource.Slice(sourceBorder, reconstructedWidth);

        lowBitDepthSource[..sourceBorder].Fill(lowBitDepthReconstructedSamples[0]);
        lowBitDepthSource[(sourceBorder + reconstructedWidth)..].Fill(lowBitDepthReconstructedSamples[^1]);
        Av1SuperResolutionFilter.UpscaleRow(lowBitDepthSource, destination, step, initialSubpixel);
    }

    /// <summary>
    /// Copies one reconstructed sample row into 16-bit working storage.
    /// </summary>
    /// <param name="lowBitDepthPlane">The byte-backed plane when the frame uses eight-bit samples.</param>
    /// <param name="highBitDepthPlane">The native 16-bit plane when the frame uses high-bit-depth samples.</param>
    /// <param name="sourceStride">The number of samples between reconstructed rows.</param>
    /// <param name="row">The zero-based visible row index.</param>
    /// <param name="sourceBorder">The decoder-padding samples preceding the visible source row.</param>
    /// <param name="destination">The destination row whose length determines the copied width.</param>
    private static void CopyFrameRow(
        ReadOnlySpan<byte> lowBitDepthPlane,
        ReadOnlySpan<ushort> highBitDepthPlane,
        int sourceStride,
        int row,
        int sourceBorder,
        Span<ushort> destination)
    {
        int sourceOffset = sourceStride + (row * sourceStride) + sourceBorder;
        if (!highBitDepthPlane.IsEmpty)
        {
            highBitDepthPlane.Slice(sourceOffset, destination.Length).CopyTo(destination);
            return;
        }

        ReadOnlySpan<byte> source = lowBitDepthPlane.Slice(sourceOffset, destination.Length);
        for (int column = 0; column < destination.Length; column++)
        {
            destination[column] = source[column];
        }
    }

    /// <summary>
    /// Gets writable storage for one plane-relative boundary row.
    /// </summary>
    /// <param name="storage">The above- or below-stripe storage for every plane.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="stripe">The frame-relative processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved context row.</param>
    /// <returns>The selected boundary-row span.</returns>
    private Span<ushort> GetBoundaryRow(
        ReadOnlySpan<IMemoryOwner<ushort>?> storage,
        int plane,
        int stripe,
        int contextRow)
    {
        int width = this.planeWidths[plane];
        int offset = ((stripe * ContextRowCount) + contextRow) * width;
        IMemoryOwner<ushort> owner = storage[plane]
            ?? throw new InvalidOperationException("The selected AV1 plane has no loop-restoration boundary storage.");

        return owner.Memory.Span.Slice(offset, width);
    }
}
