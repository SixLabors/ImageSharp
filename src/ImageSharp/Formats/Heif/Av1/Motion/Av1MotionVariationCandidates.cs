// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Derives the neighboring-block state and fixed-capacity projection samples used to select an AV1 motion mode.
/// </summary>
internal sealed class Av1MotionVariationCandidates
{
    /// <summary>
    /// The maximum number of neighboring motion samples retained for a local warped-motion projection.
    /// </summary>
    private const int ProjectionSampleCapacity = 8;

    /// <summary>
    /// The largest neighbor step used by overlapping motion compensation, measured in 4x4 mode-information units.
    /// </summary>
    private const int MaximumNeighborStep = 16;

    /// <summary>
    /// The number of fractional bits in an AV1 motion vector and warped-motion sample position.
    /// </summary>
    private const int MotionVectorSubpixelBits = 3;

    /// <summary>
    /// Stores sample positions relative to the current block origin in one-eighth-sample units.
    /// </summary>
    private InlineArray8<Point> sourcePoints;

    /// <summary>
    /// Stores the corresponding reference-frame positions in one-eighth-sample units.
    /// </summary>
    private InlineArray8<Point> referencePoints;

    /// <summary>
    /// Gets the number of valid entries in <see cref="SourcePoints"/> and <see cref="ReferencePoints"/>.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an inter-coded block overlaps the current block's above or left edge.
    /// </summary>
    public bool HasOverlappableNeighbor { get; private set; }

    /// <summary>
    /// Gets the retained current-frame sample positions in one-eighth-sample units relative to the current block.
    /// </summary>
    public ReadOnlySpan<Point> SourcePoints => this.sourcePoints[..this.Count];

    /// <summary>
    /// Gets the retained reference-frame sample positions in one-eighth-sample units relative to the current block.
    /// </summary>
    public ReadOnlySpan<Point> ReferencePoints => this.referencePoints[..this.Count];

    /// <summary>
    /// Derives the spatial state used to select Simple Translation, OBMC, or Warped motion for one inter block.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide decoded mode map.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence-level superblock geometry.</param>
    /// <param name="frameHeader">The current frame dimensions.</param>
    /// <param name="referenceFrame">The current block's primary canonical reference.</param>
    public void Build(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame)
    {
        this.Count = 0;
        this.CollectProjectionSamples(ref partitionInfo, tileInfo, sequenceHeader, frameHeader, referenceFrame);
        this.HasOverlappableNeighbor = FindOverlappableNeighbor(ref partitionInfo, frameHeader);
    }

    /// <summary>
    /// Collects the at most eight spatial samples permitted by AV1's local warped-motion model.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide decoded mode map.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence-level superblock geometry.</param>
    /// <param name="frameHeader">The current frame dimensions.</param>
    /// <param name="referenceFrame">The current block's primary canonical reference.</param>
    private void CollectProjectionSamples(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType referenceFrame)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int width = blockSize.Get4x4WideCount();
        int height = blockSize.Get4x4HighCount();
        int row = partitionInfo.RowIndex;
        int column = partitionInfo.ColumnIndex;
        bool includeTopLeft = true;
        bool includeTopRight = true;

