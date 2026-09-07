// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies decoded AV1 loop-restoration units to a reconstructed frame.
/// </summary>
internal sealed class Av1LoopRestorationDecoder : IDisposable
{
    /// <summary>
    /// The number of source rows and columns required around each filtered processing stripe.
    /// </summary>
    private const int FilterBorder = 3;

    /// <summary>
    /// The allocator supplying this decoding stage's working storage.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// The restored output retained independently of published reference frames.
    /// </summary>
    private Av1FrameBuffer<byte>? destinationBuffer;

    /// <summary>
    /// The convolution intermediate reused by successive planes and frames.
    /// </summary>
    private IMemoryOwner<ushort>? wienerOwner;

    /// <summary>
    /// The requested convolution capacity, excluding any allocator padding.
    /// </summary>
    private int wienerLength;

    /// <summary>
    /// The projection and integral/coefficient workspace reused by successive planes and frames.
    /// </summary>
    private IMemoryOwner<int>? selfGuidedOwner;

    /// <summary>
    /// The requested self-guided capacity, excluding any allocator padding.
    /// </summary>
    private int selfGuidedLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopRestorationDecoder"/> class.
    /// </summary>
    /// <param name="allocator">The allocator used for filter working storage.</param>
    public Av1LoopRestorationDecoder(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Releases the output and working storage retained by this filtering stage.
    /// </summary>
    public void Dispose()
    {
        this.destinationBuffer?.Dispose();
        this.destinationBuffer = null;
        this.wienerOwner?.Dispose();
        this.wienerOwner = null;
        this.wienerLength = 0;
        this.selfGuidedOwner?.Dispose();
        this.selfGuidedOwner = null;
        this.selfGuidedLength = 0;
    }

    /// <summary>
    /// Restores active planes while preserving the unfiltered context needed by neighboring units.
    /// </summary>
    /// <param name="sequenceHeader">The sequence defining sample precision and chroma subsampling.</param>
    /// <param name="frameHeader">The frame defining unit sizes and visible dimensions.</param>
    /// <param name="frameInfo">The decoded restoration-unit selections and coefficients.</param>
    /// <param name="frameBuffer">The CDEF-filtered and upscaled samples to restore.</param>
    /// <param name="boundary">The deblocked context preserved around restoration stripes.</param>
    public void DecodeFrame(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopRestorationBoundary boundary)
    {
        Av1FrameBuffer<byte>? destination = this.destinationBuffer;
        if (destination is null)
        {
            destination = Av1FrameBuffer<byte>.CreateRestoration(this.allocator, sequenceHeader, frameBuffer);
            this.destinationBuffer = destination;
        }
        else
        {
            destination.ResizeRestoration(sequenceHeader, frameBuffer);
        }

        // Planes run sequentially and chroma processing units never exceed the luma dimensions.
        // One capacity calculation therefore covers the complete frame. Keep successful rents reachable
        // from the session if a later allocation fails, and release old capacity before growing it.
        int maximumBlockWidth = Math.Min(Av1LoopRestorationBoundary.ProcessingStripeSize, frameHeader.FrameSize.SuperResolutionUpscaledWidth);
        int maximumStripeHeight = Av1LoopRestorationBoundary.ProcessingStripeSize;
        int wienerScratchLength = Av1WienerFilter.GetScratchLength(maximumBlockWidth, maximumStripeHeight);
        IMemoryOwner<ushort>? wiener = this.wienerOwner;
        if (wiener is null || this.wienerLength < wienerScratchLength)
        {
            wiener?.Dispose();
            this.wienerOwner = null;
            this.wienerLength = 0;
            wiener = this.allocator.Allocate<ushort>(wienerScratchLength);
            this.wienerOwner = wiener;
            this.wienerLength = wienerScratchLength;
        }

        int selfGuidedScratchLength = Av1SelfGuidedFilter.GetScratchLength(maximumBlockWidth, maximumStripeHeight);
        IMemoryOwner<int>? selfGuided = this.selfGuidedOwner;
        if (selfGuided is null || this.selfGuidedLength < selfGuidedScratchLength)
        {
            selfGuided?.Dispose();
            this.selfGuidedOwner = null;
            this.selfGuidedLength = 0;
            selfGuided = this.allocator.Allocate<int>(selfGuidedScratchLength);
            this.selfGuidedOwner = selfGuided;
            this.selfGuidedLength = selfGuidedScratchLength;
        }

        Span<ushort> wienerScratch = wiener.Memory.Span[..wienerScratchLength];
        Span<int> selfGuidedScratch = selfGuided.Memory.Span[..selfGuidedScratchLength];
        if (frameBuffer.BytesPerSample == 1)
        {
            DecodeFrame<byte>(sequenceHeader, frameHeader, frameInfo, frameBuffer, boundary, destination, wienerScratch, selfGuidedScratch);
        }
        else
        {
            DecodeFrame<ushort>(sequenceHeader, frameHeader, frameInfo, frameBuffer, boundary, destination, wienerScratch, selfGuidedScratch);
        }
    }

    /// <summary>
    /// Selects each active plane with its physical sample type fixed for the complete traversal.
    /// </summary>
    /// <typeparam name="TSample">The frame-selected byte or ushort sample type.</typeparam>
    /// <param name="sequenceHeader">The sequence configuration.</param>
    /// <param name="frameHeader">The current frame configuration.</param>
    /// <param name="frameInfo">The decoded unit state.</param>
    /// <param name="frameBuffer">The source reconstruction.</param>
    /// <param name="boundary">The preserved stripe context.</param>
    /// <param name="destinationBuffer">The separate output frame.</param>
    /// <param name="wienerScratch">The convolution workspace.</param>
    /// <param name="selfGuidedScratch">The projection and statistics workspace.</param>
    private static void DecodeFrame<TSample>(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopRestorationBoundary boundary,
        Av1FrameBuffer<byte> destinationBuffer,
        Span<ushort> wienerScratch,
        Span<int> selfGuidedScratch)
        where TSample : unmanaged
    {
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            ObuLoopRestorationItem item = frameHeader.LoopRestorationParameters.Items[planeIndex];
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            DecodePlane<TSample>(
                frameHeader,
                frameInfo,
                frameBuffer,
                boundary,
                destinationBuffer,
                plane,
                subsamplingX,
                subsamplingY,
                item.Size,
                wienerScratch,
                selfGuidedScratch);
        }

        // Publish only restored planes, after every unit has consumed the original reconstruction.
        // Frame-region views carry the physical byte width, including high-bit-depth samples.
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            if (frameHeader.LoopRestorationParameters.Items[planeIndex].Type == ObuRestorationType.None)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            Buffer2DRegion<byte> restored = destinationBuffer.DeriveBlockPointer(plane, subsamplingX, subsamplingY);
            Buffer2DRegion<byte> target = frameBuffer.DeriveBlockPointer(plane, subsamplingX, subsamplingY);
            int height = Av1Math.DivideLog2Ceiling(frameHeader.FrameSize.FrameHeight, subsamplingY);
            for (int row = 0; row < height; row++)
            {
                restored.DangerousGetRowSpan(row).CopyTo(target.DangerousGetRowSpan(row));
            }
        }
    }

    /// <summary>
    /// Restores one plane from its reconstructed samples and preserved stripe boundaries.
    /// </summary>
    /// <typeparam name="TSample">The physical plane sample type.</typeparam>
    /// <param name="frameHeader">The current frame configuration.</param>
    /// <param name="frameInfo">The decoded unit state.</param>
    /// <param name="frameBuffer">The source reconstruction.</param>
    /// <param name="boundary">The preserved stripe context.</param>
    /// <param name="destinationBuffer">The separate output frame.</param>
    /// <param name="plane">The selected color plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="unitSize">The nominal restoration-unit size in plane samples.</param>
    /// <param name="wienerScratch">The convolution workspace.</param>
    /// <param name="selfGuidedScratch">The projection and statistics workspace.</param>
    private static void DecodePlane<TSample>(
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopRestorationBoundary boundary,
        Av1FrameBuffer<byte> destinationBuffer,
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        int unitSize,
        Span<ushort> wienerScratch,
        Span<int> selfGuidedScratch)
        where TSample : unmanaged
    {
        int planeIndex = (int)plane;
        ObuFrameSize frameSize = frameHeader.FrameSize;
        int planeWidth = Av1Math.DivideLog2Ceiling(frameSize.SuperResolutionUpscaledWidth, subsamplingX);
        int planeHeight = Av1Math.DivideLog2Ceiling(frameSize.FrameHeight, subsamplingY);
        int horizontalBorder = Av1LoopRestorationBoundary.HorizontalBorder;
        Span<byte> sourceStorage = frameBuffer.DeriveBlockPointer(
            plane,
            new Point(-horizontalBorder, -FilterBorder),
            subsamplingX,
            subsamplingY,
            out int sourceStride);

        Span<TSample> source = MemoryMarshal.Cast<byte, TSample>(sourceStorage);

        // The frame view includes one preceding row. Starting it above and left of the visible plane
        // keeps every temporary boundary replacement inside the existing reconstruction allocation.
        int sourceOrigin = ((FilterBorder + 1) * sourceStride) + horizontalBorder;
        int extendedRowWidth = planeWidth + (2 * FilterBorder);
        for (int row = 0; row < planeHeight; row++)
        {
            int offset = sourceOrigin + (row * sourceStride);
            TSample first = source[offset];
            TSample last = source[offset + planeWidth - 1];
            source.Slice(offset - FilterBorder, FilterBorder).Fill(first);
            source.Slice(offset + planeWidth, FilterBorder).Fill(last);
        }

        // Only frame edges replicate samples. Internal unit/stripe edges will borrow preserved
        // deblocked rows, while their horizontal context continues through adjacent reconstructed units.
        ReadOnlySpan<TSample> top = source.Slice(sourceOrigin - FilterBorder, extendedRowWidth);
        ReadOnlySpan<TSample> bottom = source.Slice(sourceOrigin + ((planeHeight - 1) * sourceStride) - FilterBorder, extendedRowWidth);
        for (int row = 1; row <= FilterBorder; row++)
        {
            top.CopyTo(source.Slice(sourceOrigin - (row * sourceStride) - FilterBorder, extendedRowWidth));
            bottom.CopyTo(source.Slice(sourceOrigin + ((planeHeight - 1 + row) * sourceStride) - FilterBorder, extendedRowWidth));
        }

        Span<byte> destinationStorage = destinationBuffer.DeriveBlockPointer(
            plane,
            Point.Empty,
            subsamplingX,
            subsamplingY,
            out int destinationStride);

        // The frame's block view begins one row before its visible origin. Advance by logical samples
        // after selecting the physical type, preserving the aligned destination stride.
        Span<TSample> destination = MemoryMarshal.Cast<byte, TSample>(destinationStorage)[destinationStride..];

        // Preserve only the three overwritten rows on each side of the active stripe. The fixed
        // ushort storage accommodates either physical precision; its row stride remains unchanged
        // when a byte frame uses half of each row's byte capacity.
        Span<TSample> savedRows = MemoryMarshal.Cast<ushort, TSample>(boundary.GetStripeSaveBuffer());

        int extendedUnitSize = (unitSize * 3) / 2;
        int unitRowCount = frameInfo.GetLoopRestorationUnitRowCount(planeIndex);
        int unitColumnCount = frameInfo.GetLoopRestorationUnitColumnCount(planeIndex);
        int unitY = 0;
        for (int unitRow = 0; unitRow < unitRowCount; unitRow++)
        {
            int remainingHeight = planeHeight - unitY;
            int unadjustedUnitHeight = remainingHeight < extendedUnitSize ? remainingHeight : unitSize;
            int verticalStart = unitY;
            int verticalEnd = unitY + unadjustedUnitHeight;
            int verticalOffset = Av1LoopRestorationBoundary.ProcessingStripeOffset >> subsamplingY;

            // Syntax owns the unshifted grid; filtering begins eight luma rows above it, except at
            // the frame edge. The final unit absorbs a remainder smaller than half a nominal unit.
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
                Av1LoopRestorationUnit unit = frameInfo.GetLoopRestorationUnit(planeIndex, unitRow, unitColumn);
                FilterUnit(
                    boundary,
                    frameBuffer.BitDepth.GetBitCount(),
                    planeIndex,
                    subsamplingX,
                    subsamplingY,
                    source,
                    sourceOrigin,
                    sourceStride,
                    destination,
                    destinationStride,
                    planeHeight,
                    unitX,
                    unitWidth,
                    verticalStart,
                    verticalEnd,
                    unit,
                    savedRows,
                    wienerScratch,
                    selfGuidedScratch);

                unitX += unitWidth;
            }

            unitY += unadjustedUnitHeight;
        }
    }

    /// <summary>
    /// Filters a unit using temporary stripe context, restoring every overwritten source row afterward.
    /// </summary>
    /// <typeparam name="TSample">The physical plane sample type.</typeparam>
    /// <param name="boundary">The preserved stripe context.</param>
    /// <param name="bitDepth">The number of significant sample bits.</param>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="subsamplingX">The horizontal chroma shift.</param>
    /// <param name="subsamplingY">The vertical chroma shift.</param>
    /// <param name="source">The reconstructed plane including its existing border.</param>
    /// <param name="sourceOrigin">The visible plane origin in source samples.</param>
    /// <param name="sourceStride">The source row stride in samples.</param>
    /// <param name="destination">The separate restored plane.</param>
    /// <param name="destinationStride">The restored output row stride in samples.</param>
    /// <param name="planeHeight">The visible plane height.</param>
    /// <param name="horizontalStart">The first unit column.</param>
    /// <param name="unitWidth">The unit width.</param>
    /// <param name="verticalStart">The stripe-adjusted first unit row.</param>
    /// <param name="verticalEnd">The exclusive unit row limit.</param>
    /// <param name="unit">The decoded filter choice and coefficients.</param>
    /// <param name="savedRows">Storage for the temporarily overwritten source rows.</param>
    /// <param name="wienerScratch">The two-pass convolution workspace.</param>
    /// <param name="selfGuidedScratch">The self-guided arithmetic workspace.</param>
    private static void FilterUnit<TSample>(
        Av1LoopRestorationBoundary boundary,
        int bitDepth,
        int plane,
        int subsamplingX,
        int subsamplingY,
        Span<TSample> source,
        int sourceOrigin,
        int sourceStride,
        Span<TSample> destination,
        int destinationStride,
        int planeHeight,
        int horizontalStart,
        int unitWidth,
        int verticalStart,
        int verticalEnd,
        Av1LoopRestorationUnit unit,
        Span<TSample> savedRows,
        Span<ushort> wienerScratch,
        Span<int> selfGuidedScratch)
        where TSample : unmanaged
    {
        if (unit.FilterType == Av1RestorationFilterType.None)
        {
            for (int row = verticalStart; row < verticalEnd; row++)
            {
                int sourceOffset = sourceOrigin + (row * sourceStride) + horizontalStart;
                source.Slice(sourceOffset, unitWidth).CopyTo(destination.Slice((row * destinationStride) + horizontalStart, unitWidth));
            }

            return;
        }

        int fullStripeHeight = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingY;
        int stripeOffset = Av1LoopRestorationBoundary.ProcessingStripeOffset >> subsamplingY;
        int processingUnitWidth = Av1LoopRestorationBoundary.ProcessingStripeSize >> subsamplingX;
        int horizontalBorder = Av1LoopRestorationBoundary.HorizontalBorder;
        int boundaryWidth = unitWidth + (2 * horizontalBorder);
        int savedStride = (Av1LoopRestorationBoundary.SavedRowLength * sizeof(ushort)) / Unsafe.SizeOf<TSample>();
        for (int stripeStart = verticalStart; stripeStart < verticalEnd;)
        {
            int frameStripe = (stripeStart + stripeOffset) / fullStripeHeight;
            int nominalStripeHeight = fullStripeHeight - (frameStripe == 0 ? stripeOffset : 0);
            int stripeHeight = Math.Min(nominalStripeHeight, verticalEnd - stripeStart);
            bool copyAbove = stripeStart != 0;
            bool copyBelow = stripeStart + nominalStripeHeight < planeHeight;

            if (copyAbove)
            {
                for (int row = 0; row < FilterBorder; row++)
                {
                    int offset = sourceOrigin + ((stripeStart + row - FilterBorder) * sourceStride) + horizontalStart - horizontalBorder;
                    Span<TSample> replaced = source.Slice(offset, boundaryWidth);
                    replaced.CopyTo(savedRows.Slice(row * savedStride, boundaryWidth));

                    // Two preserved deblocked rows expand upward as [0, 0, 1].
                    ReadOnlySpan<TSample> boundaryRow = MemoryMarshal.Cast<byte, TSample>(
                        boundary.GetRowAboveWithBorder(plane, frameStripe, Math.Max(row - 1, 0)));

                    boundaryRow.Slice(horizontalStart, boundaryWidth).CopyTo(replaced);
                }
            }

            if (copyBelow)
            {
                for (int row = 0; row < FilterBorder; row++)
                {
                    int offset = sourceOrigin + ((stripeStart + stripeHeight + row) * sourceStride) + horizontalStart - horizontalBorder;
                    Span<TSample> replaced = source.Slice(offset, boundaryWidth);
                    replaced.CopyTo(savedRows.Slice((FilterBorder + row) * savedStride, boundaryWidth));

                    // The bottom expansion repeats the last deblocked row as [0, 1, 1].
                    ReadOnlySpan<TSample> boundaryRow = MemoryMarshal.Cast<byte, TSample>(
                        boundary.GetRowBelowWithBorder(plane, frameStripe, Math.Min(row, 1)));

                    boundaryRow.Slice(horizontalStart, boundaryWidth).CopyTo(replaced);
                }
            }

            for (int unitColumn = 0; unitColumn < unitWidth; unitColumn += processingUnitWidth)
            {
                int blockWidth = Math.Min(processingUnitWidth, unitWidth - unitColumn);
                int blockStart = horizontalStart + unitColumn;
                int sourceOffset = sourceOrigin + ((stripeStart - FilterBorder) * sourceStride) + blockStart - FilterBorder;
                int destinationOffset = (stripeStart * destinationStride) + blockStart;
                ReadOnlySpan<TSample> filterSource = source[sourceOffset..];
                Span<TSample> filterDestination = destination[destinationOffset..];

                // Both kernels read their seven-tap context directly from the reconstructed plane.
                // The typed source and destination keep byte frames byte-backed through both filters.
                if (unit.FilterType == Av1RestorationFilterType.Wiener)
                {
                    int scratchLength = Av1WienerFilter.GetScratchLength(blockWidth, stripeHeight);
                    Av1WienerFilter.FilterStripe(
                        filterSource,
                        sourceStride,
                        filterDestination,
                        destinationStride,
                        blockWidth,
                        stripeHeight,
                        bitDepth,
                        unit.WienerHorizontal,
                        unit.WienerVertical,
                        wienerScratch[..scratchLength]);
                }
                else
                {
                    int scratchLength = Av1SelfGuidedFilter.GetScratchLength(blockWidth, stripeHeight);
                    Av1SelfGuidedFilter.FilterBlock(
                        filterSource,
                        sourceStride,
                        filterDestination,
                        destinationStride,
                        blockWidth,
                        stripeHeight,
                        bitDepth,
                        unit.SgrParameterSet,
                        unit.SgrProjectionCoefficients,
                        selfGuidedScratch[..scratchLength]);
                }
            }

            // Later stripes and neighboring units must see the original reconstruction, never a
            // prior unit's temporary deblocked context. Restore the exact saved bytes before advancing.
            if (copyAbove)
            {
                for (int row = 0; row < FilterBorder; row++)
                {
                    int offset = sourceOrigin + ((stripeStart + row - FilterBorder) * sourceStride) + horizontalStart - horizontalBorder;
                    savedRows.Slice(row * savedStride, boundaryWidth).CopyTo(source.Slice(offset, boundaryWidth));
                }
            }

            if (copyBelow)
            {
                for (int row = 0; row < FilterBorder; row++)
                {
                    int offset = sourceOrigin + ((stripeStart + stripeHeight + row) * sourceStride) + horizontalStart - horizontalBorder;
                    savedRows.Slice((FilterBorder + row) * savedStride, boundaryWidth).CopyTo(source.Slice(offset, boundaryWidth));
                }
            }

            stripeStart += stripeHeight;
        }
    }
}
