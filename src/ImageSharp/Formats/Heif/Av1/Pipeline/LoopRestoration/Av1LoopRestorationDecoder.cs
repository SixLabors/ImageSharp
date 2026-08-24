// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies decoded AV1 loop-restoration units to a reconstructed still-image frame.
/// </summary>
internal class Av1LoopRestorationDecoder
{
    /// <summary>
    /// The number of source rows and columns required around each filtered processing stripe.
    /// </summary>
    private const int FilterBorder = 3;

    /// <summary>
    /// The additional zero-coefficient tap read by the padded Wiener convolution kernel.
    /// </summary>
    private const int WienerPadding = 1;

    /// <summary>
    /// The sequence-level bit-depth and plane-layout configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level restoration-unit and dimension configuration.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The decoded restoration filter and coefficient selections.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The reconstructed sample planes updated with restored output.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The preserved deblocked rows used at restoration-stripe boundaries.
    /// </summary>
    private readonly Av1LoopRestorationBoundary boundary;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopRestorationDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining bit depth and chroma subsampling.</param>
    /// <param name="frameHeader">The frame header defining restoration-unit sizes and frame dimensions.</param>
    /// <param name="frameInfo">The decoded restoration-unit selections and coefficients.</param>
    /// <param name="frameBuffer">The CDEF-filtered and upscaled frame samples.</param>
    /// <param name="boundary">The deblocked context preserved around restoration stripes.</param>
    public Av1LoopRestorationDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopRestorationBoundary boundary)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.boundary = boundary;
    }

    /// <summary>
    /// Restores every active color plane from an immutable post-super-resolution source snapshot.
    /// </summary>
    public void DecodeFrame()
    {
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            ObuLoopRestorationItem item = this.frameHeader.LoopRestorationParameters.Items[planeIndex];
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            this.DecodePlane(plane, subsamplingX, subsamplingY, item.Size);
        }
    }

    /// <summary>
    /// Restores one color plane in raster-ordered restoration units.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="unitSize">The nominal restoration-unit width and height in plane samples.</param>
    private void DecodePlane(Av1Plane plane, int subsamplingX, int subsamplingY, int unitSize)
    {
        int planeIndex = (int)plane;
        ObuFrameSize frameSize = this.frameHeader.FrameSize;
        int planeWidth = Av1Math.DivideLog2Ceiling(frameSize.SuperResolutionUpscaledWidth, subsamplingX);
        int planeHeight = Av1Math.DivideLog2Ceiling(frameSize.FrameHeight, subsamplingY);
        int planeLength = planeWidth * planeHeight;
        MemoryAllocator allocator = this.frameBuffer.MemoryAllocator;
        using IMemoryOwner<ushort> sourceOwner = allocator.Allocate<ushort>(planeLength);
        using IMemoryOwner<ushort> destinationOwner = allocator.Allocate<ushort>(planeLength);
        Span<ushort> source = sourceOwner.Memory.Span[..planeLength];
        Span<ushort> destination = destinationOwner.Memory.Span[..planeLength];

        // Restoration units overlap in their filter context but not in their output. A separate
        // destination prevents later units from observing already restored neighboring samples.
        this.CopyPlaneToWorkingBuffer(plane, subsamplingX, subsamplingY, planeWidth, planeHeight, source);

        // AV1 lets the last unit absorb a remainder smaller than 150 percent of the nominal size.
        // Size scratch storage for that largest legal unit rather than the nominal grid step.
        int extendedUnitSize = (unitSize * 3) / 2;
        int maximumUnitWidth = Math.Min(extendedUnitSize, planeWidth);
        int maximumStripeHeight = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingY;
        int borderedStride = maximumUnitWidth + (FilterBorder * 2) + WienerPadding;
        int borderedLength = borderedStride * (maximumStripeHeight + (FilterBorder * 2) + WienerPadding);
        int wienerScratchLength = Av1WienerFilter.GetScratchLength(maximumUnitWidth, maximumStripeHeight);
        using IMemoryOwner<ushort> borderedSourceOwner = allocator.Allocate<ushort>(borderedLength);
        using IMemoryOwner<ushort> wienerScratchOwner = allocator.Allocate<ushort>(wienerScratchLength);
        Span<ushort> borderedSource = borderedSourceOwner.Memory.Span[..borderedLength];
        Span<ushort> wienerScratch = wienerScratchOwner.Memory.Span[..wienerScratchLength];
        int processingUnitWidth = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingX;
        int maximumSelfGuidedWidth = Math.Min(processingUnitWidth, maximumUnitWidth);
        int selfGuidedScratchLength = Av1SelfGuidedFilter.GetScratchLength(maximumSelfGuidedWidth, maximumStripeHeight);
        using IMemoryOwner<int> selfGuidedScratchOwner = allocator.Allocate<int>(selfGuidedScratchLength);
        Span<int> selfGuidedScratch = selfGuidedScratchOwner.Memory.Span[..selfGuidedScratchLength];

        int unitRowCount = this.frameInfo.GetLoopRestorationUnitRowCount(planeIndex);
        int unitColumnCount = this.frameInfo.GetLoopRestorationUnitColumnCount(planeIndex);
        int unitY = 0;
        for (int unitRow = 0; unitRow < unitRowCount; unitRow++)
        {
            int remainingHeight = planeHeight - unitY;
            int unadjustedUnitHeight = remainingHeight < extendedUnitSize ? remainingHeight : unitSize;
            int verticalStart = unitY;
            int verticalEnd = unitY + unadjustedUnitHeight;
            int verticalOffset = Av1LoopRestorationBoundary.ProcessingStripeOffset >> subsamplingY;

            // Unit ownership is signaled on the unshifted grid, but filtering rows follow the
            // processing-stripe grid positioned eight luma samples above it.
            verticalStart = Math.Max(0, verticalStart - verticalOffset);
            if (verticalEnd < planeHeight)
            {
                verticalEnd -= verticalOffset;
            }

            int unitX = 0;
            for (int unitColumn = 0; unitColumn < unitColumnCount; unitColumn++)
            {
                int remainingWidth = planeWidth - unitX;
                int unitWidth = remainingWidth < extendedUnitSize ? remainingWidth : unitSize;
                Av1LoopRestorationUnit unit = this.frameInfo.GetLoopRestorationUnit(
                    planeIndex,
                    unitRow,
                    unitColumn);

                this.FilterUnit(
                    planeIndex,
                    subsamplingX,
                    source,
                    destination,
                    planeWidth,
                    planeHeight,
                    unitX,
                    unitX + unitWidth,
                    verticalStart,
                    verticalEnd,
                    unit,
                    borderedSource,
                    wienerScratch,
                    selfGuidedScratch);

                unitX += unitWidth;
            }

            unitY += unadjustedUnitHeight;
        }

        this.CopyWorkingBufferToPlane(plane, subsamplingX, subsamplingY, planeWidth, planeHeight, destination);
    }

    /// <summary>
    /// Copies or filters one restoration unit without reading already restored destination samples.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="source">The immutable post-super-resolution plane samples.</param>
    /// <param name="destination">The restored destination plane samples.</param>
    /// <param name="planeWidth">The visible plane width.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="horizontalStart">The unit's first plane column.</param>
    /// <param name="horizontalEnd">The exclusive unit column limit.</param>
    /// <param name="verticalStart">The unit's stripe-aligned first plane row.</param>
    /// <param name="verticalEnd">The exclusive unit row limit.</param>
    /// <param name="unit">The decoded unit filter and coefficients.</param>
    /// <param name="borderedSource">Reusable storage for one bordered processing stripe.</param>
    /// <param name="wienerScratch">Reusable Wiener intermediate storage.</param>
    /// <param name="selfGuidedScratch">Reusable self-guided intermediate storage.</param>
    private void FilterUnit(
        int plane,
        int subsamplingX,
        ReadOnlySpan<ushort> source,
        Span<ushort> destination,
        int planeWidth,
        int planeHeight,
        int horizontalStart,
        int horizontalEnd,
        int verticalStart,
        int verticalEnd,
        Av1LoopRestorationUnit unit,
        Span<ushort> borderedSource,
        Span<ushort> wienerScratch,
        Span<int> selfGuidedScratch)
    {
        int unitWidth = horizontalEnd - horizontalStart;
        if (unit.FilterType == Av1RestorationFilterType.None)
        {
            // Every output sample still belongs to exactly one unit, including units that select
            // RESTORE_NONE, so copy the immutable source rectangle into the destination plane.
            CopyRectangle(
                source,
                destination,
                planeWidth,
                horizontalStart,
                unitWidth,
                verticalStart,
                verticalEnd);

            return;
        }

        int subsamplingY = plane == (int)Av1Plane.Y || !this.sequenceHeader.ColorConfig.SubSamplingY ? 0 : 1;
        int fullStripeHeight = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingY;
        int stripeOffset = Av1LoopRestorationBoundary.ProcessingStripeOffset >> subsamplingY;
        int unitHeight = verticalEnd - verticalStart;
        for (int unitRow = 0; unitRow < unitHeight;)
        {
            int stripeStart = verticalStart + unitRow;
            int frameStripe = (stripeStart + stripeOffset) / fullStripeHeight;
            int nominalStripeHeight = fullStripeHeight - (frameStripe == 0 ? stripeOffset : 0);
            int stripeHeight = Math.Min(nominalStripeHeight, verticalEnd - stripeStart);

            // The first frame stripe is shortened by the upward offset; subsequent stripes remain
            // 64 luma samples high, with the current unit limiting only the final iteration.
            if (unit.FilterType == Av1RestorationFilterType.Wiener)
            {
                int sourceStride = unitWidth + (FilterBorder * 2) + WienerPadding;
                int sourceLength = sourceStride * (stripeHeight + (FilterBorder * 2) + WienerPadding);
                Span<ushort> filterSource = borderedSource[..sourceLength];
                this.PopulateBorderedSource(
                    plane,
                    frameStripe,
                    source,
                    planeWidth,
                    planeHeight,
                    horizontalStart,
                    unitWidth,
                    stripeStart,
                    stripeHeight,
                    sourceStride,
                    filterSource);

                int destinationOffset = (stripeStart * planeWidth) + horizontalStart;
                int scratchLength = Av1WienerFilter.GetScratchLength(unitWidth, stripeHeight);
                Av1WienerFilter.FilterStripe(
                    filterSource,
                    sourceStride,
                    destination[destinationOffset..],
                    planeWidth,
                    unitWidth,
                    stripeHeight,
                    this.frameBuffer.BitDepth.GetBitCount(),
                    unit.WienerHorizontal,
                    unit.WienerVertical,
                    wienerScratch[..scratchLength]);
            }
            else
            {
                int processingUnitWidth = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingX;

                // Self-guided local statistics restart at each normative 64-luma processing unit.
                // Context still crosses the chunk boundary because the source is the full plane.
                for (int unitColumn = 0; unitColumn < unitWidth; unitColumn += processingUnitWidth)
                {
                    int blockWidth = Math.Min(processingUnitWidth, unitWidth - unitColumn);
                    int blockStart = horizontalStart + unitColumn;
                    int sourceStride = blockWidth + (FilterBorder * 2) + WienerPadding;
                    int sourceLength = sourceStride * (stripeHeight + (FilterBorder * 2) + WienerPadding);
                    Span<ushort> filterSource = borderedSource[..sourceLength];
                    this.PopulateBorderedSource(
                        plane,
                        frameStripe,
                        source,
                        planeWidth,
                        planeHeight,
                        blockStart,
                        blockWidth,
                        stripeStart,
                        stripeHeight,
                        sourceStride,
                        filterSource);

                    int destinationOffset = (stripeStart * planeWidth) + blockStart;
                    int scratchLength = Av1SelfGuidedFilter.GetScratchLength(blockWidth, stripeHeight);
                    Av1SelfGuidedFilter.FilterBlock(
                        filterSource,
                        sourceStride,
                        destination[destinationOffset..],
                        planeWidth,
                        blockWidth,
                        stripeHeight,
                        this.frameBuffer.BitDepth.GetBitCount(),
                        unit.SgrParameterSet,
                        unit.SgrProjectionCoefficients,
                        selfGuidedScratch[..scratchLength]);
                }
            }

            unitRow += stripeHeight;
        }
    }

    /// <summary>
    /// Builds one filter source rectangle with normative horizontal and stripe-boundary extension.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="frameStripe">The frame-relative processing-stripe index.</param>
    /// <param name="source">The immutable post-super-resolution plane samples.</param>
    /// <param name="planeWidth">The visible plane width.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="blockStart">The first filtered plane column.</param>
    /// <param name="blockWidth">The number of filtered columns.</param>
    /// <param name="stripeStart">The first filtered plane row.</param>
    /// <param name="stripeHeight">The number of filtered rows.</param>
    /// <param name="destinationStride">The number of samples between bordered destination rows.</param>
    /// <param name="destination">The bordered filter source rectangle.</param>
    private void PopulateBorderedSource(
        int plane,
        int frameStripe,
        ReadOnlySpan<ushort> source,
        int planeWidth,
        int planeHeight,
        int blockStart,
        int blockWidth,
        int stripeStart,
        int stripeHeight,
        int destinationStride,
        Span<ushort> destination)
    {
        int stripeEnd = stripeStart + stripeHeight;
        int sourceRowCount = stripeHeight + (FilterBorder * 2) + WienerPadding;
        for (int destinationRow = 0; destinationRow < sourceRowCount; destinationRow++)
        {
            int sourceY = stripeStart + destinationRow - FilterBorder;
            ReadOnlySpan<ushort> sourceRow;
            if (sourceY < 0)
            {
                sourceRow = this.boundary.GetRowAbove(plane, frameStripe, 0);
            }
            else if (sourceY < stripeStart)
            {
                // Two preserved deblocked rows expand to three filter rows as [0, 0, 1].
                int contextRow = Math.Min(Math.Max(destinationRow - 1, 0), 1);
                sourceRow = this.boundary.GetRowAbove(plane, frameStripe, contextRow);
            }
            else if (sourceY >= planeHeight)
            {
                sourceRow = this.boundary.GetRowBelow(plane, frameStripe, 0);
            }
            else if (sourceY >= stripeEnd)
            {
                // The bottom expansion is [0, 1, 1]; the padded Wiener zero tap also reads row 1.
                int contextRow = Math.Min(sourceY - stripeEnd, 1);
                sourceRow = this.boundary.GetRowBelow(plane, frameStripe, contextRow);
            }
            else
            {
                sourceRow = source.Slice(sourceY * planeWidth, planeWidth);
            }

            Span<ushort> destinationRowSpan = destination.Slice(destinationRow * destinationStride, destinationStride);
            int sourceX = blockStart - FilterBorder;
            int leftExtension = Math.Max(-sourceX, 0);

            // Horizontal context crosses restoration-unit and self-guided processing-unit edges.
            // Replication occurs only at the visible frame boundary.
            if (leftExtension > 0)
            {
                destinationRowSpan[..leftExtension].Fill(sourceRow[0]);
            }

            int copiedStart = Math.Max(sourceX, 0);
            int copiedEnd = Math.Min(sourceX + destinationStride, planeWidth);
            int copiedLength = copiedEnd - copiedStart;
            sourceRow.Slice(copiedStart, copiedLength).CopyTo(destinationRowSpan[leftExtension..]);

            int populatedLength = leftExtension + copiedLength;
            if (populatedLength < destinationStride)
            {
                destinationRowSpan[populatedLength..].Fill(sourceRow[^1]);
            }
        }
    }

    /// <summary>
    /// Copies an unfiltered restoration-unit rectangle between plane working buffers.
    /// </summary>
    /// <param name="source">The immutable source plane.</param>
    /// <param name="destination">The destination plane.</param>
    /// <param name="planeWidth">The number of samples between plane rows.</param>
    /// <param name="horizontalStart">The first copied column.</param>
    /// <param name="width">The number of copied columns.</param>
    /// <param name="verticalStart">The first copied row.</param>
    /// <param name="verticalEnd">The exclusive copied row limit.</param>
    private static void CopyRectangle(
        ReadOnlySpan<ushort> source,
        Span<ushort> destination,
        int planeWidth,
        int horizontalStart,
        int width,
        int verticalStart,
        int verticalEnd)
    {
        for (int row = verticalStart; row < verticalEnd; row++)
        {
            int offset = (row * planeWidth) + horizontalStart;
            source.Slice(offset, width).CopyTo(destination[offset..]);
        }
    }

    /// <summary>
    /// Copies one reconstructed plane into an immutable 16-bit working buffer.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="planeWidth">The visible plane width.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="destination">The row-major working buffer.</param>
    private void CopyPlaneToWorkingBuffer(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        int planeWidth,
        int planeHeight,
        Span<ushort> destination)
    {
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

        for (int row = 0; row < planeHeight; row++)
        {
            // DeriveBlockPointer spans begin on the row above the visible origin for prediction,
            // hence the leading stride in every frame-relative row offset.
            int frameOffset = sourceStride + (row * sourceStride);
            Span<ushort> destinationRow = destination.Slice(row * planeWidth, planeWidth);
            if (!highBitDepthPlane.IsEmpty)
            {
                highBitDepthPlane.Slice(frameOffset, planeWidth).CopyTo(destinationRow);
            }
            else
            {
                ReadOnlySpan<byte> sourceRow = lowBitDepthPlane.Slice(frameOffset, planeWidth);
                for (int column = 0; column < planeWidth; column++)
                {
                    destinationRow[column] = sourceRow[column];
                }
            }
        }
    }

    /// <summary>
    /// Copies one restored 16-bit working buffer back to its reconstructed plane.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="planeWidth">The visible plane width.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="source">The row-major restored working buffer.</param>
    private void CopyWorkingBufferToPlane(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        int planeWidth,
        int planeHeight,
        ReadOnlySpan<ushort> source)
    {
        Span<byte> lowBitDepthPlane = default;
        Span<ushort> highBitDepthPlane = default;
        int destinationStride;
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<short> signedPlane = this.frameBuffer.DeriveBlockPointer16(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out destinationStride);

            highBitDepthPlane = MemoryMarshal.Cast<short, ushort>(signedPlane);
        }
        else
        {
            lowBitDepthPlane = this.frameBuffer.DeriveBlockPointer(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out destinationStride);
        }

        for (int row = 0; row < planeHeight; row++)
        {
            // The destination view has the same preceding prediction row as the source view.
            int frameOffset = destinationStride + (row * destinationStride);
            ReadOnlySpan<ushort> sourceRow = source.Slice(row * planeWidth, planeWidth);
            if (!highBitDepthPlane.IsEmpty)
            {
                sourceRow.CopyTo(highBitDepthPlane[frameOffset..]);
            }
            else
            {
                Span<byte> destinationRow = lowBitDepthPlane.Slice(frameOffset, planeWidth);
                for (int column = 0; column < planeWidth; column++)
                {
                    destinationRow[column] = (byte)sourceRow[column];
                }
            }
        }
    }
}
