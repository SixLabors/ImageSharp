// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;
using SixLabors.ImageSharp.Memory;

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
    /// The replicated samples on each horizontal side of a preserved row.
    /// </summary>
    public const int HorizontalBorder = 4;

    /// <summary>
    /// The maximum unit width plus horizontal context saved for each temporary stripe row.
    /// </summary>
    public const int SavedRowLength = (Av1Constants.RestorationMaxTileSize * 3 / 2) + (2 * HorizontalBorder);

    /// <summary>
    /// The allocator supplying boundary storage for this decoder session.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The original reconstruction rows temporarily replaced around the active filtering stripe.
    /// </summary>
    private IMemoryOwner<ushort>? savedRows;

    /// <summary>
    /// The physical bytes occupied by each sample in the active frame.
    /// </summary>
    private int bytesPerSample;

    /// <summary>
    /// The two preserved rows above every processing stripe, stored by plane.
    /// </summary>
    private InlineArray4<IMemoryOwner<byte>?> rowsAbove;

    /// <summary>
    /// The two preserved rows below every processing stripe, stored by plane.
    /// </summary>
    private InlineArray4<IMemoryOwner<byte>?> rowsBelow;

    /// <summary>
    /// The upscaled sample width stored for each plane boundary row.
    /// </summary>
    private InlineArray4<int> planeWidths;

    /// <summary>
    /// The number of processing stripes represented for each plane.
    /// </summary>
    private InlineArray4<int> stripeCounts;

    /// <summary>
    /// The aligned row strides in samples.
    /// </summary>
    private InlineArray4<int> planeStrides;

    /// <summary>
    /// The requested byte lengths of the retained above and below owners.
    /// </summary>
    private InlineArray4<int> storageLengths;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopRestorationBoundary"/> class.
    /// </summary>
    /// <param name="allocator">The allocator used for preserved boundary rows.</param>
    public Av1LoopRestorationBoundary(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Releases the preserved plane-boundary storage.
    /// </summary>
    public void Dispose()
    {
        this.savedRows?.Dispose();
        this.savedRows = null;
        Span<int> storageLengths = this.storageLengths;
        Span<int> stripeCounts = this.stripeCounts;
        for (int plane = 0; plane < Av1Constants.MaxPlanes; plane++)
        {
            this.rowsAbove[plane]?.Dispose();
            this.rowsAbove[plane] = null;
            this.rowsBelow[plane]?.Dispose();
            this.rowsBelow[plane] = null;
            storageLengths[plane] = 0;
            stripeCounts[plane] = 0;
        }
    }

    /// <summary>
    /// Gets storage for the three original rows above and below the active filtering stripe.
    /// </summary>
    /// <returns>The six-row save area, with a fixed ushort stride at either sample precision.</returns>
    public Span<ushort> GetStripeSaveBuffer()
    {
        // Byte frames use half of each row's byte capacity. Keeping the physical row stride fixed lets
        // every plane and frame reuse the same six rows without a precision-dependent allocation.
        IMemoryOwner<ushort> owner = this.savedRows ??= this.allocator.Allocate<ushort>(SavedRowLength * 6);
        return owner.Memory.Span[..(SavedRowLength * 6)];
    }

    /// <summary>
    /// Preserves deblocked rows at every internal restoration-stripe boundary.
    /// </summary>
    /// <param name="sequenceHeader">The active sequence bit depth and chroma layout.</param>
    /// <param name="frameHeader">The active coded and upscaled frame dimensions.</param>
    /// <param name="frameBuffer">The reconstructed samples before CDEF.</param>
    public void SaveDeblockedRows(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameBuffer<byte> frameBuffer)
    {
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        int activePlaneMask = 0;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            if (frameHeader.LoopRestorationParameters.Items[planeIndex].Type != ObuRestorationType.None)
            {
                activePlaneMask |= 1 << planeIndex;
            }
        }

        this.PrepareFrame(sequenceHeader, frameHeader, frameBuffer.BytesPerSample, activePlaneMask);

        int bitDepth = frameBuffer.BitDepth.GetBitCount();
        bool usesSuperResolution = frameSize.FrameWidth != frameSize.SuperResolutionUpscaledWidth;
        Span<int> stripeCounts = this.stripeCounts;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            if (stripeCounts[planeIndex] == 0)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            int sourceBorder = usesSuperResolution ? Av1SuperResolutionFilter.SourceBorder : 0;
            Span<byte> lowBitDepthPlane = default;
            Span<ushort> highBitDepthPlane = default;
            int sourceStride;
            if (frameBuffer.BytesPerSample == 2)
            {
                Span<short> signedPlane = frameBuffer.DeriveBlockPointer16(
                    plane,
                    new Point(-sourceBorder, 0),
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);

                highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane)[sourceStride..];
            }
            else
            {
                lowBitDepthPlane = frameBuffer.DeriveBlockPointer(
                    plane,
                    new Point(-sourceBorder, 0),
                    subsamplingX,
                    subsamplingY,
                    out sourceStride)[sourceStride..];
            }

            this.SaveDeblockedPlane(
                frameHeader,
                planeIndex,
                subsamplingX,
                subsamplingY,
                bitDepth,
                lowBitDepthPlane,
                highBitDepthPlane,
                sourceStride,
                sourceBorder);
        }
    }

    /// <summary>
    /// Prepares the row storage for the planes that can use restoration in this frame.
    /// </summary>
    /// <param name="sequenceHeader">The active sequence chroma layout.</param>
    /// <param name="frameHeader">The active frame dimensions.</param>
    /// <param name="bytesPerSample">The physical sample size.</param>
    /// <param name="activePlaneMask">The planes whose boundary rows must be retained.</param>
    public void PrepareFrame(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, int bytesPerSample, int activePlaneMask)
    {
        _ = this.GetStripeSaveBuffer();
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        this.bytesPerSample = bytesPerSample;

        // The allocation grid uses the mode-info-aligned luma height for all planes. Chroma stripes use the same indices when their sample height is halved.
        // Each stripe keeps two context rows on each side. Four replicated samples on each side of a row cover the three-sample horizontal filter context.
        int stripeCount = (ProcessingStripeOffset + (frameHeader.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) + 63) / ProcessingStripeSize;
        Span<int> planeWidths = this.planeWidths;
        Span<int> planeStrides = this.planeStrides;
        Span<int> stripeCounts = this.stripeCounts;
        Span<int> storageLengths = this.storageLengths;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            int subsamplingX = planeIndex != 0 && colorConfig.SubSamplingX ? 1 : 0;
            int planeWidth = Av1Math.DivideLog2Ceiling(frameSize.SuperResolutionUpscaledWidth, subsamplingX);
            int stride = Av1Math.AlignPowerOf2(planeWidth + (2 * HorizontalBorder), 5);
            int storageLength = stripeCount * ContextRowCount * stride * this.bytesPerSample;
            planeWidths[planeIndex] = planeWidth;
            planeStrides[planeIndex] = stride;
            stripeCounts[planeIndex] = (activePlaneMask & (1 << planeIndex)) == 0
                ? 0
                : stripeCount;

            // The physical byte size decides reuse, so a change of sample precision also reallocates. If an allocation fails, the owners
            // that exist stay in this object and Dispose releases them. This storage keeps no reference to a frame or a header.
            if (storageLengths[planeIndex] != storageLength)
            {
                this.rowsAbove[planeIndex]?.Dispose();
                this.rowsAbove[planeIndex] = null;
                this.rowsBelow[planeIndex]?.Dispose();
                this.rowsBelow[planeIndex] = null;
                storageLengths[planeIndex] = 0;
                this.rowsAbove[planeIndex] = this.allocator.Allocate<byte>(storageLength);
                this.rowsBelow[planeIndex] = this.allocator.Allocate<byte>(storageLength);
                storageLengths[planeIndex] = storageLength;
            }
        }
    }

    /// <summary>
    /// Preserves one reconstructed plane's internal stripe boundaries before CDEF.
    /// </summary>
    /// <param name="frameHeader">The active coded and upscaled dimensions.</param>
    /// <param name="planeIndex">The plane whose context is retained.</param>
    /// <param name="subsamplingX">The horizontal chroma shift.</param>
    /// <param name="subsamplingY">The vertical chroma shift.</param>
    /// <param name="bitDepth">The significant sample bits.</param>
    /// <param name="lowBitDepthPlane">The byte plane, empty for high-bit-depth samples.</param>
    /// <param name="highBitDepthPlane">The ushort plane, empty for byte samples.</param>
    /// <param name="sourceStride">The row stride in samples.</param>
    /// <param name="sourceBorder">The samples preceding the visible row origin.</param>
    public void SaveDeblockedPlane(
        ObuFrameHeader frameHeader,
        int planeIndex,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Span<byte> lowBitDepthPlane,
        Span<ushort> highBitDepthPlane,
        int sourceStride,
        int sourceBorder)
    {
        ObuFrameSize frameSize = frameHeader.FrameSize;
        bool usesSuperResolution = frameSize.FrameWidth != frameSize.SuperResolutionUpscaledWidth;
        int codedWidth = Av1Math.DivideLog2Ceiling(frameSize.FrameWidth, subsamplingX);
        int upscaledWidth = this.planeWidths[planeIndex];
        int reconstructedWidth = frameHeader.ModeInfoColumnCount
            << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);

        // The super-resolution phase uses the coded width. Its filter taps can read the full mode-info-aligned reconstruction at the right edge.
        int planeHeight = Av1Math.DivideLog2Ceiling(frameSize.FrameHeight, subsamplingY);
        int stripeHeight = ProcessingStripeSize >> subsamplingY;
        int stripeOffset = ProcessingStripeOffset >> subsamplingY;

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
                // The top context of an internal stripe is the two deblocked rows before the stripe.
                // Restoration later repeats the first row to fill its three-row border.
                SaveDeblockedRow(
                    lowBitDepthPlane,
                    highBitDepthPlane,
                    sourceStride,
                    stripeStart - ContextRowCount,
                    reconstructedWidth,
                    step,
                    initialSubpixel,
                    sourceBorder,
                    bitDepth,
                    this.GetBoundaryRow(this.rowsAbove, planeIndex, stripe, 0));

                SaveDeblockedRow(
                    lowBitDepthPlane,
                    highBitDepthPlane,
                    sourceStride,
                    stripeStart - 1,
                    reconstructedWidth,
                    step,
                    initialSubpixel,
                    sourceBorder,
                    bitDepth,
                    this.GetBoundaryRow(this.rowsAbove, planeIndex, stripe, 1));
            }

            if (stripeEnd < planeHeight)
            {
                // The bottom context of an internal stripe starts at the exclusive stripe end. If only one plane row remains,
                // the second context row repeats that row, because AV1 clamps row positions at the crop edge.
                SaveDeblockedRow(
                    lowBitDepthPlane,
                    highBitDepthPlane,
                    sourceStride,
                    stripeEnd,
                    reconstructedWidth,
                    step,
                    initialSubpixel,
                    sourceBorder,
                    bitDepth,
                    this.GetBoundaryRow(this.rowsBelow, planeIndex, stripe, 0));

                SaveDeblockedRow(
                    lowBitDepthPlane,
                    highBitDepthPlane,
                    sourceStride,
                    Math.Min(stripeEnd + 1, planeHeight - 1),
                    reconstructedWidth,
                    step,
                    initialSubpixel,
                    sourceBorder,
                    bitDepth,
                    this.GetBoundaryRow(this.rowsBelow, planeIndex, stripe, 1));
            }
        }
    }

    /// <summary>
    /// Preserves the post-CDEF and post-super-resolution rows at the top and bottom of each active plane.
    /// </summary>
    /// <param name="sequenceHeader">The active sequence chroma layout.</param>
    /// <param name="frameHeader">The active upscaled frame dimensions.</param>
    /// <param name="frameBuffer">The samples after CDEF and super-resolution.</param>
    public void SaveFrameEdgeRows(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameBuffer<byte> frameBuffer)
    {
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        Span<int> stripeCounts = this.stripeCounts;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            int stripeCount = stripeCounts[planeIndex];
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
            if (frameBuffer.BytesPerSample == 2)
            {
                Span<short> signedPlane = frameBuffer.DeriveBlockPointer16(
                    plane,
                    Point.Empty,
                    subsamplingX,
                    subsamplingY,
                    out sourceStride);

                highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane)[sourceStride..];
            }
            else
            {
                lowBitDepthPlane = frameBuffer.DeriveBlockPointer(
                    plane,
                    Point.Empty,
                    subsamplingX,
                    subsamplingY,
                    out sourceStride)[sourceStride..];
            }

            this.SaveFrameEdgePlane(planeIndex, planeHeight, lowBitDepthPlane, highBitDepthPlane, sourceStride);
        }
    }

    /// <summary>
    /// Preserves the outer rows after CDEF and super-resolution.
    /// </summary>
    /// <param name="planeIndex">The plane whose frame edges are retained.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="lowBitDepthPlane">The byte plane, empty for high-bit-depth samples.</param>
    /// <param name="highBitDepthPlane">The ushort plane, empty for byte samples.</param>
    /// <param name="sourceStride">The row stride in samples.</param>
    public void SaveFrameEdgePlane(
        int planeIndex,
        int planeHeight,
        ReadOnlySpan<byte> lowBitDepthPlane,
        ReadOnlySpan<ushort> highBitDepthPlane,
        int sourceStride)
    {
        Span<byte> topRow0 = this.GetBoundaryRow(this.rowsAbove, planeIndex, 0, 0);
        Span<byte> topRow1 = this.GetBoundaryRow(this.rowsAbove, planeIndex, 0, 1);
        CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, 0, topRow0);

        // Frame boundaries use post-CDEF/post-super-resolution samples and replicate the outer row.
        topRow0.CopyTo(topRow1);

        int lastStripe = this.stripeCounts[planeIndex] - 1;
        Span<byte> bottomRow0 = this.GetBoundaryRow(this.rowsBelow, planeIndex, lastStripe, 0);
        Span<byte> bottomRow1 = this.GetBoundaryRow(this.rowsBelow, planeIndex, lastStripe, 1);
        CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, planeHeight - 1, bottomRow0);
        bottomRow0.CopyTo(bottomRow1);
    }

    /// <summary>
    /// Gets one preserved row above a restoration processing stripe.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="stripe">The frame-relative processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved context row.</param>
    /// <returns>The preserved upscaled row.</returns>
    public ReadOnlySpan<byte> GetRowAbove(int plane, int stripe, int contextRow)
        => this.GetBoundaryRow(this.rowsAbove, plane, stripe, contextRow)
            .Slice(HorizontalBorder * this.bytesPerSample, this.planeWidths[plane] * this.bytesPerSample);

    /// <summary>
    /// Gets one preserved row below a restoration processing stripe.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="stripe">The frame-relative processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved context row.</param>
    /// <returns>The preserved upscaled row.</returns>
    public ReadOnlySpan<byte> GetRowBelow(int plane, int stripe, int contextRow)
        => this.GetBoundaryRow(this.rowsBelow, plane, stripe, contextRow)
            .Slice(HorizontalBorder * this.bytesPerSample, this.planeWidths[plane] * this.bytesPerSample);

    /// <summary>
    /// Gets one preserved row above a stripe, including its replicated horizontal context.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="stripe">The processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved row.</param>
    /// <returns>The row at the frame's physical sample precision.</returns>
    public ReadOnlySpan<byte> GetRowAboveWithBorder(int plane, int stripe, int contextRow)
        => this.GetBoundaryRow(this.rowsAbove, plane, stripe, contextRow);

    /// <summary>
    /// Gets one preserved row below a stripe, including its replicated horizontal context.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="stripe">The processing-stripe index.</param>
    /// <param name="contextRow">The first or second preserved row.</param>
    /// <returns>The row at the frame's physical sample precision.</returns>
    public ReadOnlySpan<byte> GetRowBelowWithBorder(int plane, int stripe, int contextRow)
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
    /// <param name="bitDepth">The precision used to clamp high-bit-depth interpolation.</param>
    /// <param name="destination">The preserved upscaled boundary row including horizontal context.</param>
    private static void SaveDeblockedRow(
        Span<byte> lowBitDepthPlane,
        Span<ushort> highBitDepthPlane,
        int sourceStride,
        int row,
        int reconstructedWidth,
        int step,
        int initialSubpixel,
        int sourceBorder,
        int bitDepth,
        Span<byte> destination)
    {
        if (sourceBorder == 0)
        {
            CopyFrameRow(lowBitDepthPlane, highBitDepthPlane, sourceStride, row, destination);
            return;
        }

        // Boundary rows use the same phase and reconstructed right edge as full-frame upscaling. The padded source supplies the interpolation taps.
        // The destination padding repeats the last upscaled sample and does not advance the interpolation phase.
        int sourceOffset = row * sourceStride;
        if (!highBitDepthPlane.IsEmpty)
        {
            Span<ushort> source = highBitDepthPlane.Slice(sourceOffset, reconstructedWidth + (sourceBorder * 2));
            Span<ushort> reconstructedSamples = source.Slice(sourceBorder, reconstructedWidth);
            Span<ushort> destinationSamples = MemoryMarshal.Cast<byte, ushort>(destination);

            source[..sourceBorder].Fill(reconstructedSamples[0]);
            source[(sourceBorder + reconstructedWidth)..].Fill(reconstructedSamples[^1]);
            Av1SuperResolutionFilter.UpscaleRow(
                source,
                destinationSamples[HorizontalBorder..^HorizontalBorder],
                step,
                initialSubpixel,
                bitDepth);

            destinationSamples[..HorizontalBorder].Fill(destinationSamples[HorizontalBorder]);
            destinationSamples[^HorizontalBorder..].Fill(destinationSamples[^(HorizontalBorder + 1)]);
        }
        else
        {
            Span<byte> source = lowBitDepthPlane.Slice(sourceOffset, reconstructedWidth + (sourceBorder * 2));
            Span<byte> reconstructedSamples = source.Slice(sourceBorder, reconstructedWidth);

            source[..sourceBorder].Fill(reconstructedSamples[0]);
            source[(sourceBorder + reconstructedWidth)..].Fill(reconstructedSamples[^1]);
            Av1SuperResolutionFilter.UpscaleRow(source, destination[HorizontalBorder..^HorizontalBorder], step, initialSubpixel);
            destination[..HorizontalBorder].Fill(destination[HorizontalBorder]);
            destination[^HorizontalBorder..].Fill(destination[^(HorizontalBorder + 1)]);
        }
    }

    /// <summary>
    /// Copies one sample row and replicates its horizontal context at the frame edges.
    /// </summary>
    /// <param name="lowBitDepthPlane">The byte-backed source plane.</param>
    /// <param name="highBitDepthPlane">The 16-bit source plane.</param>
    /// <param name="sourceStride">The number of samples between reconstructed rows.</param>
    /// <param name="row">The zero-based visible row index.</param>
    /// <param name="destination">The destination row including horizontal context.</param>
    private static void CopyFrameRow(
        ReadOnlySpan<byte> lowBitDepthPlane,
        ReadOnlySpan<ushort> highBitDepthPlane,
        int sourceStride,
        int row,
        Span<byte> destination)
    {
        int sourceOffset = row * sourceStride;
        if (!highBitDepthPlane.IsEmpty)
        {
            Span<ushort> destinationSamples = MemoryMarshal.Cast<byte, ushort>(destination);
            Span<ushort> visible = destinationSamples[HorizontalBorder..^HorizontalBorder];

            highBitDepthPlane.Slice(sourceOffset, visible.Length).CopyTo(visible);
            destinationSamples[..HorizontalBorder].Fill(visible[0]);
            destinationSamples[^HorizontalBorder..].Fill(visible[^1]);
        }
        else
        {
            Span<byte> visible = destination[HorizontalBorder..^HorizontalBorder];
            lowBitDepthPlane.Slice(sourceOffset, visible.Length).CopyTo(visible);
            destination[..HorizontalBorder].Fill(visible[0]);
            destination[^HorizontalBorder..].Fill(visible[^1]);
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
    private Span<byte> GetBoundaryRow(
        ReadOnlySpan<IMemoryOwner<byte>?> storage,
        int plane,
        int stripe,
        int contextRow)
    {
        int width = this.planeWidths[plane];
        int offset = ((stripe * ContextRowCount) + contextRow) * this.planeStrides[plane] * this.bytesPerSample;
        IMemoryOwner<byte> owner = storage[plane]
            ?? throw new InvalidOperationException("The selected AV1 plane has no loop-restoration boundary storage.");

        return owner.Memory.Span.Slice(offset, (width + (2 * HorizontalBorder)) * this.bytesPerSample);
    }
}