        if (partitionInfo.AvailableAbove)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column, row - 1));
            int candidateWidth = candidate.BlockSize.Get4x4WideCount();
            if (width <= candidateWidth)
            {
                // A wider above block can also cover the diagonal search positions. The signed alignment offset
                // prevents those positions from contributing the same block a second time.
                int columnOffset = -column % candidateWidth;
                includeTopLeft = columnOffset >= 0;
                includeTopRight = columnOffset + candidateWidth <= width;
                this.AddProjectionSample(candidate, referenceFrame, 0, -1, columnOffset, 1);
            }
            else
            {
                int end = Math.Min(width, frameHeader.ModeInfoColumnCount - column);
                for (int index = 0; index < end && this.Count < ProjectionSampleCapacity; index += candidateWidth)
                {
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column + index, row - 1));
                    candidateWidth = candidate.BlockSize.Get4x4WideCount();
                    this.AddProjectionSample(candidate, referenceFrame, 0, -1, index, 1);
                }
            }
        }

        if (partitionInfo.AvailableLeft && this.Count < ProjectionSampleCapacity)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, row));
            int candidateHeight = candidate.BlockSize.Get4x4HighCount();
            if (height <= candidateHeight)
            {
                // The same alignment rule suppresses a duplicate top-left sample when one tall left block covers it.
                int rowOffset = -row % candidateHeight;
                includeTopLeft &= rowOffset >= 0;
                this.AddProjectionSample(candidate, referenceFrame, rowOffset, 1, 0, -1);
            }
            else
            {
                int end = Math.Min(height, frameHeader.ModeInfoRowCount - row);
                for (int index = 0; index < end && this.Count < ProjectionSampleCapacity; index += candidateHeight)
                {
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, row + index));
                    candidateHeight = candidate.BlockSize.Get4x4HighCount();
                    this.AddProjectionSample(candidate, referenceFrame, index, 1, 0, -1);
                }
            }
        }

        if (includeTopLeft && partitionInfo.AvailableAbove && partitionInfo.AvailableLeft && this.Count < ProjectionSampleCapacity)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, row - 1));
            this.AddProjectionSample(candidate, referenceFrame, 0, -1, 0, -1);
        }

        int topRightRow = row - 1;
        int topRightColumn = column + width;
        bool topRightInsideTile =
            topRightRow >= tileInfo.ModeInfoRowStart &&
            topRightRow < tileInfo.ModeInfoRowEnd &&
            topRightColumn >= tileInfo.ModeInfoColumnStart &&
            topRightColumn < tileInfo.ModeInfoColumnEnd;

        if (includeTopRight &&
            this.Count < ProjectionSampleCapacity &&
            partitionInfo.HasTopRight(sequenceHeader.SuperblockModeInfoSize) &&
            topRightInsideTile)
        {
            Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(topRightColumn, topRightRow));
            this.AddProjectionSample(candidate, referenceFrame, 0, -1, width, 1);
        }
    }

    /// <summary>
    /// Determines whether an inter-coded neighbor covers either complete prediction edge of the current block.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and frame-wide decoded mode map.</param>
    /// <param name="frameHeader">The current frame dimensions.</param>
    /// <returns><see langword="true"/> when an above or left inter block can contribute overlapping prediction.</returns>
    private static bool FindOverlappableNeighbor(ref Av1PartitionInfo partitionInfo, ObuFrameHeader frameHeader)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int width = blockSize.Get4x4WideCount();
        int height = blockSize.Get4x4HighCount();
        int row = partitionInfo.RowIndex;
        int column = partitionInfo.ColumnIndex;

        if (partitionInfo.AvailableAbove)
        {
            int endColumn = Math.Min(column + width, frameHeader.ModeInfoColumnCount);
            for (int aboveColumn = column; aboveColumn < endColumn;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(aboveColumn, row - 1));
                int step = Math.Min(candidate.BlockSize.Get4x4WideCount(), MaximumNeighborStep);
                if (step == 1)
                {
                    // AV1 treats a 4-sample-wide neighbor as one half of an 8-sample pair and reads the mode record
                    // attached to the pair's second cell before advancing across both cells.
                    aboveColumn &= ~1;
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(aboveColumn + 1, row - 1));
                    step = 2;
                }

                if (IsOverlappable(candidate))
                {
                    return true;
                }

                aboveColumn += step;
            }
        }

        if (partitionInfo.AvailableLeft)
        {
            int endRow = Math.Min(row + height, frameHeader.ModeInfoRowCount);
            for (int leftRow = row; leftRow < endRow;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, leftRow));
                int step = Math.Min(candidate.BlockSize.Get4x4HighCount(), MaximumNeighborStep);
                if (step == 1)
                {
                    // The vertical scan applies the corresponding 4-sample-high pairing rule.
                    leftRow &= ~1;
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(column - 1, leftRow + 1));
                    step = 2;
                }

                if (IsOverlappable(candidate))
                {
                    return true;
                }

                leftRow += step;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends one neighboring single-reference sample when it uses the current block's primary reference.
    /// </summary>
    /// <param name="candidate">The neighboring decoded block.</param>
    /// <param name="referenceFrame">The current block's primary canonical reference.</param>
    /// <param name="rowOffset">The neighbor center row offset in 4x4 mode-information units.</param>
    /// <param name="rowSign">The direction from the current block toward the neighbor on the vertical axis.</param>
    /// <param name="columnOffset">The neighbor center column offset in 4x4 mode-information units.</param>
    /// <param name="columnSign">The direction from the current block toward the neighbor on the horizontal axis.</param>
    private void AddProjectionSample(
        Av1BlockModeInfo candidate,
        Av1ReferenceFrameType referenceFrame,
        int rowOffset,
        int rowSign,
        int columnOffset,
        int columnSign)
    {
        Span<Av1ReferenceFrameType> candidateReferences = candidate.ReferenceFrames;
        if (candidateReferences[0] != referenceFrame || candidateReferences[1] != Av1ReferenceFrameType.None)
        {
            return;
        }

        const int modeInfoSampleSize = 1 << Av1Constants.ModeInfoSizeLog2;
        int sourceX = (columnOffset * modeInfoSampleSize) + (columnSign * (candidate.BlockSize.GetWidth() >> 1)) - 1;
        int sourceY = (rowOffset * modeInfoSampleSize) + (rowSign * (candidate.BlockSize.GetHeight() >> 1)) - 1;
        Point sourcePoint = new(sourceX << MotionVectorSubpixelBits, sourceY << MotionVectorSubpixelBits);
        Av1MotionVector motionVector = candidate.MotionVectors[0];

        // Neighbor centers and motion vectors share Q3 precision. Adding them directly produces the corresponding
        // reference position without rounding away the fractional displacement needed by the projection solver.
        this.sourcePoints[this.Count] = sourcePoint;
        this.referencePoints[this.Count] = new Point(sourcePoint.X + motionVector.Column, sourcePoint.Y + motionVector.Row);
        this.Count++;
    }

    /// <summary>
    /// Determines whether a decoded neighbor can participate in overlapping motion compensation.
    /// </summary>
    /// <param name="candidate">The neighboring decoded block.</param>
    /// <returns><see langword="true"/> for inter prediction or intra-block copy; otherwise, <see langword="false"/>.</returns>
    private static bool IsOverlappable(Av1BlockModeInfo candidate)
        => candidate.UseIntraBlockCopy || candidate.ReferenceFrames[0] > Av1ReferenceFrameType.Intra;

    /// <summary>
    /// Provides fixed storage for AV1's eight local warped-motion projection samples.
    /// </summary>
    /// <typeparam name="T">The source or reference point type stored in the inline buffer.</typeparam>
    [InlineArray(ProjectionSampleCapacity)]
    private struct InlineArray8<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}
