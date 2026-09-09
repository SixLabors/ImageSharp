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
    /// <param name="macroBlock">The current block's frame edges and tile availability.</param>
    /// <param name="modeInfoPosition">The current block origin in 4x4 mode-information units.</param>
    /// <param name="blockSize">The current block size.</param>
    /// <param name="partitionType">The partition type that produced the block.</param>
    /// <param name="candidates">Reusable storage for up to eight unique reference vectors.</param>
    /// <param name="weights">Reusable storage for the corresponding spatial weights.</param>
    /// <returns>The nearest nonzero spatial candidate, or the normative tile-relative fallback.</returns>
    public static Av1MotionVector FindReference(
        Av1PictureControlSet picture,
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

        // The top-left sample begins the outer search region. Sorting the adjacent and outer regions independently
        // preserves the reference decoder's nearest/near ordering while still accumulating repeated vectors across both regions.
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

        // The reference decoder clamps the ranked stack before selecting nearest and near. The displacement entropy syntax is
        // differential, so using an unclamped spatial candidate changes every following component even though the
        // final decoded displacement is validated separately against the stricter intra-block-copy source limits.
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
    /// <returns><see langword="true"/> when the complete source block is a permitted reference; otherwise, <see langword="false"/>.</returns>
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
    /// <returns><see langword="true"/> when the complete source block is a permitted reference; otherwise, <see langword="false"/>.</returns>
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
            // A sub-8x8 luma block can map to a chroma block whose rounded origin lies one additional luma unit
            // inside the tile. These checks prevent that chroma reference from crossing the tile boundary.
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

        // The wavefront boundary reserves four completed 64-sample columns and advances farther right for every
        // completed source row. A 128x128 superblock adds one column to account for its two 64-sample halves.
        int gradient = 1 + Delay64 + (superblockSize > 64 ? 1 : 0);
        int wavefrontOffset = gradient * (activeSuperblockRow - sourceSuperblockRow);
        return sourceSuperblockRow <= activeSuperblockRow && source64Column < active64Column - Delay64 + wavefrontOffset;
    }

    /// <summary>
    /// Scans a mode-information row using AV1's block-size-dependent steps and weights.
    /// </summary>
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

        // Blocks below 64 samples use the finer two-mode-info-unit scan step.
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

        // Blocks below 64 samples use the finer two-mode-info-unit scan step.
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
    private readonly struct ReferenceContext
    {
        private readonly Av1SuperblockInfo decodedSuperblock;
        private readonly Av1PictureControlSet? encodedPicture;

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

        public ReferenceContext(
            Av1PictureControlSet picture,
            Av1MacroBlockD macroBlock,
            Point modeInfoPosition,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int superblockModeInfoSize)
        {
            this.decodedSuperblock = default;
            this.encodedPicture = picture;
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

        public Av1BlockSize BlockSize { get; }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public bool AvailableAbove { get; }

        public bool AvailableLeft { get; }

        public int ModeBlockToLeftEdge { get; }

        public int ModeBlockToRightEdge { get; }

        public int ModeBlockToTopEdge { get; }

        public int ModeBlockToBottomEdge { get; }

        public bool HasTopRight { get; }

        public int GetMaxBlockWide()
        {
            int width = this.BlockSize.GetWidth();
            if (this.ModeBlockToRightEdge < 0)
            {
                width += this.ModeBlockToRightEdge >> 3;
            }

            return width >> Av1Constants.ModeInfoSizeLog2;
        }

        public int GetMaxBlockHigh()
        {
            int height = this.BlockSize.GetHeight();
            if (this.ModeBlockToBottomEdge < 0)
            {
                height += this.ModeBlockToBottomEdge >> 3;
            }

            return height >> Av1Constants.ModeInfoSizeLog2;
        }

        public ReferenceBlock GetModeInfoAt(Point position)
        {
            Av1PictureControlSet? picture = this.encodedPicture;
            if (picture is not null)
            {
                Av1MacroBlockModeInfo encodedModeInfo = picture.GetFromModeInfoGrid(position);
                return new ReferenceBlock(
                    encodedModeInfo.Block.BlockSize,
                    encodedModeInfo.Block.UseIntraBlockCopy,
                    picture.GetDisplacementVector(position));
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
        public ReferenceBlock(
            Av1BlockSize blockSize,
            bool useIntraBlockCopy,
            Av1MotionVector displacementVector)
        {
            this.BlockSize = blockSize;
            this.UseIntraBlockCopy = useIntraBlockCopy;
            this.DisplacementVector = displacementVector;
        }

        public Av1BlockSize BlockSize { get; }

        public bool UseIntraBlockCopy { get; }

        public Av1MotionVector DisplacementVector { get; }
    }
}
