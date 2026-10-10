// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Derives and validates AV1 intra-block-copy displacement vectors.
/// </summary>
internal static class Av1IntraBlockCopy
{
    /// <summary>
    /// The number of surrounding mode-information rows and columns searched for reference vectors.
    /// </summary>
    private const int ReferenceSearchDistance = 3;

    /// <summary>
    /// The weight separating immediately adjacent candidates from the outer search area.
    /// </summary>
    private const int NearestCandidateWeight = 640;

    /// <summary>
    /// The number of 64-sample blocks that an intra-block-copy source must precede the active block.
    /// </summary>
    private const int Delay64 = 4;

    /// <summary>
    /// Finds the spatial reference used to differentially decode an intra-block-copy displacement vector.
    /// </summary>
    /// <param name="partitionInfo">The current block geometry and decoded neighbors.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="superblockModeInfoSize">The superblock width in 4x4 mode-information units.</param>
    /// <param name="candidates">Reusable storage for up to eight unique reference vectors.</param>
    /// <param name="weights">Reusable storage for the corresponding spatial weights.</param>
    /// <returns>The nearest nonzero spatial candidate, or the normative tile-relative fallback.</returns>
    public static Av1MotionVector FindReference(
        ref Av1PartitionInfo partitionInfo,
        Av1TileInfo tileInfo,
        int superblockModeInfoSize,
        Span<Av1MotionVector> candidates,
        Span<int> weights)
    {
        ReferenceContext context = new(ref partitionInfo, superblockModeInfoSize);
        return FindReference(ref context, tileInfo, superblockModeInfoSize, candidates, weights);
    }

    /// <summary>
    /// Finds the spatial reference used to differentially encode an intra-block-copy displacement vector.
    /// </summary>
    /// <param name="picture">The encoded frame's mapped mode and displacement state.</param>
    /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
    /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
    /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
    /// <param name="macroBlock">The current block's frame edges and tile availability.</param>
    /// <param name="modeInfoPosition">The current block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="partitionType">The partition type that produced the block.</param>
    /// <param name="candidates">Reusable storage for up to eight unique reference vectors.</param>
    /// <param name="weights">Reusable storage for the corresponding spatial weights.</param>
    /// <returns>The nearest nonzero spatial candidate, or the normative tile-relative fallback.</returns>
    public static Av1MotionVector FindReference(
        Av1PictureControlSet picture,
        ReadOnlySpan<int> modeInfoGrid,
        ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
        ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
        Av1MacroBlockD macroBlock,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        Av1PartitionType partitionType,
        Span<Av1MotionVector> candidates,
        Span<int> weights)
    {
        int superblockModeInfoSize = picture.Sequence.SequenceHeader.SuperblockModeInfoSize;
        ReferenceContext context = new(
            picture,
            modeInfoGrid,
            modeInfoAllocation,
            displacementVectors,
            macroBlock,
            modeInfoPosition,
            blockSize,
            partitionType,
            superblockModeInfoSize);

        return FindReference(
            ref context,
            macroBlock.Tile,
            superblockModeInfoSize,
            candidates,
            weights);
    }

