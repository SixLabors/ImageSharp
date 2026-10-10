// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Applies restoration filters with preserved stripe context.
/// </summary>
internal static class Av1LoopRestorationFilter
{
    /// <summary>
    /// The filter context extends three samples beyond each processing block.
    /// </summary>
    private const int FilterBorder = 3;

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
    /// <param name="unit">The selected filter choice and coefficients.</param>
    /// <param name="savedRows">Storage for the temporarily overwritten source rows.</param>
    /// <param name="wienerStorage">The two-pass convolution workspace.</param>
    /// <param name="selfGuidedStorage">The self-guided arithmetic workspace.</param>
    public static void FilterUnit<TSample>(
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
        Span<ushort> wienerStorage,
        Span<int> selfGuidedStorage)
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

        // The save buffer keeps a fixed ushort row stride. In byte frames, that stride holds twice as many samples.
        int savedStride = (Av1LoopRestorationBoundary.SavedRowLength * sizeof(ushort)) / Unsafe.SizeOf<TSample>();
        for (int stripeStart = verticalStart; stripeStart < verticalEnd;)
        {
            // The first stripe of the frame is shorter by the stripe offset. At the top and bottom frame edges,
            // the replicated border of the source supplies the context, so no rows are replaced there.
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

                // Both kernels read their context of three samples on each side directly from the reconstructed plane.
                // The typed source and destination keep byte frames byte-backed through both filters.
                if (unit.FilterType == Av1RestorationFilterType.Wiener)
                {
                    int storageLength = Av1WienerFilter.GetIntermediateRowLength(blockWidth, stripeHeight);
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
                        wienerStorage[..storageLength]);
                }
                else
                {
                    int storageLength = Av1SelfGuidedFilter.GetFilterStorageLength(blockWidth, stripeHeight);
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
                        selfGuidedStorage[..storageLength]);
                }
            }

            // Later stripes and neighboring units must read the original reconstruction, not the temporary deblocked context of an earlier unit.
            // Thus the code restores the saved rows before it moves to the next stripe.
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