    /// <summary>
    /// Ranks the shared decoder or encoder reference context without allocating candidate state.
    /// </summary>
    /// <param name="context">The view of the mode information around the current block.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="superblockModeInfoSize">The superblock width in 4x4 mode-information units.</param>
    /// <param name="candidates">Reusable storage for up to eight unique reference vectors.</param>
    /// <param name="weights">Reusable storage for the corresponding spatial weights.</param>
    /// <returns>The nearest nonzero spatial candidate, or the normative tile-relative fallback.</returns>
    private static Av1MotionVector FindReference(
        ref ReferenceContext context,
        Av1TileInfo tileInfo,
        int superblockModeInfoSize,
        Span<Av1MotionVector> candidates,
        Span<int> weights)
    {
        Av1BlockSize blockSize = context.BlockSize;
        int width = blockSize.Get4x4WideCount();
        int height = blockSize.Get4x4HighCount();
        int row = context.RowIndex;
        int column = context.ColumnIndex;
        int rowAdjustment = height < 2 && (row & 1) != 0 ? 1 : 0;
        int columnAdjustment = width < 2 && (column & 1) != 0 ? 1 : 0;
        int maximumRowOffset = 0;
        int maximumColumnOffset = 0;

        if (context.AvailableAbove)
        {
            maximumRowOffset = height < 2 ? -4 + rowAdjustment : -(ReferenceSearchDistance << 1) + rowAdjustment;
            maximumRowOffset = Math.Clamp(maximumRowOffset, tileInfo.ModeInfoRowStart - row, tileInfo.ModeInfoRowEnd - row - 1);
        }

        if (context.AvailableLeft)
        {
            maximumColumnOffset = width < 2 ? -4 + columnAdjustment : -(ReferenceSearchDistance << 1) + columnAdjustment;
            maximumColumnOffset = Math.Clamp(maximumColumnOffset, tileInfo.ModeInfoColumnStart - column, tileInfo.ModeInfoColumnEnd - column - 1);
        }

        int candidateCount = 0;
        int processedRows = 0;
        int processedColumns = 0;
        if (Math.Abs(maximumRowOffset) >= 1)
        {
            ScanRow(ref context, -1, maximumRowOffset, candidates, weights, ref candidateCount, ref processedRows);
        }

        if (Math.Abs(maximumColumnOffset) >= 1)
        {
            ScanColumn(ref context, -1, maximumColumnOffset, candidates, weights, ref candidateCount, ref processedColumns);
        }

        if (context.HasTopRight)
        {
            AddBlock(ref context, -1, width, tileInfo, candidates, weights, ref candidateCount);
        }

        int nearestCandidateCount = candidateCount;
        for (int index = 0; index < nearestCandidateCount; index++)
        {
            weights[index] += NearestCandidateWeight;
        }

        // The top-left sample begins the outer search region. The adjacent and outer regions are sorted separately. This keeps the normative
        // nearest and near order, and repeated vectors still accumulate weight across both regions.
        AddBlock(ref context, -1, -1, tileInfo, candidates, weights, ref candidateCount);
        for (int index = 2; index <= ReferenceSearchDistance; index++)
        {
            int rowOffset = -(index << 1) + 1 + rowAdjustment;
            int columnOffset = -(index << 1) + 1 + columnAdjustment;
            if (Math.Abs(rowOffset) <= Math.Abs(maximumRowOffset) && Math.Abs(rowOffset) > processedRows)
            {
                ScanRow(ref context, rowOffset, maximumRowOffset, candidates, weights, ref candidateCount, ref processedRows);
            }

            if (Math.Abs(columnOffset) <= Math.Abs(maximumColumnOffset) && Math.Abs(columnOffset) > processedColumns)
            {
                ScanColumn(ref context, columnOffset, maximumColumnOffset, candidates, weights, ref candidateCount, ref processedColumns);
            }
        }

        SortByWeight(candidates, weights, 0, nearestCandidateCount);
        SortByWeight(candidates, weights, nearestCandidateCount, candidateCount);

        // The decoder clamps the ranked stack before it selects nearest and near. The displacement entropy syntax is differential. Thus an unclamped
        // spatial candidate changes every following component. This is true although a separate check tests the final decoded displacement against
        // the stricter intra-block-copy source limits.
        for (int index = 0; index < candidateCount; index++)
        {
            candidates[index] = candidates[index].ClampReference(
                blockSize.GetWidth(),
                blockSize.GetHeight(),
                context.ModeBlockToLeftEdge,
                context.ModeBlockToRightEdge,
                context.ModeBlockToTopEdge,
                context.ModeBlockToBottomEdge);
        }

        Av1MotionVector reference = candidateCount > 0 ? candidates[0] : default;
        if (reference.IsZero && candidateCount > 1)
        {
            reference = candidates[1];
        }

        if (!reference.IsZero)
        {
            return reference;
        }

        const int modeInfoSampleSize = 1 << Av1Constants.ModeInfoSizeLog2;
        const int eighthSampleScale = 8;
        int fallbackRow = -modeInfoSampleSize * superblockModeInfoSize * eighthSampleScale;
        int fallbackColumn = fallbackRow - (Delay64 * 64 * eighthSampleScale);

        return (row - superblockModeInfoSize) < tileInfo.ModeInfoRowStart
            ? new Av1MotionVector(0, fallbackColumn)
            : new Av1MotionVector(fallbackRow, 0);
    }

    /// <summary>
    /// Determines whether a decoded displacement vector references an earlier reconstructable block inside the tile.
    /// </summary>
    /// <param name="vector">The decoded displacement vector in one-eighth-sample units.</param>
    /// <param name="partitionInfo">The current block geometry.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence-level superblock and chroma configuration.</param>
    /// <returns><see langword="true"/> when the complete source block is a permitted reference, otherwise <see langword="false"/>.</returns>
    public static bool IsValid(Av1MotionVector vector, ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo, ObuSequenceHeader sequenceHeader)
        => IsValid(
            vector,
            new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex),
            partitionInfo.ModeInfo.BlockSize,
            partitionInfo.IsChroma,
            tileInfo,
            sequenceHeader);

    /// <summary>
    /// Determines whether an encoder displacement vector references an earlier reconstructable block inside the tile.
    /// </summary>
    /// <param name="vector">The displacement vector in one-eighth-sample units.</param>
    /// <param name="modeInfoPosition">The current block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="isChroma">Indicates whether chroma subsampling constraints apply.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence-level superblock and chroma configuration.</param>
    /// <returns><see langword="true"/> when the complete source block is a permitted reference, otherwise <see langword="false"/>.</returns>
    public static bool IsValid(
        Av1MotionVector vector,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        bool isChroma,
        Av1TileInfo tileInfo,
        ObuSequenceHeader sequenceHeader)
    {
        const int eighthSampleScale = 8;
        const int modeInfoSampleSize = 1 << Av1Constants.ModeInfoSizeLog2;
        if ((vector.Row & (eighthSampleScale - 1)) != 0 || (vector.Column & (eighthSampleScale - 1)) != 0 ||
            vector.Row <= -(1 << 14) || vector.Row >= (1 << 14) || vector.Column <= -(1 << 14) || vector.Column >= (1 << 14))
        {
            return false;
        }

        int row = modeInfoPosition.Y;
        int column = modeInfoPosition.X;
        int blockWidth = blockSize.GetWidth();
        int blockHeight = blockSize.GetHeight();
        int sourceTop = (row * modeInfoSampleSize * eighthSampleScale) + vector.Row;
        int sourceLeft = (column * modeInfoSampleSize * eighthSampleScale) + vector.Column;
        int sourceBottom = (((row * modeInfoSampleSize) + blockHeight) * eighthSampleScale) + vector.Row;
        int sourceRight = (((column * modeInfoSampleSize) + blockWidth) * eighthSampleScale) + vector.Column;
        int tileTop = tileInfo.ModeInfoRowStart * modeInfoSampleSize * eighthSampleScale;
        int tileLeft = tileInfo.ModeInfoColumnStart * modeInfoSampleSize * eighthSampleScale;
        int tileBottom = tileInfo.ModeInfoRowEnd * modeInfoSampleSize * eighthSampleScale;
        int tileRight = tileInfo.ModeInfoColumnEnd * modeInfoSampleSize * eighthSampleScale;
        if (sourceTop < tileTop || sourceLeft < tileLeft || sourceBottom > tileBottom || sourceRight > tileRight)
        {
            return false;
        }

        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        if (isChroma && colorConfig.PlaneCount > 1)
        {
            // A sub-8x8 luma block can map to a chroma block whose rounded origin lies one additional luma unit inside the tile. These checks
            // prevent that chroma reference from crossing the tile boundary.
            if (blockWidth < 8 && colorConfig.SubSamplingX && sourceLeft < tileLeft + (modeInfoSampleSize * eighthSampleScale))
            {
                return false;
            }

            if (blockHeight < 8 && colorConfig.SubSamplingY && sourceTop < tileTop + (modeInfoSampleSize * eighthSampleScale))
            {
                return false;
            }
        }

        int superblockModeInfoSize = sequenceHeader.SuperblockModeInfoSize;
        int superblockSize = superblockModeInfoSize * modeInfoSampleSize;
        int superblockModeInfoSizeLog2 = sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        int activeSuperblockRow = row >> superblockModeInfoSizeLog2;
        int active64Column = (column * modeInfoSampleSize) >> 6;
        int sourceSuperblockRow = ((sourceBottom >> 3) - 1) / superblockSize;
        int source64Column = ((sourceRight >> 3) - 1) >> 6;
        int tile64ColumnCount = ((tileInfo.ModeInfoColumnEnd - tileInfo.ModeInfoColumnStart - 1) >> 4) + 1;
        int active64 = (activeSuperblockRow * tile64ColumnCount) + active64Column;
        int source64 = (sourceSuperblockRow * tile64ColumnCount) + source64Column;
        if (source64 >= active64 - Delay64)
        {
            return false;
        }

        // The wavefront boundary reserves four completed 64-sample columns and advances farther right for every completed source row. A 128x128
        // superblock adds one column to account for its two 64-sample halves.
        int gradient = 1 + Delay64 + (superblockSize > 64 ? 1 : 0);
        int wavefrontOffset = gradient * (activeSuperblockRow - sourceSuperblockRow);
        return sourceSuperblockRow <= activeSuperblockRow && source64Column < active64Column - Delay64 + wavefrontOffset;
    }

    /// <summary>
    /// Scans a mode-information row using AV1's block-size-dependent steps and weights.
    /// </summary>
    /// <param name="context">The view of the mode information around the current block.</param>
    /// <param name="rowOffset">The offset of the scanned row from the current block, in 4x4 mode-information units.</param>
    /// <param name="maximumRowOffset">The farthest row offset that the search reaches.</param>
    /// <param name="candidates">The unique candidate vectors found so far.</param>
    /// <param name="weights">The accumulated weight of each candidate.</param>
    /// <param name="candidateCount">The number of candidates, updated in place.</param>
    /// <param name="processedRows">
    /// Receives the farthest row distance that a scanned candidate covers, when the candidate is at least as wide as the block. A later scan skips
    /// the rows within that distance.
    /// </param>
    private static void ScanRow(
        ref ReferenceContext context,
        int rowOffset,
        int maximumRowOffset,
        Span<Av1MotionVector> candidates,
        Span<int> weights,
        ref int candidateCount,
        ref int processedRows)
    {
        int width = context.BlockSize.Get4x4WideCount();
        int end = Math.Min(context.GetMaxBlockWide(), 16);
        int columnOffset = 0;
        if (Math.Abs(rowOffset) > 1)
        {
            columnOffset = 1;
            if ((context.ColumnIndex & 1) != 0 && width < 2)
            {
                columnOffset--;
            }
        }

        // A block at least 64 samples wide scans in steps of at least four mode-information units. A narrower block scans the rows that are not
        // adjacent in steps of at least two units.
        bool useFourUnitStep = width >= 16;
        for (int index = 0; index < end;)
        {
            ReferenceBlock candidate = context.GetModeInfoAt(
                new Point(context.ColumnIndex + columnOffset + index, context.RowIndex + rowOffset));

            int candidateWidth = candidate.BlockSize.Get4x4WideCount();
            int length = Math.Min(width, candidateWidth);
            if (useFourUnitStep)
            {
                length = Math.Max(4, length);
            }
            else if (Math.Abs(rowOffset) > 1)
            {
                length = Math.Max(2, length);
            }

            int weight = 2;
            if (width >= 2 && width <= candidateWidth)
            {
                int increment = Math.Min(-maximumRowOffset + rowOffset + 1, candidate.BlockSize.Get4x4HighCount());
                weight = Math.Max(weight, increment);
                processedRows = increment - rowOffset - 1;
            }

            AddCandidate(candidate, length * weight, candidates, weights, ref candidateCount);
            index += length;
        }
    }

    /// <summary>
    /// Scans a mode-information column using AV1's block-size-dependent steps and weights.
    /// </summary>
    /// <param name="context">The view of the mode information around the current block.</param>
    /// <param name="columnOffset">The offset of the scanned column from the current block, in 4x4 mode-information units.</param>
    /// <param name="maximumColumnOffset">The farthest column offset that the search reaches.</param>
    /// <param name="candidates">The unique candidate vectors found so far.</param>
    /// <param name="weights">The accumulated weight of each candidate.</param>
    /// <param name="candidateCount">The number of candidates, updated in place.</param>
    /// <param name="processedColumns">
    /// Receives the farthest column distance that a scanned candidate covers, when the candidate is at least as tall as the block. A later scan
    /// skips the columns within that distance.
    /// </param>
    private static void ScanColumn(
        ref ReferenceContext context,
        int columnOffset,
        int maximumColumnOffset,
        Span<Av1MotionVector> candidates,
        Span<int> weights,
        ref int candidateCount,
        ref int processedColumns)
    {
        int height = context.BlockSize.Get4x4HighCount();
        int end = Math.Min(context.GetMaxBlockHigh(), 16);
        int rowOffset = 0;
        if (Math.Abs(columnOffset) > 1)
        {
            rowOffset = 1;
            if ((context.RowIndex & 1) != 0 && height < 2)
            {
                rowOffset--;
            }
        }

        // A block at least 64 samples tall scans in steps of at least four mode-information units. A shorter block scans the columns that are not
        // adjacent in steps of at least two units.
        bool useFourUnitStep = height >= 16;
        for (int index = 0; index < end;)
        {
            ReferenceBlock candidate = context.GetModeInfoAt(
                new Point(context.ColumnIndex + columnOffset, context.RowIndex + rowOffset + index));

            int candidateHeight = candidate.BlockSize.Get4x4HighCount();
            int length = Math.Min(height, candidateHeight);
            if (useFourUnitStep)
            {
                length = Math.Max(4, length);
            }
            else if (Math.Abs(columnOffset) > 1)
            {
                length = Math.Max(2, length);
            }

            int weight = 2;
            if (height >= 2 && height <= candidateHeight)
            {
                int increment = Math.Min(-maximumColumnOffset + columnOffset + 1, candidate.BlockSize.Get4x4WideCount());
                weight = Math.Max(weight, increment);
                processedColumns = increment - columnOffset - 1;
            }

            AddCandidate(candidate, length * weight, candidates, weights, ref candidateCount);
            index += length;
        }
    }

    /// <summary>
    /// Adds the intra-block-copy vector at one tile-relative search position.
    /// </summary>
    /// <param name="context">The view of the mode information around the current block.</param>
    /// <param name="rowOffset">The row offset of the position from the current block, in 4x4 mode-information units.</param>
    /// <param name="columnOffset">The column offset of the position from the current block, in 4x4 mode-information units.</param>
    /// <param name="tileInfo">The active tile boundaries. A position outside the tile adds nothing.</param>
    /// <param name="candidates">The unique candidate vectors found so far.</param>
    /// <param name="weights">The accumulated weight of each candidate.</param>
    /// <param name="candidateCount">The number of candidates, updated in place.</param>
    private static void AddBlock(
        ref ReferenceContext context,
        int rowOffset,
        int columnOffset,
        Av1TileInfo tileInfo,
        Span<Av1MotionVector> candidates,
        Span<int> weights,
        ref int candidateCount)
    {
        int row = context.RowIndex + rowOffset;
        int column = context.ColumnIndex + columnOffset;
        if (row < tileInfo.ModeInfoRowStart || row >= tileInfo.ModeInfoRowEnd ||
            column < tileInfo.ModeInfoColumnStart || column >= tileInfo.ModeInfoColumnEnd)
        {
            return;
        }

        ReferenceBlock candidate = context.GetModeInfoAt(new Point(column, row));
        AddCandidate(candidate, 4, candidates, weights, ref candidateCount);
    }

    /// <summary>
    /// Accumulates one unique intra-block-copy candidate and its spatial weight.
    /// </summary>
    /// <param name="candidate">The neighboring block. A block that does not use intra block copy adds nothing.</param>
    /// <param name="weight">The weight that the candidate adds.</param>
    /// <param name="candidates">The unique candidate vectors found so far.</param>
    /// <param name="weights">The accumulated weight of each candidate.</param>
    /// <param name="candidateCount">The number of candidates, updated in place.</param>
    /// <remarks>
    /// A vector that is already in the list adds its weight to the existing entry. A new vector is dropped when the list is full.
    /// </remarks>
    private static void AddCandidate(
        ReferenceBlock candidate,
        int weight,
        Span<Av1MotionVector> candidates,
        Span<int> weights,
        ref int candidateCount)
    {
        if (!candidate.UseIntraBlockCopy)
        {
            return;
        }

        Av1MotionVector vector = candidate.DisplacementVector;
        int index = 0;
        for (; index < candidateCount; index++)
        {
            if (candidates[index] == vector)
            {
                weights[index] += weight;
                return;
            }
        }

        if (candidateCount < candidates.Length)
        {
            candidates[candidateCount] = vector;
            weights[candidateCount] = weight;
            candidateCount++;
        }
    }

    /// <summary>
    /// Sorts one candidate region by descending accumulated weight.
    /// </summary>
    /// <param name="candidates">The candidate vectors, sorted in place.</param>
    /// <param name="weights">The weight of each candidate, sorted in place with the candidates.</param>
    /// <param name="start">The index of the first entry of the region.</param>
    /// <param name="end">The index one past the last entry of the region.</param>
    /// <remarks>
    /// The bubble sort swaps two entries only when the second weight is strictly larger. Thus the sort is stable, and equal weights keep their scan
    /// order. Each pass ends at the last swap of the pass before, because the entries after it are already in order.
    /// </remarks>
    private static void SortByWeight(Span<Av1MotionVector> candidates, Span<int> weights, int start, int end)
    {
        int length = end;
        while (length > start)
        {
            int lastSwap = start;
            for (int index = start + 1; index < length; index++)
            {
                if (weights[index - 1] < weights[index])
                {
                    Av1MotionVector candidate = candidates[index - 1];
                    candidates[index - 1] = candidates[index];
                    candidates[index] = candidate;

                    int weight = weights[index - 1];
                    weights[index - 1] = weights[index];
                    weights[index] = weight;
                    lastSwap = index;
                }
            }

            length = lastSwap;
        }
    }

    /// <summary>
    /// Provides one allocation-free view over decoder or encoder mode-information storage.
    /// </summary>
    private readonly ref struct ReferenceContext
    {
        /// <summary>
        /// The decoder superblock whose mode information the decoder view reads. The encoder view does not read it.
        /// </summary>
        private readonly Av1SuperblockInfo decodedSuperblock;

        /// <summary>
        /// The encoder picture, or <see langword="null"/> for the decoder view.
        /// </summary>
        private readonly Av1PictureControlSet? encodedPicture;

        /// <summary>
        /// The encoder mode-information grid, read once for the block. Each cell holds an index in the allocation.
        /// </summary>
        private readonly ReadOnlySpan<int> encodedGrid;

        /// <summary>
        /// The encoder mode-information allocation, read once for the block.
        /// </summary>
        private readonly ReadOnlySpan<Av1MacroBlockModeInfo> encodedModeInfo;

        /// <summary>
        /// The motion vectors of the encoder blocks, read once for the block.
        /// </summary>
        private readonly ReadOnlySpan<Av1EncoderDisplacementVector> encodedVectors;

        /// <summary>
        /// The number of grid cells between rows of <see cref="encodedGrid"/>.
        /// </summary>
        private readonly int encodedStride;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceContext"/> struct over the decoder partition state.
        /// </summary>
        /// <param name="partitionInfo">The current block geometry and decoded neighbors.</param>
        /// <param name="superblockModeInfoSize">The superblock size in 4x4 mode-information units.</param>
        public ReferenceContext(ref Av1PartitionInfo partitionInfo, int superblockModeInfoSize)
        {
            this.decodedSuperblock = partitionInfo.SuperblockInfo;
            this.encodedPicture = null;
            this.BlockSize = partitionInfo.ModeInfo.BlockSize;
            this.RowIndex = partitionInfo.RowIndex;
            this.ColumnIndex = partitionInfo.ColumnIndex;
            this.AvailableAbove = partitionInfo.AvailableAbove;
            this.AvailableLeft = partitionInfo.AvailableLeft;
            this.ModeBlockToLeftEdge = partitionInfo.ModeBlockToLeftEdge;
            this.ModeBlockToRightEdge = partitionInfo.ModeBlockToRightEdge;
            this.ModeBlockToTopEdge = partitionInfo.ModeBlockToTopEdge;
            this.ModeBlockToBottomEdge = partitionInfo.ModeBlockToBottomEdge;
            this.HasTopRight = partitionInfo.HasTopRight(superblockModeInfoSize);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceContext"/> struct over the encoder picture state, which the caller read once.
        /// </summary>
        /// <param name="picture">The encoded frame's mapped mode and displacement state.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="macroBlock">The current block's frame edges and tile availability.</param>
        /// <param name="modeInfoPosition">The current block origin in 4x4 mode-information units.</param>
        /// <param name="blockSize">The current block size.</param>
        /// <param name="partitionType">The partition type that produced the block.</param>
        /// <param name="superblockModeInfoSize">The superblock size in 4x4 mode-information units.</param>
        public ReferenceContext(
            Av1PictureControlSet picture,
            ReadOnlySpan<int> modeInfoGrid,
            ReadOnlySpan<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<Av1EncoderDisplacementVector> displacementVectors,
            Av1MacroBlockD macroBlock,
            Point modeInfoPosition,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int superblockModeInfoSize)
        {
            this.decodedSuperblock = default;
            this.encodedPicture = picture;
            this.encodedGrid = modeInfoGrid;
            this.encodedModeInfo = modeInfoAllocation;
            this.encodedVectors = displacementVectors;
            this.encodedStride = picture.ModeInfoStride;
            this.BlockSize = blockSize;
            this.RowIndex = modeInfoPosition.Y;
            this.ColumnIndex = modeInfoPosition.X;
            this.AvailableAbove = macroBlock.IsUpAvailable;
            this.AvailableLeft = macroBlock.IsLeftAvailable;
            this.ModeBlockToLeftEdge = macroBlock.ToLeftEdge;
            this.ModeBlockToRightEdge = macroBlock.ToRightEdge;
            this.ModeBlockToTopEdge = macroBlock.ToTopEdge;
            this.ModeBlockToBottomEdge = macroBlock.ToBottomEdge;
            this.HasTopRight = Av1PartitionInfo.HasTopRight(
                blockSize,
                partitionType,
                modeInfoPosition.Y,
                modeInfoPosition.X,
                superblockModeInfoSize);
        }

        /// <summary>
        /// Gets the size of the current block.
        /// </summary>
        public Av1BlockSize BlockSize { get; }

        /// <summary>
        /// Gets the row of the current block, in 4x4 mode-information units.
        /// </summary>
        public int RowIndex { get; }

        /// <summary>
        /// Gets the column of the current block, in 4x4 mode-information units.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the row above the current block is available.
        /// </summary>
        public bool AvailableAbove { get; }

        /// <summary>
        /// Gets a value indicating whether the column to the left of the current block is available.
        /// </summary>
        public bool AvailableLeft { get; }

        /// <summary>
        /// Gets the signed distance from the left edge of the current block to the left frame edge, in one-eighth-sample units.
        /// </summary>
        public int ModeBlockToLeftEdge { get; }

        /// <summary>
        /// Gets the signed distance from the right edge of the current block to the right frame edge, in one-eighth-sample units.
        /// </summary>
        /// <remarks>
        /// The value is negative when the block extends past the right frame edge.
        /// </remarks>
        public int ModeBlockToRightEdge { get; }

        /// <summary>
        /// Gets the signed distance from the top edge of the current block to the top frame edge, in one-eighth-sample units.
        /// </summary>
        public int ModeBlockToTopEdge { get; }

        /// <summary>
        /// Gets the signed distance from the bottom edge of the current block to the bottom frame edge, in one-eighth-sample units.
        /// </summary>
        /// <remarks>
        /// The value is negative when the block extends past the bottom frame edge.
        /// </remarks>
        public int ModeBlockToBottomEdge { get; }

        /// <summary>
        /// Gets a value indicating whether the top-right neighbor of the current block is available.
        /// </summary>
        public bool HasTopRight { get; }

        /// <summary>
        /// Gets the width of the part of the current block that is inside the frame.
        /// </summary>
        /// <returns>The width, in 4x4 mode-information units.</returns>
        public int GetMaxBlockWide()
        {
            int width = this.BlockSize.GetWidth();
            if (this.ModeBlockToRightEdge < 0)
            {
                width += this.ModeBlockToRightEdge >> 3;
            }

            return width >> Av1Constants.ModeInfoSizeLog2;
        }

        /// <summary>
        /// Gets the height of the part of the current block that is inside the frame.
        /// </summary>
        /// <returns>The height, in 4x4 mode-information units.</returns>
        public int GetMaxBlockHigh()
        {
            int height = this.BlockSize.GetHeight();
            if (this.ModeBlockToBottomEdge < 0)
            {
                height += this.ModeBlockToBottomEdge >> 3;
            }

            return height >> Av1Constants.ModeInfoSizeLog2;
        }

        /// <summary>
        /// Reads the mode fields of the block that covers one mode-information position.
        /// </summary>
        /// <param name="position">The position, in 4x4 mode-information units.</param>
        /// <returns>The block size, the intra-block-copy flag and the displacement vector of that block.</returns>
        public ReferenceBlock GetModeInfoAt(Point position)
        {
            if (this.encodedPicture is not null)
            {
                int index = this.encodedGrid[(position.Y * this.encodedStride) + position.X];
                ref readonly Av1EncoderBlockModeInfo block = ref this.encodedModeInfo[index].Block;
                Av1EncoderDisplacementVector vector = this.encodedVectors[index];
                return new ReferenceBlock(
                    block.BlockSize,
                    block.UseIntraBlockCopy,
                    new Av1MotionVector(vector.Row, vector.Column));
            }

            Av1BlockModeInfo decodedModeInfo = this.decodedSuperblock.GetModeInfoAt(position);
            return new ReferenceBlock(
                decodedModeInfo.BlockSize,
                decodedModeInfo.UseIntraBlockCopy,
                decodedModeInfo.DisplacementVector);
        }
    }

    /// <summary>
    /// Carries the three neighboring mode fields consumed by displacement-reference ranking.
    /// </summary>
    private readonly struct ReferenceBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceBlock"/> struct.
        /// </summary>
        /// <param name="blockSize">The size of the neighboring block.</param>
        /// <param name="useIntraBlockCopy">A value indicating whether the neighboring block uses intra block copy.</param>
        /// <param name="displacementVector">The displacement vector of the neighboring block.</param>
        public ReferenceBlock(
            Av1BlockSize blockSize,
            bool useIntraBlockCopy,
            Av1MotionVector displacementVector)
        {
            this.BlockSize = blockSize;
            this.UseIntraBlockCopy = useIntraBlockCopy;
            this.DisplacementVector = displacementVector;
        }

        /// <summary>
        /// Gets the size of the neighboring block.
        /// </summary>
        public Av1BlockSize BlockSize { get; }

        /// <summary>
        /// Gets a value indicating whether the neighboring block uses intra block copy.
        /// </summary>
        public bool UseIntraBlockCopy { get; }

        /// <summary>
        /// Gets the displacement vector of the neighboring block.
        /// </summary>
        public Av1MotionVector DisplacementVector { get; }
    }
}
