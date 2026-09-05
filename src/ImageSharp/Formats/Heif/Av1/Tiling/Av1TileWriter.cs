// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Writes the partition, mode, transform, and coefficient syntax for one AV1 tile.
/// </summary>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Maps each AV1 block size to the five-bit partition contexts written to its bottom and right edges.
    /// </summary>
    /// <remarks>
    /// Each set bit represents a split level from 128x128 through 8x8. For example, <c>11111</c>
    /// records every split level, while <c>10000</c> records only the 128x128 split.
    /// </remarks>
    private static readonly Av1PartitionContext[] PartitionContextLookup =
        [
            new(31, 31),  // 4X4   - {0b11111, 0b11111}
            new(31, 30),  // 4X8   - {0b11111, 0b11110}
            new(30, 31),  // 8X4   - {0b11110, 0b11111}
            new(30, 30),  // 8X8   - {0b11110, 0b11110}
            new(30, 28),  // 8X16  - {0b11110, 0b11100}
            new(28, 30),  // 16X8  - {0b11100, 0b11110}
            new(28, 28),  // 16X16 - {0b11100, 0b11100}
            new(28, 24),  // 16X32 - {0b11100, 0b11000}
            new(24, 28),  // 32X16 - {0b11000, 0b11100}
            new(24, 24),  // 32X32 - {0b11000, 0b11000}
            new(24, 16),  // 32X64 - {0b11000, 0b10000}
            new(16, 24),  // 64X32 - {0b10000, 0b11000}
            new(16, 16),  // 64X64 - {0b10000, 0b10000}
            new(16, 0),   // 64X128- {0b10000, 0b00000}
            new(0, 16),   // 128X64- {0b00000, 0b10000}
            new(0, 0),    // 128X128-{0b00000, 0b00000}
            new(31, 28),  // 4X16  - {0b11111, 0b11100}
            new(28, 31),  // 16X4  - {0b11100, 0b11111}
            new(30, 24),  // 8X32  - {0b11110, 0b11000}
            new(24, 30),  // 32X8  - {0b11000, 0b11110}
            new(28, 16),  // 16X64 - {0b11100, 0b10000}
            new(16, 28),  // 64X16 - {0b10000, 0b11100}
    ];

    /// <summary>
    /// Maps neighboring intra prediction modes to the key-frame luma-mode entropy contexts.
    /// </summary>
    private static readonly byte[] IntraModeContextLookup = [0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0];

    /// <summary>
    /// Writes the partition tree and each final coding block for a superblock.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="ec_ctx">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="superblock">The encoder decisions for the superblock.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    public static void WriteSuperblock(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex)
    {
        PrecomputedBlockEncodingHandler blockEncoder = default;
        WriteSuperblock(
            pcs,
            ec_ctx,
            writer,
            superblock,
            coefficientBuffer,
            tileIndex,
            ref blockEncoder);
    }

    /// <summary>
    /// Writes a partition tree while producing each final block against the immediately preceding tile state.
    /// </summary>
    /// <typeparam name="TBlockEncoder">The value type that produces final-block decisions.</typeparam>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="ec_ctx">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="superblock">The encoder decisions for the superblock.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="blockEncoder">The handler invoked for each final block.</param>
    public static void WriteSuperblock<TBlockEncoder>(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler
    {
        ec_ctx.CodedAreaSuperblock = 0;
        ec_ctx.CodedAreaSuperblockUv = 0;
        ec_ctx.MacroBlock.Tile = superblock.TileInfo;
        int partitionIndex = 0;
        int finalBlockIndex = 0;

        // Current libaom writes the selected partition tree recursively from the superblock origin. Keeping the
        // decisions in preorder removes the global geometry catalog and keeps traversal state on this stack.
        WritePartitionTree(
            pcs,
            ec_ctx,
            writer,
            superblock,
            coefficientBuffer,
            tileIndex,
            pcs.Sequence.SequenceHeader.SuperblockSize,
            ec_ctx.SuperblockOrigin,
            ref partitionIndex,
            ref finalBlockIndex,
            ref blockEncoder);
    }

    /// <summary>
    /// Writes one selected partition node and recursively visits its split children.
    /// </summary>
    private static void WritePartitionTree<TBlockEncoder>(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Av1BlockSize blockSize,
        Point blockOrigin,
        ref int partitionIndex,
        ref int finalBlockIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler
    {
        Av1EncoderCommon common = pcs.Parent.Common;
        int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        if (modeInfoRow >= common.ModeInfoRowCount || modeInfoColumn >= common.ModeInfoColumnCount)
        {
            return;
        }

        int currentPartitionIndex = partitionIndex++;
        Av1PartitionType preparedPartition =
            (Av1PartitionType)superblock.CodingUnitPartitionTypes[currentPartitionIndex];

        Av1PartitionType partition = blockEncoder.SelectPartition(
            writer,
            entropyCodingContext.MacroBlock,
            blockOrigin,
            tileIndex,
            blockSize,
            preparedPartition);

        superblock.CodingUnitPartitionTypes[currentPartitionIndex] = (byte)partition;
        Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);
        int halfBlockSize = blockSize.GetWidth() >> 1;
        int quarterBlockSize = blockSize.GetWidth() >> 2;

        EncodePartition(
            pcs,
            writer,
            blockSize,
            partition,
            blockOrigin,
            pcs.PartitionContexts[tileIndex]);

        switch (partition)
        {
            case Av1PartitionType.None:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.Horizontal:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                if (modeInfoRow + (blockSize.Get4x4HighCount() >> 1) < common.ModeInfoRowCount)
                {
                    WriteFinalBlock(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        blockOrigin + new Size(0, halfBlockSize),
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Vertical:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);

                if (modeInfoColumn + (blockSize.Get4x4WideCount() >> 1) < common.ModeInfoColumnCount)
                {
                    WriteFinalBlock(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        blockOrigin + new Size(halfBlockSize, 0),
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Split:
                if (blockSize == Av1BlockSize.Block8x8)
                {
                    // A split 8x8 node terminates in four 4x4 coding blocks. AV1 does not carry another
                    // partition symbol at that size, so the children are final blocks rather than tree nodes.
                    for (int childIndex = 0; childIndex < 4; childIndex++)
                    {
                        Point childOrigin = blockOrigin + new Size(
                            (childIndex & 1) * halfBlockSize,
                            (childIndex >> 1) * halfBlockSize);

                        Point childModeInfoPosition = childOrigin >> Av1Constants.ModeInfoSizeLog2;
                        if (childModeInfoPosition.Y >= common.ModeInfoRowCount ||
                            childModeInfoPosition.X >= common.ModeInfoColumnCount)
                        {
                            continue;
                        }

                        WriteFinalBlock(
                            pcs,
                            entropyCodingContext,
                            writer,
                            superblock,
                            coefficientBuffer,
                            tileIndex,
                            childOrigin,
                            ref finalBlockIndex,
                            ref blockEncoder);
                    }
                }
                else
                {
                    WritePartitionTree(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin,
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(halfBlockSize, 0),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(0, halfBlockSize),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);

                    WritePartitionTree(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        subSize,
                        blockOrigin + new Size(halfBlockSize, halfBlockSize),
                        ref partitionIndex,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.HorizontalA:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.HorizontalB:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.VerticalA:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.VerticalB:
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin,
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex,
                    ref blockEncoder);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex,
                    ref blockEncoder);

                break;
            case Av1PartitionType.Horizontal4:
                for (int childIndex = 0; childIndex < 4; childIndex++)
                {
                    Point childOrigin = blockOrigin + new Size(0, childIndex * quarterBlockSize);
                    if (childIndex > 0 &&
                        (childOrigin.Y >> Av1Constants.ModeInfoSizeLog2) >= common.ModeInfoRowCount)
                    {
                        break;
                    }

                    WriteFinalBlock(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        childOrigin,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
            case Av1PartitionType.Vertical4:
                for (int childIndex = 0; childIndex < 4; childIndex++)
                {
                    Point childOrigin = blockOrigin + new Size(childIndex * quarterBlockSize, 0);
                    if (childIndex > 0 &&
                        (childOrigin.X >> Av1Constants.ModeInfoSizeLog2) >= common.ModeInfoColumnCount)
                    {
                        break;
                    }

                    WriteFinalBlock(
                        pcs,
                        entropyCodingContext,
                        writer,
                        superblock,
                        coefficientBuffer,
                        tileIndex,
                        childOrigin,
                        ref finalBlockIndex,
                        ref blockEncoder);
                }

                break;
        }

        UpdatePartitionContexts(
            pcs.PartitionContexts[tileIndex],
            blockOrigin,
            subSize,
            blockSize,
            partition);
    }

    /// <summary>
    /// Writes the next final block selected by partition traversal.
    /// </summary>
    private static void WriteFinalBlock<TBlockEncoder>(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Point blockOrigin,
        ref int finalBlockIndex,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler
    {
        ref Av1EncoderBlockStruct block = ref superblock.FinalBlocks[finalBlockIndex++];
        WriteModesBlock(
            pcs,
            entropyCodingContext,
            writer,
            superblock,
            ref block,
            tileIndex,
            blockOrigin,
            coefficientBuffer,
            ref blockEncoder);
    }

    /// <summary>
    /// Publishes the partition contexts produced by one completed partition node.
    /// </summary>
    internal static void UpdatePartitionContexts(
        Av1NeighborArrayUnit<Av1PartitionContext> neighbors,
        Point blockOrigin,
        Av1BlockSize subSize,
        Av1BlockSize blockSize,
        Av1PartitionType partition)
    {
        if (blockSize < Av1BlockSize.Block8x8)
        {
            return;
        }

        int halfBlockSize = blockSize.GetWidth() >> 1;
        Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        switch (partition)
        {
            case Av1PartitionType.Split:
                if (blockSize != Av1BlockSize.Block8x8)
                {
                    return;
                }

                UpdatePartitionContext(neighbors, blockOrigin, subSize, blockSize);
                break;
            case Av1PartitionType.None:
            case Av1PartitionType.Horizontal:
            case Av1PartitionType.Vertical:
            case Av1PartitionType.Horizontal4:
            case Av1PartitionType.Vertical4:
                UpdatePartitionContext(neighbors, blockOrigin, subSize, blockSize);
                break;
            case Av1PartitionType.HorizontalA:
                UpdatePartitionContext(neighbors, blockOrigin, splitSize, subSize);
                UpdatePartitionContext(
                    neighbors,
                    blockOrigin + new Size(0, halfBlockSize),
                    subSize,
                    subSize);

                break;
            case Av1PartitionType.HorizontalB:
                UpdatePartitionContext(neighbors, blockOrigin, subSize, subSize);
                UpdatePartitionContext(
                    neighbors,
                    blockOrigin + new Size(0, halfBlockSize),
                    splitSize,
                    subSize);

                break;
            case Av1PartitionType.VerticalA:
                UpdatePartitionContext(neighbors, blockOrigin, splitSize, subSize);
                UpdatePartitionContext(
                    neighbors,
                    blockOrigin + new Size(halfBlockSize, 0),
                    subSize,
                    subSize);

                break;
            case Av1PartitionType.VerticalB:
                UpdatePartitionContext(neighbors, blockOrigin, subSize, subSize);
                UpdatePartitionContext(
                    neighbors,
                    blockOrigin + new Size(halfBlockSize, 0),
                    splitSize,
                    subSize);

                break;
        }
    }

    /// <summary>
    /// Writes one partition-context value across the complete parent edges.
    /// </summary>
    private static void UpdatePartitionContext(
        Av1NeighborArrayUnit<Av1PartitionContext> neighbors,
        Point blockOrigin,
        Av1BlockSize contextBlockSize,
        Av1BlockSize coveredBlockSize)
    {
        Av1PartitionContext context = PartitionContextLookup[(int)contextBlockSize];
        Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask edgeMask =
            Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left |
            Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top;

        neighbors.UnitModeWrite(
            context,
            blockOrigin,
            new Size(coveredBlockSize.GetWidth(), coveredBlockSize.GetHeight()),
            edgeMask);
    }

    /// <summary>
    /// Gets the partition-symbol rate from the above and left contexts available at a block origin.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="partitionType">The partition type to measure.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="partitionContexts">The partition neighbor arrays for the tile.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetPartitionCost(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Av1PartitionType partitionType,
        Point blockOrigin,
        Av1NeighborArrayUnit<Av1PartitionContext> partitionContexts)
    {
        int context = GetPartitionContext(
            pcs,
            blockSize,
            blockOrigin,
            partitionContexts,
            out bool hasRows,
            out bool hasColumns);

        if (!hasRows && !hasColumns)
        {
            return 0;
        }

        if (hasRows && hasColumns)
        {
            return writer.GetPartitionTypeCost(partitionType, context);
        }

        return !hasRows
            ? writer.GetSplitOrHorizontalCost(partitionType, blockSize, context)
            : writer.GetSplitOrVerticalCost(partitionType, blockSize, context);
    }

    /// <summary>
    /// Writes a partition symbol using the above and left partition contexts available at a block origin.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blockSize">The square parent block size.</param>
    /// <param name="partitionType">The selected partition type.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="partition_context_na">The partition neighbor arrays for the tile.</param>
    public static void EncodePartition(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Av1PartitionType partitionType,
        Point blockOrigin,
        Av1NeighborArrayUnit<Av1PartitionContext> partition_context_na)
    {
        bool is_partition_point = blockSize >= Av1BlockSize.Block8x8;

        if (!is_partition_point)
        {
            return;
        }

        int context_index = GetPartitionContext(
            pcs,
            blockSize,
            blockOrigin,
            partition_context_na,
            out bool has_rows,
            out bool has_cols);

        if (!has_rows && !has_cols)
        {
            Guard.IsTrue(partitionType == Av1PartitionType.Split, nameof(partitionType), "Partition outside frame boundaries should have Split type.");
            return;
        }

        if (has_rows && has_cols)
        {
            writer.WritePartitionType(partitionType, context_index);
        }
        else if (!has_rows && has_cols)
        {
            writer.WriteSplitOrHorizontal(partitionType, blockSize, context_index);
        }
        else
        {
            writer.WriteSplitOrVertical(partitionType, blockSize, context_index);
        }

        return;
    }

    private static int GetPartitionContext(
        Av1PictureControlSet pcs,
        Av1BlockSize blockSize,
        Point blockOrigin,
        Av1NeighborArrayUnit<Av1PartitionContext> partitionContexts,
        out bool hasRows,
        out bool hasColumns)
    {
        int halfBlockModeInfoCount = blockSize.Get4x4WideCount() >> 1;
        Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
        hasRows = modeInfoPosition.Y + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoRowCount;
        hasColumns = modeInfoPosition.X + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoColumnCount;
        int leftIndex = partitionContexts.GetLeftIndex(blockOrigin);
        int topIndex = partitionContexts.GetTopIndex(blockOrigin);
        byte aboveContext = partitionContexts.Top[topIndex].Above == byte.MaxValue
            ? (byte)0
            : partitionContexts.Top[topIndex].Above;

        byte leftContext = partitionContexts.Left[leftIndex].Left == byte.MaxValue
            ? (byte)0
            : partitionContexts.Left[leftIndex].Left;

        int blockSizeLog2 = blockSize.Get4x4WidthLog2() - 1;
        int above = (aboveContext >> blockSizeLog2) & 1;
        int left = (leftContext >> blockSizeLog2) & 1;

        Guard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), nameof(blockSize), "Blocks need to be square.");
        Guard.IsTrue(blockSizeLog2 >= 0, nameof(blockSizeLog2), "bsl needs to be a positive integer.");

        // Each square block-size level owns four contexts selected by the current split bit of its neighbors.
        return ((left * 2) + above) + (blockSizeLog2 * Av1Constants.PartitionProbabilitySet);
    }

    /// <summary>
    /// Writes the segmentation, prediction, transform, coefficient, and filter syntax for one final coding block.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="tb_ptr">The containing superblock.</param>
    /// <param name="blk_ptr">The final encoder decisions for the block.</param>
    /// <param name="tile_idx">The zero-based tile index.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin of the block.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="blockEncoder">The final-block decision producer.</param>
    private static void WriteModesBlock<TBlockEncoder>(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock tb_ptr,
        ref Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Point blockOrigin,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ref TBlockEncoder blockEncoder)
        where TBlockEncoder : struct, IBlockEncodingHandler
    {
        Av1SequenceControlSet scs = pcs.Sequence;
        ObuFrameHeader frm_hdr = pcs.Parent.FrameHeader;
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na = pcs.LuminanceDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na = pcs.CrDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na = pcs.CbDcSignLevelCoefficientNeighbors[tile_idx];
        int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_stride = pcs.Parent.Common.ModeInfoStride;
        Point modeInfoPosition = new(mi_col, mi_row);
        ref Av1MacroBlockModeInfo macroBlockModeInfo = ref pcs.GetMacroBlockModeInfo(modeInfoPosition);
        Av1BlockSize blockSize = macroBlockModeInfo.Block.BlockSize;
        pcs.MapModeInfoBlock(modeInfoPosition, blockSize);
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;

        Guard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));

        SetModeInfoRowAndColumn(
            pcs,
            macroBlock,
            macroBlock.Tile,
            modeInfoPosition,
            blockSize,
            mi_stride,
            pcs.Parent.Common.ModeInfoRowCount,
            pcs.Parent.Common.ModeInfoColumnCount);

        // Producing the decision here exposes exactly the reconstructed neighbors, coefficient contexts,
        // and adaptive probabilities that the following syntax writes.
        tb_ptr.Workspace.PaletteInfo = default;
        ref Av1EncoderPaletteInfo paletteInfo = ref tb_ptr.Workspace.PaletteInfo;
        blockEncoder.EncodeBlock(
            writer,
            macroBlock,
            blockOrigin,
            tile_idx,
            ref macroBlockModeInfo,
            ref blk_ptr,
            ref paletteInfo);

        bool skipWritingCoefficients = macroBlockModeInfo.Block.Skip;

        // Segmentation, skip, filter, and quantizer syntax precede the prediction-domain branch in both
        // intra and inter frames. Keeping this prefix shared preserves the decoder's symbol order.
        {
            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(
                    pcs,
                    writer,
                    blockSize,
                    blockOrigin,
                    macroBlock,
                    ref blk_ptr,
                    skipWritingCoefficients,
                    beforeSkip: true);
            }

            EncodeSkipCoefficients(writer, macroBlock, skipWritingCoefficients);

            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && !pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(
                    pcs,
                    writer,
                    blockSize,
                    blockOrigin,
                    macroBlock,
                    ref blk_ptr,
                    skipWritingCoefficients,
                    beforeSkip: false);
            }

            WriteCdef(
                scs,
                pcs,
                writer,
                tile_idx,
                skipWritingCoefficients,
                modeInfoPosition);

            if (pcs.Parent.FrameHeader.DeltaQParameters.IsPresent)
            {
                int current_q_index = blk_ptr.QuantizationIndex;
                bool super_block_upper_left = (((blockOrigin.Y >> 2) & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0) &&
                    (((blockOrigin.X >> 2) & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0);
                if ((blockSize != scs.SequenceHeader.SuperblockSize || !skipWritingCoefficients) && super_block_upper_left)
                {
                    Guard.MustBeGreaterThan(current_q_index, 0, nameof(current_q_index));
                    int reduced_delta_qindex = (current_q_index - pcs.Parent.PreviousQIndex.Span[tile_idx]) /
                        frm_hdr.DeltaQParameters.Resolution;

                    writer.WriteDeltaQuantizerIndex(reduced_delta_qindex);
                    pcs.Parent.PreviousQIndex.Span[tile_idx] = current_q_index;
                }
            }

            bool isInterBlock = macroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra;
            bool isGlobalMotionForced = false;
            bool isReferenceForced = false;
            if (!frm_hdr.IsIntra)
            {
                ObuSegmentationParameters segmentation = frm_hdr.SegmentationParameters;
                int segmentId = macroBlockModeInfo.Block.SegmentId;
                isReferenceForced = segmentation.IsFeatureActive(
                    segmentId,
                    ObuSegmentationLevelFeature.ReferenceFrame);

                isGlobalMotionForced = segmentation.IsFeatureActive(
                    segmentId,
                    ObuSegmentationLevelFeature.GlobalMotionVector);

                if (!isReferenceForced && !isGlobalMotionForced)
                {
                    int intraInterContext = GetIntraInterContext(macroBlock);
                    writer.WriteIsInter(isInterBlock, intraInterContext);
                }
            }

            Av1PredictionMode lumaMode = macroBlockModeInfo.Block.Mode;
            Av1ChromaPredictionMode intra_chroma_mode = macroBlockModeInfo.Block.UvMode;
            if (isInterBlock)
            {
                if (!isReferenceForced && !isGlobalMotionForced)
                {
                    Span<byte> referenceCounts = stackalloc byte[Av1Constants.ReferenceFrameCount];
                    CollectNeighborReferenceCounts(macroBlock, referenceCounts);
                    writer.WriteSingleReference(
                        macroBlockModeInfo.Block.ReferenceFrame,
                        referenceCounts);
                }

                if (!isGlobalMotionForced)
                {
                    ref Av1ReferenceMotionVectors referenceMotionVectors = ref tb_ptr.Workspace.ReferenceMotionVectors;
                    referenceMotionVectors.Build(
                        pcs,
                        macroBlock,
                        modeInfoPosition,
                        blockSize,
                        macroBlockModeInfo.Block.PartitionType,
                        scs.SequenceHeader,
                        frm_hdr,
                        macroBlockModeInfo.Block.ReferenceFrame);

                    writer.WriteInterMode(lumaMode, referenceMotionVectors.ModeContext);
                    int referenceMotionVectorIndex = blk_ptr.ReferenceMotionVectorIndex;
                    if (lumaMode == Av1PredictionMode.NearMotionVector)
                    {
                        // NEARMV reserves stack entry zero for NEARESTMV, so its DRL decisions advance from
                        // near entry zero to one and then from one to two.
                        for (int index = 1; index < 3 && referenceMotionVectors.Count > index + 1; index++)
                        {
                            bool advance = referenceMotionVectorIndex >= index;
                            int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                            writer.WriteDynamicReferenceList(advance, context);
                            if (!advance)
                            {
                                break;
                            }
                        }
                    }
                    else if (lumaMode == Av1PredictionMode.NewMotionVector)
                    {
                        // NEWMV begins at stack entry zero and can advance through entries one and two.
                        for (int index = 0; index < 2 && referenceMotionVectors.Count > index + 1; index++)
                        {
                            bool advance = referenceMotionVectorIndex > index;
                            int context = Av1SymbolContextHelper.GetDrlContext(referenceMotionVectors.Weights, index);
                            writer.WriteDynamicReferenceList(advance, context);
                            if (!advance)
                            {
                                break;
                            }
                        }

                        Av1MotionVector vector = pcs.GetDisplacementVector(modeInfoPosition);
                        writer.WriteMotionVector(
                            vector,
                            referenceMotionVectors.GetNewReference(referenceMotionVectorIndex),
                            frm_hdr.MotionVectorPrecision);
                    }
                }

                if (UsesSwitchableInterpolation(frm_hdr, macroBlockModeInfo.Block))
                {
                    // The vertical symbol is first and supplies both axes unless the sequence enables dual filters.
                    int verticalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                        macroBlockModeInfo.Block,
                        macroBlock,
                        direction: 0);

                    writer.WriteSwitchableInterpolationFilter(macroBlockModeInfo.Block.VerticalInterpolationFilter, verticalContext);
                    if (scs.SequenceHeader.EnableDualFilter)
                    {
                        int horizontalContext = Av1SymbolContextHelper.GetSwitchableInterpolationContext(
                            macroBlockModeInfo.Block,
                            macroBlock,
                            direction: 1);

                        writer.WriteSwitchableInterpolationFilter(macroBlockModeInfo.Block.HorizontalInterpolationFilter, horizontalContext);
                    }
                }
            }
            else if (IsIntraBlockCopyAllowed(pcs.Parent.FrameHeader/*, pcs.Parent.SliceType*/))
            {
                WriteIntraBlockCopyInfo(
                    pcs,
                    writer,
                    macroBlock,
                    modeInfoPosition,
                    macroBlockModeInfo);
            }

            if (!isInterBlock && !macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                EncodeIntraLumaMode(
                    writer,
                    frm_hdr,
                    macroBlockModeInfo,
                    macroBlock,
                    ref blk_ptr,
                    blockSize,
                    lumaMode);
            }

            if (!isInterBlock && !macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                if (blk_ptr.HasChroma)
                {
                    EncodeIntraChromaMode(
                        writer,
                        frm_hdr,
                        scs.SequenceHeader.ColorConfig,
                        macroBlockModeInfo,
                        ref blk_ptr,
                        blockSize,
                        lumaMode,
                        intra_chroma_mode);
                }
            }

            bool paletteAllowed = !isInterBlock &&
                !macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsPaletteAllowed(frm_hdr.AllowScreenContentTools, blockSize);

            if (paletteAllowed)
            {
                WritePaletteModeInfo(
                    scs,
                    pcs,
                    writer,
                    macroBlock,
                    macroBlockModeInfo,
                    ref paletteInfo,
                    blockSize,
                    blockOrigin,
                    tile_idx,
                    blk_ptr.HasChroma);
            }

            if (!isInterBlock &&
                !macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsFilterIntraAllowed(
                    scs.SequenceHeader.EnableFilterIntra,
                    blockSize,
                    paletteInfo.PaletteSizes[0],
                    lumaMode))
            {
                writer.WriteFilterIntraMode(blk_ptr.FilterIntraMode, blockSize);
            }

            if (paletteAllowed)
            {
                ObuColorConfig colorConfig = scs.SequenceHeader.ColorConfig;
                int palettePlaneCount = Math.Min(2, colorConfig.PlaneCount);
                for (int plane = 0; plane < palettePlaneCount; ++plane)
                {
                    int paletteSize = paletteInfo.PaletteSizes[plane];
                    if (paletteSize == 0)
                    {
                        continue;
                    }

                    Av1PlaneType planeType = (Av1PlaneType)plane;
                    int subX = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingX ? 1 : 0;
                    int subY = planeType == Av1PlaneType.Uv && colorConfig.SubSamplingY ? 1 : 0;
                    int blockWidth = blockSize.GetWidth();
                    int blockHeight = blockSize.GetHeight();
                    int planeWidth = blockWidth >> subX;
                    int planeHeight = blockHeight >> subY;

                    // Palette syntax covers coded alignment samples too. Visible-frame clipping would omit symbols
                    // that the decoder consumes before transform syntax and corrupt the remainder of the tile.
                    int columns = (blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3)) >> subX;
                    int rows = (blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3)) >> subY;
                    Buffer2DRegion<byte> colorIndexMap = tb_ptr.Workspace
                        .GetPaletteMaps()
                        .GetMap(planeType, planeWidth, planeHeight);

                    writer.WritePaletteColorMap(
                        paletteSize,
                        planeType,
                        rows,
                        columns,
                        colorIndexMap);
                }
            }

            WriteTransformSize(
                pcs,
                writer,
                ref macroBlockModeInfo,
                macroBlock,
                blockSize,
                blockOrigin,
                tile_idx);

            entropyCodingContext.MacroBlockModeInfo = macroBlockModeInfo;
            if (!skipWritingCoefficients)
            {
                EncodeCoefficients1d(
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref blk_ptr,
                    blockOrigin,
                    lumaMode,
                    blockSize,
                    coefficientBuffer,
                    tb_ptr.Index,
                    luma_dc_sign_level_coeff_na,
                    cr_dc_sign_level_coeff_na,
                    cb_dc_sign_level_coeff_na);
            }
        }

        // Palette colors are published only after the current block's mode and map have consumed the preceding edges.
        if (frm_hdr.AllowScreenContentTools)
        {
            const Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask PaletteContextMask =
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Left |
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Top;

            pcs.PaletteContexts[tile_idx].UnitModeWrite(
                paletteInfo,
                blockOrigin,
                new Size(blockSize.GetWidth(), blockSize.GetHeight()),
                PaletteContextMask);
        }

        // Coefficient neighbor state follows the same post-symbol ownership boundary.
        UpdateNeighbors(pcs, entropyCodingContext, blockOrigin, ref blk_ptr, tile_idx, blockSize);
    }

    /// <summary>
    /// Derives the uniform intra transform-size context from the current above and left edges.
    /// </summary>
    /// <param name="transformContexts">The retained transform widths and heights.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blockSize">The block size defining the maximum transform.</param>
    /// <returns>The uniform transform-size context.</returns>
    public static int GetTransformSizeContext(
        Av1NeighborArrayUnit<byte> transformContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin,
        Av1BlockSize blockSize)
    {
        Av1TransformSize maximumTransformSize = blockSize.GetMaximumTransformSize();
        int above = transformContexts.Top[transformContexts.GetTopIndex(blockOrigin)] >= maximumTransformSize.GetWidth() ? 1 : 0;
        int left = transformContexts.Left[transformContexts.GetLeftIndex(blockOrigin)] >= maximumTransformSize.GetHeight() ? 1 : 0;

        // Inter neighbors contribute their coding-block extent, not their residual-transform extent.
        if (macroBlock.IsUpAvailable)
        {
            ref Av1MacroBlockModeInfo aboveModeInfo =
                ref macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride);

            if (aboveModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
                aboveModeInfo.Block.UseIntraBlockCopy)
            {
                above = aboveModeInfo.Block.BlockSize.GetWidth() >= maximumTransformSize.GetWidth() ? 1 : 0;
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            ref Av1MacroBlockModeInfo leftModeInfo = ref macroBlock.GetRelativeModeInfo(-1);
            if (leftModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
                leftModeInfo.Block.UseIntraBlockCopy)
            {
                left = leftModeInfo.Block.BlockSize.GetHeight() >= maximumTransformSize.GetHeight() ? 1 : 0;
            }
        }

        return macroBlock.IsUpAvailable
            ? macroBlock.IsLeftAvailable ? above + left : above
            : macroBlock.IsLeftAvailable ? left : 0;
    }

    /// <summary>
    /// Writes or derives the block transform size and publishes its edge contexts.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    internal static void WriteTransformSize(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        ref Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1MacroBlockD macroBlock,
        Av1BlockSize blockSize,
        Point blockOrigin,
        int tileIndex)
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        bool isLossless = frameHeader.LosslessArray[macroBlockModeInfo.Block.SegmentId];
        bool isInter = macroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
            macroBlockModeInfo.Block.UseIntraBlockCopy;
        bool writesUniformTransformSize = !isLossless &&
            frameHeader.TransformMode == Av1TransformMode.Select &&
            !isInter &&
            blockSize > Av1BlockSize.Block4x4;
        bool writesVariableTransformSize = !isLossless &&
            frameHeader.TransformMode == Av1TransformMode.Select &&
            isInter &&
            !macroBlockModeInfo.Block.Skip &&
            blockSize > Av1BlockSize.Block4x4;

        Av1TransformSize transformSize = isLossless
            ? Av1TransformSize.Size4x4
            : writesUniformTransformSize || writesVariableTransformSize
                ? macroBlockModeInfo.Block.TransformSize
                : blockSize.GetMaximumTransformSize();

        macroBlockModeInfo.Block.TransformSize = transformSize;
        Av1NeighborArrayUnit<byte> transformContexts = pcs.TransformFunctionContexts[tileIndex];
        if (writesUniformTransformSize)
        {
            int context = GetTransformSizeContext(transformContexts, macroBlock, blockOrigin, blockSize);
            writer.WriteTransformSize(blockSize, transformSize, context);
        }
        else if (writesVariableTransformSize)
        {
            int topIndex = transformContexts.GetTopIndex(blockOrigin);
            int leftIndex = transformContexts.GetLeftIndex(blockOrigin);
            int context = Av1SymbolContextHelper.GetTransformPartitionContext(
                transformContexts.Top[topIndex],
                transformContexts.Left[leftIndex],
                blockSize,
                blockSize.GetMaximumTransformSize());

            // Current inter decisions retain the maximum transform, so their variable-transform tree has one unsplit root.
            writer.WriteTransformPartition(false, context);
        }

        Size blockDimensions = new(blockSize.GetWidth(), blockSize.GetHeight());

        // Above entries retain transform widths and left entries retain heights, including rectangular selections.
        transformContexts.UnitModeWrite(
            (byte)transformSize.GetWidth(),
            blockOrigin,
            blockDimensions,
            Av1NeighborArrayUnit<byte>.UnitMask.Top);

        transformContexts.UnitModeWrite(
            (byte)transformSize.GetHeight(),
            blockOrigin,
            blockDimensions,
            Av1NeighborArrayUnit<byte>.UnitMask.Left);
    }

    /// <summary>
    /// Gets the chroma prediction-mode and directional-angle rate from the live tile probabilities.
    /// </summary>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="frameHeader">The current frame syntax and segment lossless state.</param>
    /// <param name="colorConfig">The sequence chroma subsampling configuration.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <param name="chromaMode">The candidate chroma prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <returns>The chroma mode and directional-angle rate in 1/512-bit units.</returns>
    public static int GetChromaModeCost(
        Av1SymbolEncoder writer,
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode,
        Av1ChromaPredictionMode chromaMode,
        int angleDelta)
    {
        bool isChromaFromLumaAllowed = IsChromaFromLumaAllowed(
            frameHeader,
            colorConfig,
            macroBlockModeInfo,
            blockSize);

        int cost = writer.GetChromaModeCost(chromaMode, isChromaFromLumaAllowed, lumaMode);
        if (blockSize >= Av1BlockSize.Block8x8 && chromaMode.IsDirectional())
        {
            cost += writer.GetAngleDeltaCost(angleDelta + Av1Constants.MaxAngleDelta, chromaMode.ToLumaMode());
        }

        return cost;
    }

    /// <summary>
    /// Writes the chroma intra mode, chroma-from-luma alpha values, and directional angle adjustment for a block.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="frameHeader">The current frame syntax and segment lossless state.</param>
    /// <param name="colorConfig">The sequence chroma subsampling configuration.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <param name="chromaMode">The selected chroma prediction mode.</param>
    public static void EncodeIntraChromaMode(
        Av1SymbolEncoder writer,
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode,
        Av1ChromaPredictionMode chromaMode)
    {
        bool isChromaFromLumaAllowed = IsChromaFromLumaAllowed(
            frameHeader,
            colorConfig,
            macroBlockModeInfo,
            blockSize);

        writer.WriteChromaMode(chromaMode, isChromaFromLumaAllowed, lumaMode);

        if (chromaMode == Av1ChromaPredictionMode.ChromaFromLuma)
        {
            writer.WriteChromaFromLumaAlphas(
                blk_ptr.PredictionUnit.ChromaFromLumaIndex,
                blk_ptr.PredictionUnit.ChromaFromLumaSigns);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.UvMode.IsDirectional())
        {
            writer.WriteAngleDelta(
                blk_ptr.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] + Av1Constants.MaxAngleDelta,
                chromaMode.ToLumaMode());
        }
    }

    private static bool IsChromaFromLumaAllowed(
        ObuFrameHeader frameHeader,
        ObuColorConfig colorConfig,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1BlockSize blockSize)
        => blockSize.AllowsChromaFromLuma(
            frameHeader.LosslessArray[macroBlockModeInfo.Block.SegmentId],
            colorConfig.SubSamplingX,
            colorConfig.SubSamplingY);

    /// <summary>
    /// Gets the above and left key-frame contexts used to write an intra luma mode.
    /// </summary>
    /// <param name="xd">The current macroblock and its mapped neighbors.</param>
    /// <param name="above_ctx">The context derived from the above luma mode.</param>
    /// <param name="left_ctx">The context derived from the left luma mode.</param>
    private static void GetYModeContext(Av1MacroBlockD xd, out byte above_ctx, out byte left_ctx)
    {
        Av1PredictionMode intraLumaLeftMode = Av1PredictionMode.DC;
        Av1PredictionMode intraLumaTopMode = Av1PredictionMode.DC;
        if (xd.IsLeftAvailable)
        {
            // Key-frame neighbors are intra blocks, so their luma modes directly select the context class.
            intraLumaLeftMode = xd.GetRelativeModeInfo(-1).Block.Mode;
        }

        if (xd.IsUpAvailable)
        {
            intraLumaTopMode = xd.GetRelativeModeInfo(-xd.ModeInfoStride).Block.Mode;
        }

        above_ctx = IntraModeContextLookup[(int)intraLumaTopMode];
        left_ctx = IntraModeContextLookup[(int)intraLumaLeftMode];
    }

    /// <summary>
    /// Gets the luma mode rate from the frame-appropriate distribution.
    /// </summary>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="blockSize">The selected block size.</param>
    /// <param name="mode">The candidate luma mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="isIntraFrame">Whether the frame uses key-frame neighbor-conditioned mode syntax.</param>
    /// <returns>The luma mode and directional-angle rate in 1/512-bit units.</returns>
    public static int GetLumaModeCost(
        Av1SymbolEncoder writer,
        Av1MacroBlockD macroBlock,
        Av1BlockSize blockSize,
        Av1PredictionMode mode,
        int angleDelta,
        bool isIntraFrame)
    {
        int cost;
        if (isIntraFrame)
        {
            GetYModeContext(macroBlock, out byte topContext, out byte leftContext);
            cost = writer.GetLumaModeCost(mode, topContext, leftContext);
        }
        else
        {
            cost = writer.GetInterFrameLumaModeCost(mode, blockSize);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && mode.IsDirectional())
        {
            cost += writer.GetAngleDeltaCost(angleDelta + Av1Constants.MaxAngleDelta, mode);
        }

        return cost;
    }

    /// <summary>
    /// Writes the frame-appropriate luma prediction mode and any directional angle adjustment.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="frameHeader">The frame header that selects the luma-mode probability model.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    private static void EncodeIntraLumaMode(
        Av1SymbolEncoder writer,
        ObuFrameHeader frameHeader,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode)
    {
        if (frameHeader.IsIntra)
        {
            GetYModeContext(macroBlock, out byte topContext, out byte leftContext);
            writer.WriteLumaMode(lumaMode, topContext, leftContext);
        }
        else
        {
            writer.WriteInterFrameLumaMode(lumaMode, blockSize);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.Mode.IsDirectional())
        {
            writer.WriteAngleDelta(blk_ptr.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] + Av1Constants.MaxAngleDelta, lumaMode);
        }
    }

    /// <summary>
    /// Gets the prediction-domain context from the immediately above and left encoder blocks.
    /// </summary>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <returns>The context in the inclusive range zero through three.</returns>
    public static int GetIntraInterContext(Av1MacroBlockD macroBlock)
    {
        bool hasAbove = macroBlock.IsUpAvailable;
        bool hasLeft = macroBlock.IsLeftAvailable;
        if (hasAbove && hasLeft)
        {
            bool aboveIsIntra = macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra;

            bool leftIsIntra = macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra;

            if (aboveIsIntra && leftIsIntra)
            {
                return 3;
            }

            return aboveIsIntra || leftIsIntra ? 1 : 0;
        }

        if (hasAbove)
        {
            return macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra
                    ? 2
                    : 0;
        }

        if (hasLeft)
        {
            return macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame <= Av1ReferenceFrameType.Intra
                    ? 2
                    : 0;
        }

        return 0;
    }

    /// <summary>
    /// Counts the single-reference labels used by the immediately above and left encoded blocks.
    /// </summary>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="referenceCounts">The eight-entry destination indexed by reference-frame label.</param>
    public static void CollectNeighborReferenceCounts(
        Av1MacroBlockD macroBlock,
        Span<byte> referenceCounts)
    {
        // The caller supplies short-lived fixed storage for one block. Clearing it here keeps unavailable
        // neighbors from retaining votes collected for a preceding block.
        referenceCounts.Clear();
        if (macroBlock.IsUpAvailable)
        {
            Av1ReferenceFrameType referenceFrame = macroBlock
                .GetRelativeModeInfo(-macroBlock.ModeInfoStride)
                .Block.ReferenceFrame;

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)referenceFrame]++;
            }
        }

        if (macroBlock.IsLeftAvailable)
        {
            Av1ReferenceFrameType referenceFrame = macroBlock
                .GetRelativeModeInfo(-1)
                .Block.ReferenceFrame;

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                referenceCounts[(int)referenceFrame]++;
            }
        }
    }

    /// <summary>
    /// Writes luma and chroma palette-mode syntax for a block.
    /// </summary>
    /// <param name="scs">The sequence coding state.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The current block's mapped neighbor state.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="paletteInfo">The selected palette sizes and colors.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="hasChroma">Whether the block owns chroma syntax.</param>
    internal static void WritePaletteModeInfo(
        Av1SequenceControlSet scs,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1MacroBlockD macroBlock,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        ref Av1EncoderPaletteInfo paletteInfo,
        Av1BlockSize blockSize,
        Point blockOrigin,
        int tileIndex,
        bool hasChroma)
    {
        int blockSizeContext = GetPaletteBlockSizeContext(blockSize);
        Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = pcs.PaletteContexts[tileIndex];
        int yPaletteSize = paletteInfo.PaletteSizes[0];
        if (macroBlockModeInfo.Block.Mode == Av1PredictionMode.DC)
        {
            int neighborContext = GetPaletteYModeContext(paletteContexts, macroBlock, blockOrigin);
            writer.WritePaletteYMode(yPaletteSize != 0, blockSizeContext, neighborContext);
            if (yPaletteSize != 0)
            {
                writer.WritePaletteSize(yPaletteSize, blockSizeContext, Av1PlaneType.Y);
                Span<ushort> colorCache = stackalloc ushort[2 * Av1Constants.PaletteMaxSize];
                int cacheSize = GetPaletteCache(
                    paletteContexts,
                    macroBlock,
                    blockOrigin,
                    Av1Plane.Y,
                    colorCache);

                writer.WritePaletteYColors(
                    colorCache[..cacheSize],
                    paletteInfo.GetColors(Av1Plane.Y),
                    scs.SequenceHeader.ColorConfig.BitDepth.GetBitCount());
            }
        }

        int uvPaletteSize = paletteInfo.PaletteSizes[1];
        if (scs.SequenceHeader.ColorConfig.PlaneCount > 1 &&
            macroBlockModeInfo.Block.UvMode == Av1ChromaPredictionMode.DC &&
            hasChroma)
        {
            writer.WritePaletteUvMode(uvPaletteSize != 0, yPaletteSize != 0);
            if (uvPaletteSize != 0)
            {
                writer.WritePaletteSize(uvPaletteSize, blockSizeContext, Av1PlaneType.Uv);
                Span<ushort> colorCache = stackalloc ushort[2 * Av1Constants.PaletteMaxSize];
                int cacheSize = GetPaletteCache(
                    paletteContexts,
                    macroBlock,
                    blockOrigin,
                    Av1Plane.U,
                    colorCache);

                writer.WritePaletteUvColors(
                    colorCache[..cacheSize],
                    paletteInfo.GetColors(Av1Plane.U),
                    paletteInfo.GetColors(Av1Plane.V),
                    scs.SequenceHeader.ColorConfig.BitDepth.GetBitCount());
            }
        }
    }

    /// <summary>
    /// Builds the sorted palette-color cache from the available above and left encoder edges.
    /// </summary>
    internal static int GetPaletteCache(
        Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin,
        Av1Plane plane,
        Span<ushort> cache)
    {
        // AV1 excludes the above palette at each 64-sample row boundary, even with 128x128 superblocks.
        bool hasAbove = macroBlock.IsUpAvailable &&
            (blockOrigin.Y & (Av1BlockSize.Block64x64.GetHeight() - 1)) != 0;

        Av1EncoderPaletteInfo above = hasAbove
            ? paletteContexts.Top[paletteContexts.GetTopIndex(blockOrigin)]
            : default;

        Av1EncoderPaletteInfo left = macroBlock.IsLeftAvailable
            ? paletteContexts.Left[paletteContexts.GetLeftIndex(blockOrigin)]
            : default;

        ReadOnlySpan<ushort> aboveColors = hasAbove ? above.GetColors(plane) : [];
        ReadOnlySpan<ushort> leftColors = macroBlock.IsLeftAvailable ? left.GetColors(plane) : [];
        return Av1PaletteCache.Merge(aboveColors, leftColors, cache);
    }

    /// <summary>
    /// Gets the palette probability context derived from the logarithmic block area.
    /// </summary>
    internal static int GetPaletteBlockSizeContext(Av1BlockSize blockSize)
        => Av1Math.Log2(blockSize.GetWidth() * blockSize.GetHeight()) - 6;

    /// <summary>
    /// Counts the available above and left luma neighbors that selected palette mode.
    /// </summary>
    internal static int GetPaletteYModeContext(
        Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts,
        Av1MacroBlockD macroBlock,
        Point blockOrigin)
    {
        int neighborContext = 0;
        if (macroBlock.IsUpAvailable &&
            paletteContexts.Top[paletteContexts.GetTopIndex(blockOrigin)].PaletteSizes[0] != 0)
        {
            neighborContext++;
        }

        if (macroBlock.IsLeftAvailable &&
            paletteContexts.Left[paletteContexts.GetLeftIndex(blockOrigin)].PaletteSizes[0] != 0)
        {
            neighborContext++;
        }

        return neighborContext;
    }

    /// <summary>
    /// Determines whether filter-intra syntax is available for a block mode.
    /// </summary>
    /// <param name="enableFilterIntra">A value indicating whether the sequence enables filter-intra prediction.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="paletteSize">The selected luma palette size.</param>
    /// <param name="mode">The selected luma prediction mode.</param>
    /// <returns><see langword="true"/> when the block can use filter-intra prediction; otherwise, <see langword="false"/>.</returns>
    private static bool IsFilterIntraAllowed(
        bool enableFilterIntra,
        Av1BlockSize blockSize,
        int paletteSize,
        Av1PredictionMode mode)
        => mode == Av1PredictionMode.DC && paletteSize == 0 && IsFilterIntraAllowedBlockSize(enableFilterIntra, blockSize);

    /// <summary>
    /// Determines whether filter-intra prediction is enabled for a block size.
    /// </summary>
    /// <param name="enableFilterIntra">A value indicating whether the sequence enables filter-intra prediction.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when filter-intra prediction supports the block dimensions; otherwise, <see langword="false"/>.</returns>
    internal static bool IsFilterIntraAllowedBlockSize(bool enableFilterIntra, Av1BlockSize blockSize)
    {
        if (!enableFilterIntra)
        {
            return false;
        }

        return blockSize.GetWidth() <= 32 && blockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Writes the intra-block-copy selection and displacement-vector syntax for a block.
    /// </summary>
    /// <param name="picture">The frame-owned mode and displacement state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The current block's frame edges and tile availability.</param>
    /// <param name="modeInfoPosition">The block origin in 4x4 mode-information units.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    public static void WriteIntraBlockCopyInfo(
        Av1PictureControlSet picture,
        Av1SymbolEncoder writer,
        Av1MacroBlockD macroBlock,
        Point modeInfoPosition,
        Av1MacroBlockModeInfo macroBlockModeInfo)
    {
        bool useIntraBlockCopy = macroBlockModeInfo.Block.UseIntraBlockCopy;
        writer.WriteUseIntraBlockCopy(useIntraBlockCopy);
        if (useIntraBlockCopy)
        {
            Span<Av1MotionVector> candidates = stackalloc Av1MotionVector[8];
            Span<int> weights = stackalloc int[8];
            Av1MotionVector reference = Av1IntraBlockCopy.FindReference(
                picture,
                macroBlock,
                modeInfoPosition,
                macroBlockModeInfo.Block.BlockSize,
                macroBlockModeInfo.Block.PartitionType,
                candidates,
                weights);

            writer.WriteDisplacementVector(
                picture.GetDisplacementVector(modeInfoPosition),
                reference);
        }
    }

    /// <summary>
    /// Determines whether a single-reference encoder block carries switchable interpolation symbols.
    /// </summary>
    /// <param name="frameHeader">The current frame header.</param>
    /// <param name="modeInfo">The selected block syntax.</param>
    /// <returns>Whether the block writes a vertical filter and, when enabled, a horizontal filter.</returns>
    public static bool UsesSwitchableInterpolation(ObuFrameHeader frameHeader, Av1EncoderBlockModeInfo modeInfo)
    {
        if (frameHeader.InterpolationFilter != Av1InterpolationFilter.Switchable || modeInfo.SkipMode)
        {
            return false;
        }

        // Global identity and affine models infer the regular filter on blocks at least 8x8. Translation still
        // carries filter symbols, including integer translations. Residual skip does not suppress these symbols.
        return modeInfo.Mode != Av1PredictionMode.GlobalMotionVector ||
            Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) < Av1BlockSize.Block8x8.GetWidth() ||
            frameHeader.GetGlobalMotionParameters()[(int)modeInfo.ReferenceFrame - 1].Type == Av1GlobalMotionType.Translation;
    }

    /// <summary>
    /// Determines whether the current frame permits intra block copy.
    /// </summary>
    /// <param name="frameHeader">The current frame header.</param>
    /// <returns><see langword="true"/> when both screen-content tools and intra block copy are enabled; otherwise, <see langword="false"/>.</returns>
    private static bool IsIntraBlockCopyAllowed(ObuFrameHeader frameHeader)
        => frameHeader.AllowScreenContentTools && frameHeader.AllowIntraBlockCopy;

    /// <summary>
    /// Updates coefficient neighbor arrays after writing a block.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="tile_idx">The zero-based tile index.</param>
    /// <param name="blockSize">The block size.</param>
    private static void UpdateNeighbors(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Point blockOrigin,
        ref Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Av1BlockSize blockSize)
    {
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na = pcs.LuminanceDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na = pcs.CrDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na = pcs.CbDcSignLevelCoefficientNeighbors[tile_idx];
        Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
        ref Av1MacroBlockModeInfo mbmi = ref pcs.GetMacroBlockModeInfo(modeInfoPosition);
        bool skip_coeff = mbmi.Block.Skip;

        Size size = new(blockSize.GetWidth(), blockSize.GetHeight());
        if (skip_coeff)
        {
            // A skipped block has an all-zero residual, so publish a zero sign/level context over its edges
            // and advance coefficient positions without reading transform units.
            luma_dc_sign_level_coeff_na.UnitModeWrite(
                0,
                blockOrigin,
                size,
                Av1NeighborArrayUnit<byte>.UnitMask.Left | Av1NeighborArrayUnit<byte>.UnitMask.Top);

            ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
            if (blk_ptr.HasChroma && !colorConfig.IsMonochrome)
            {
                int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
                Point chromaOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);
                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
                Size chromaSize = new(chromaBlockSize.GetWidth(), chromaBlockSize.GetHeight());

                cb_dc_sign_level_coeff_na.UnitModeWrite(
                    0,
                    chromaOrigin,
                    chromaSize,
                    Av1NeighborArrayUnit<byte>.UnitMask.Left | Av1NeighborArrayUnit<byte>.UnitMask.Top);
                cr_dc_sign_level_coeff_na.UnitModeWrite(
                    0,
                    chromaOrigin,
                    chromaSize,
                    Av1NeighborArrayUnit<byte>.UnitMask.Left | Av1NeighborArrayUnit<byte>.UnitMask.Top);
                entropyCodingContext.CodedAreaSuperblockUv += chromaSize.Width * chromaSize.Height;
            }

            entropyCodingContext.CodedAreaSuperblock += size.Width * size.Height;
        }
    }

    /// <summary>
    /// Determines whether screen-content tools and block dimensions permit palette mode.
    /// </summary>
    /// <param name="allowScreenContentTools">A value indicating whether screen-content tools are enabled.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when palette mode is available for the block; otherwise, <see langword="false"/>.</returns>
    internal static bool IsPaletteAllowed(bool allowScreenContentTools, Av1BlockSize blockSize)
        => allowScreenContentTools &&
            blockSize.GetWidth() <= 64 &&
            blockSize.GetHeight() <= 64 &&
            blockSize >= Av1BlockSize.Block8x8;

    /// <summary>
    /// Writes the constrained directional enhancement filter strength at its first coded block in a filter unit.
    /// </summary>
    /// <param name="scs">The sequence coding state.</param>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="skip">A value indicating whether the current block omits residual coefficients.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    internal static void WriteCdef(
        Av1SequenceControlSet scs,
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        int tileIndex,
        bool skip,
        Point modeInfoPosition)
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;

        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        Span<int> cdefPreset = pcs.CdefPreset.Span.Slice(
            tileIndex * Av1Constants.CdefUnitsPerSuperblock,
            Av1Constants.CdefUnitsPerSuperblock);

        // Each superblock begins with all contained 64x64 filter units unassigned.
        if ((modeInfoPosition.Y & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0 &&
            (modeInfoPosition.X & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0)
        {
            cdefPreset.Fill(-1);
        }

        // The strength is coded once, at the first non-skipped block in each 64x64 CDEF filter unit.
        int cdefSize = 1 << (6 - Av1Constants.ModeInfoSizeLog2);
        int unitColumn = (modeInfoPosition.X & cdefSize) != 0 ? 1 : 0;
        int unitRow = (modeInfoPosition.Y & cdefSize) != 0 ? 1 : 0;
        int index = scs.SequenceHeader.Use128x128Superblock ? unitColumn + (2 * unitRow) : 0;

        if (cdefPreset[index] == -1 && !skip)
        {
            int firstBlockMask = ~(cdefSize - 1);
            Point firstBlockPosition = new(
                modeInfoPosition.X & firstBlockMask,
                modeInfoPosition.Y & firstBlockMask);
            ref Av1MacroBlockModeInfo firstBlock = ref pcs.GetFromModeInfoGrid(firstBlockPosition);

            // CDEF strength belongs to the first mode-info block in the 64x64 filter unit even when skipped
            // blocks delay transmission until a later coding block.
            writer.WriteCdefStrength(firstBlock.CdefStrength, frameHeader.CdefParameters.BitCount);
            cdefPreset[index] = firstBlock.CdefStrength;
        }
    }

    /// <summary>
    /// Populates a macroblock's frame edges, tile-neighbor availability, and rectangular-partition context.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="macroBlock">The macroblock state to populate.</param>
    /// <param name="tile">The active tile boundaries.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="modeInfoStride">The row stride of the mode-information grid.</param>
    /// <param name="modeInfoRowCount">The coded frame height in mode-information rows.</param>
    /// <param name="modeInfoColumnCount">The coded frame width in mode-information columns.</param>
    internal static void SetModeInfoRowAndColumn(
        Av1PictureControlSet pcs,
        Av1MacroBlockD macroBlock,
        Av1TileInfo tile,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        int modeInfoStride,
        int modeInfoRowCount,
        int modeInfoColumnCount)
    {
        macroBlock.ToTopEdge = -((modeInfoPosition.Y << Av1Constants.ModeInfoSizeLog2) << 3);
        int blockModeInfoHeight = blockSize.Get4x4HighCount();
        int blockModeInfoWidth = blockSize.Get4x4WideCount();
        macroBlock.ToBottomEdge = ((modeInfoRowCount - blockModeInfoHeight - modeInfoPosition.Y) << Av1Constants.ModeInfoSizeLog2) << 3;
        macroBlock.ToLeftEdge = -((modeInfoPosition.X << Av1Constants.ModeInfoSizeLog2) << 3);
        macroBlock.ToRightEdge = ((modeInfoColumnCount - blockModeInfoWidth - modeInfoPosition.X) << Av1Constants.ModeInfoSizeLog2) << 3;

        macroBlock.ModeInfoStride = modeInfoStride;

        // Prediction cannot cross tile boundaries even when frame mode information exists there.
        macroBlock.IsUpAvailable = modeInfoPosition.Y > tile.ModeInfoRowStart;
        macroBlock.IsLeftAvailable = modeInfoPosition.X > tile.ModeInfoColumnStart;
        int modeInfoIndex = (modeInfoPosition.Y * modeInfoStride) + modeInfoPosition.X;
        macroBlock.SetModeInfoGrid(pcs.ModeInfoGrid, pcs.ModeInfoAllocation, modeInfoIndex);
    }

    /// <summary>
    /// Writes luma and chroma transform coefficients for a block in plane order.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="ec_ctx">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction.</param>
    /// <param name="planeBlockSize">The luma block size.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="superblockIndex">The raster-ordered index of the containing superblock.</param>
    /// <param name="luma_dc_sign_level_coeff_na">The luma coefficient neighbor contexts.</param>
    /// <param name="cr_dc_sign_level_coeff_na">The red-difference chroma coefficient neighbor contexts.</param>
    /// <param name="cb_dc_sign_level_coeff_na">The blue-difference chroma coefficient neighbor contexts.</param>
    private static void EncodeCoefficients1d(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize planeBlockSize,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na)
    {
        EncodeTransformCoefficientRegions(
            pcs,
            ec_ctx,
            writer,
            ref blk_ptr,
            blockOrigin,
            intraLumaDir,
            planeBlockSize,
            coefficientBuffer,
            superblockIndex,
            luma_dc_sign_level_coeff_na,
            cr_dc_sign_level_coeff_na,
            cb_dc_sign_level_coeff_na);
    }

    /// <summary>
    /// Writes each luma transform block and updates its DC-sign and coefficient-level neighbor contexts.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction.</param>
    /// <param name="plane_bsize">The luma block size.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="superblockIndex">The raster-ordered index of the containing superblock.</param>
    /// <param name="luma_dc_sign_level_coeff_na">The luma coefficient neighbor contexts.</param>
    public static void EncodeTransformCoefficientsY(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize plane_bsize,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na)
    {
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;
        int maximumBlocksWide = plane_bsize.GetWidth();
        int maximumBlocksHigh = plane_bsize.GetHeight();
        if (macroBlock.ToRightEdge < 0)
        {
            maximumBlocksWide += macroBlock.ToRightEdge >> 3;
        }

        if (macroBlock.ToBottomEdge < 0)
        {
            maximumBlocksHigh += macroBlock.ToBottomEdge >> 3;
        }

        maximumBlocksWide >>= Av1Constants.ModeInfoSizeLog2;
        maximumBlocksHigh >>= Av1Constants.ModeInfoSizeLog2;
        int maximumUnitBlocksWide = Math.Min(
            Av1BlockSize.Block64x64.Get4x4WideCount(),
            maximumBlocksWide);

        int maximumUnitBlocksHigh = Math.Min(
            Av1BlockSize.Block64x64.Get4x4HighCount(),
            maximumBlocksHigh);

        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitBottom = Math.Min(regionRow + maximumUnitBlocksHigh, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitRight = Math.Min(regionColumn + maximumUnitBlocksWide, maximumBlocksWide);
                EncodeTransformCoefficientRegion(
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref blk_ptr,
                    blockOrigin,
                    intraLumaDir,
                    plane_bsize,
                    Av1Plane.Y,
                    coefficientBuffer,
                    superblockIndex,
                    luma_dc_sign_level_coeff_na,
                    regionRow,
                    regionColumn,
                    unitBottom,
                    unitRight);
            }
        }
    }

    /// <summary>
    /// Writes both chroma transform blocks and updates their DC-sign and coefficient-level neighbor contexts.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The luma block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction used by coefficient contexts.</param>
    /// <param name="plane_bsize">The luma block size.</param>
    /// <param name="coefficientBuffer">The transformed coefficients retained by raster-ordered superblock.</param>
    /// <param name="superblockIndex">The raster-ordered index of the containing superblock.</param>
    /// <param name="cr_dc_sign_level_coeff_na">The red-difference chroma coefficient neighbor contexts.</param>
    /// <param name="cb_dc_sign_level_coeff_na">The blue-difference chroma coefficient neighbor contexts.</param>
    private static void EncodeTransformCoefficientsUv(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize plane_bsize,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na)
    {
        ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
        if (!blk_ptr.HasChroma || colorConfig.IsMonochrome)
        {
            return;
        }

        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        Av1BlockSize chromaBlockSize = plane_bsize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
        Point chromaBlockOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;
        int maximumBlocksWide = chromaBlockSize.GetWidth();
        int maximumBlocksHigh = chromaBlockSize.GetHeight();
        if (macroBlock.ToRightEdge < 0)
        {
            maximumBlocksWide += macroBlock.ToRightEdge >> (3 + subsamplingX);
        }

        if (macroBlock.ToBottomEdge < 0)
        {
            maximumBlocksHigh += macroBlock.ToBottomEdge >> (3 + subsamplingY);
        }

        maximumBlocksWide >>= Av1Constants.ModeInfoSizeLog2;
        maximumBlocksHigh >>= Av1Constants.ModeInfoSizeLog2;
        Av1BlockSize maximumUnitBlockSize =
            Av1BlockSize.Block64x64.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        int maximumUnitBlocksWide = Math.Min(maximumUnitBlockSize.Get4x4WideCount(), maximumBlocksWide);
        int maximumUnitBlocksHigh = Math.Min(maximumUnitBlockSize.Get4x4HighCount(), maximumBlocksHigh);

        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitBottom = Math.Min(regionRow + maximumUnitBlocksHigh, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitRight = Math.Min(regionColumn + maximumUnitBlocksWide, maximumBlocksWide);
                EncodeTransformCoefficientRegion(
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref blk_ptr,
                    chromaBlockOrigin,
                    intraLumaDir,
                    plane_bsize,
                    Av1Plane.U,
                    coefficientBuffer,
                    superblockIndex,
                    cb_dc_sign_level_coeff_na,
                    regionRow,
                    regionColumn,
                    unitBottom,
                    unitRight);

                EncodeTransformCoefficientRegion(
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref blk_ptr,
                    chromaBlockOrigin,
                    intraLumaDir,
                    plane_bsize,
                    Av1Plane.V,
                    coefficientBuffer,
                    superblockIndex,
                    cr_dc_sign_level_coeff_na,
                    regionRow,
                    regionColumn,
                    unitBottom,
                    unitRight);
            }
        }
    }

    private static void EncodeTransformCoefficientRegions(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct block,
        Point blockOrigin,
        Av1PredictionMode intraLumaMode,
        Av1BlockSize blockSize,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        Av1NeighborArrayUnit<byte> lumaCoefficientNeighbors,
        Av1NeighborArrayUnit<byte> redCoefficientNeighbors,
        Av1NeighborArrayUnit<byte> blueCoefficientNeighbors)
    {
        Av1MacroBlockD macroBlock = entropyCodingContext.MacroBlock;
        int maximumBlocksWide = blockSize.GetWidth();
        int maximumBlocksHigh = blockSize.GetHeight();
        if (macroBlock.ToRightEdge < 0)
        {
            maximumBlocksWide += macroBlock.ToRightEdge >> 3;
        }

        if (macroBlock.ToBottomEdge < 0)
        {
            maximumBlocksHigh += macroBlock.ToBottomEdge >> 3;
        }

        maximumBlocksWide >>= Av1Constants.ModeInfoSizeLog2;
        maximumBlocksHigh >>= Av1Constants.ModeInfoSizeLog2;
        int maximumUnitBlocksWide = Math.Min(
            Av1BlockSize.Block64x64.Get4x4WideCount(),
            maximumBlocksWide);

        int maximumUnitBlocksHigh = Math.Min(
            Av1BlockSize.Block64x64.Get4x4HighCount(),
            maximumBlocksHigh);

        ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
        bool hasChroma = block.HasChroma && !colorConfig.IsMonochrome;
        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        Point chromaBlockOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);

        // Residual syntax is region-major, then plane-major. Keeping the three plane calls together
        // prevents a 128x128 block from emitting later luma regions before earlier chroma regions.
        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitBottom = Math.Min(regionRow + maximumUnitBlocksHigh, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitRight = Math.Min(regionColumn + maximumUnitBlocksWide, maximumBlocksWide);
                EncodeTransformCoefficientRegion(
                    pcs,
                    entropyCodingContext,
                    writer,
                    ref block,
                    blockOrigin,
                    intraLumaMode,
                    blockSize,
                    Av1Plane.Y,
                    coefficientBuffer,
                    superblockIndex,
                    lumaCoefficientNeighbors,
                    regionRow,
                    regionColumn,
                    unitBottom,
                    unitRight);

                if (hasChroma)
                {
                    int chromaRegionRow = regionRow >> subsamplingY;
                    int chromaRegionColumn = regionColumn >> subsamplingX;

                    // Region limits count 4x4 units. Round the subsampled end upward so a chroma-owning
                    // 4x4, 4x8, or 8x4 luma block still emits its shared 4x4 chroma transform.
                    int chromaUnitBottom = Av1Math.RoundPowerOf2(unitBottom, subsamplingY);
                    int chromaUnitRight = Av1Math.RoundPowerOf2(unitRight, subsamplingX);
                    EncodeTransformCoefficientRegion(
                        pcs,
                        entropyCodingContext,
                        writer,
                        ref block,
                        chromaBlockOrigin,
                        intraLumaMode,
                        blockSize,
                        Av1Plane.U,
                        coefficientBuffer,
                        superblockIndex,
                        blueCoefficientNeighbors,
                        chromaRegionRow,
                        chromaRegionColumn,
                        chromaUnitBottom,
                        chromaUnitRight);

                    EncodeTransformCoefficientRegion(
                        pcs,
                        entropyCodingContext,
                        writer,
                        ref block,
                        chromaBlockOrigin,
                        intraLumaMode,
                        blockSize,
                        Av1Plane.V,
                        coefficientBuffer,
                        superblockIndex,
                        redCoefficientNeighbors,
                        chromaRegionRow,
                        chromaRegionColumn,
                        chromaUnitBottom,
                        chromaUnitRight);
                }
            }
        }
    }

    private static void EncodeTransformCoefficientRegion(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        ref Av1EncoderBlockStruct block,
        Point planeBlockOrigin,
        Av1PredictionMode intraLumaMode,
        Av1BlockSize lumaBlockSize,
        Av1Plane plane,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        int superblockIndex,
        Av1NeighborArrayUnit<byte> coefficientNeighbors,
        int regionRow,
        int regionColumn,
        int unitBottom,
        int unitRight)
    {
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        ObuColorConfig colorConfig = pcs.Sequence.SequenceHeader.ColorConfig;
        bool isLuma = plane == Av1Plane.Y;
        Av1BlockSize planeBlockSize = isLuma
            ? lumaBlockSize
            : lumaBlockSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        Av1TransformSize transformSize = isLuma
            ? entropyCodingContext.MacroBlockModeInfo.Block.TransformSize
            : frameHeader.LosslessArray[entropyCodingContext.MacroBlockModeInfo.Block.SegmentId]
                ? Av1TransformSize.Size4x4
                : lumaBlockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

        int transformBlockWidth = transformSize.Get4x4WideCount();
        int transformBlockHeight = transformSize.Get4x4HighCount();
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        bool usesInterTransformSet =
            entropyCodingContext.MacroBlockModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ||
            entropyCodingContext.MacroBlockModeInfo.Block.UseIntraBlockCopy;
        Av1ComponentType componentType = isLuma
            ? Av1ComponentType.Luminance
            : Av1ComponentType.Chroma;

        Span<int> planeCoefficients = coefficientBuffer.GetPlaneSpan(superblockIndex, plane);
        Span<Av1EncoderTransformBlockState> planeTransformBlocks =
            coefficientBuffer.GetTransformBlockSpan(superblockIndex, plane);

        int codedArea = isLuma
            ? entropyCodingContext.CodedAreaSuperblock
            : entropyCodingContext.CodedAreaSuperblockUv;

        for (int blockRow = regionRow; blockRow < unitBottom; blockRow += transformBlockHeight)
        {
            for (int blockColumn = regionColumn; blockColumn < unitRight; blockColumn += transformBlockWidth)
            {
                int transformStateIndex =
                    codedArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                ref Av1EncoderTransformBlockState transformBlock =
                    ref planeTransformBlocks[transformStateIndex];

                Point transformOrigin = planeBlockOrigin + new Size(
                    blockColumn << Av1Constants.ModeInfoSizeLog2,
                    blockRow << Av1Constants.ModeInfoSizeLog2);

                Span<int> coefficients = planeCoefficients[codedArea..];
                Av1TransformBlockContext blockContext = GetTransformBlockContexts(
                    componentType,
                    coefficientNeighbors,
                    transformOrigin,
                    planeBlockSize,
                    transformSize);

                Av1TransformType transformType = transformBlock.TransformType;
                if (isLuma && transformBlock.EndOfBlock == 0)
                {
                    // Empty luma transforms carry no transform-type symbol, so retain the canonical state.
                    transformType = transformBlock.TransformType = Av1TransformType.DctDct;
                }

                int culLevel = writer.WriteCoefficients(
                    transformSize,
                    transformType,
                    intraLumaMode,
                    coefficients,
                    componentType,
                    blockContext,
                    transformBlock.EndOfBlock,
                    frameHeader.UseReducedTransformSet,
                    block.FilterIntraMode,
                    usesInterTransformSet);

                coefficientNeighbors.UnitModeWrite(
                    (byte)culLevel,
                    transformOrigin,
                    new Size(transformWidth, transformHeight),
                    Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

                codedArea += transformWidth * transformHeight;
            }
        }

        if (isLuma)
        {
            entropyCodingContext.CodedAreaSuperblock = codedArea;
        }
        else if (plane == Av1Plane.V)
        {
            // U and V share the same per-plane coded-area positions; advance only after V completes the region.
            entropyCodingContext.CodedAreaSuperblockUv = codedArea;
        }
    }

    /// <summary>
    /// Converts a luma origin to the shared 4x4 chroma-block origin for the active subsampling.
    /// </summary>
    /// <param name="lumaOrigin">The luma sample position.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <returns>The aligned origin in chroma samples.</returns>
    public static Point GetChromaBlockOrigin(Point lumaOrigin, int subsamplingX, int subsamplingY)
        => new(
            (lumaOrigin.X >> (Av1Constants.ModeInfoSizeLog2 + subsamplingX)) << Av1Constants.ModeInfoSizeLog2,
            (lumaOrigin.Y >> (Av1Constants.ModeInfoSizeLog2 + subsamplingY)) << Av1Constants.ModeInfoSizeLog2);

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from the transform block's above and left neighbors.
    /// </summary>
    /// <param name="plane">The luma or chroma component class.</param>
    /// <param name="dcSignLevelCoefficientNeighborArray">The packed DC-sign and coefficient-level neighbor contexts.</param>
    /// <param name="blockOrigin">The transform-block origin in samples of the target plane.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The coefficient skip and DC-sign contexts selected by both transform edges.</returns>
    public static Av1TransformBlockContext GetTransformBlockContexts(
        Av1ComponentType plane,
        Av1NeighborArrayUnit<byte> dcSignLevelCoefficientNeighborArray,
        Point blockOrigin,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize)
    {
        int leftIndex = dcSignLevelCoefficientNeighborArray.GetLeftIndex(blockOrigin);
        int topIndex = dcSignLevelCoefficientNeighborArray.GetTopIndex(blockOrigin);
        int transformBlockWidth = transformSize.Get4x4WideCount();
        int transformBlockHeight = transformSize.Get4x4HighCount();
        ReadOnlySpan<byte> topContexts = dcSignLevelCoefficientNeighborArray.Top.Slice(topIndex, transformBlockWidth);
        ReadOnlySpan<byte> leftContexts = dcSignLevelCoefficientNeighborArray.Left.Slice(leftIndex, transformBlockHeight);

        return GetTransformBlockContexts(plane, topContexts, leftContexts, planeBlockSize, transformSize);
    }

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from explicit transform-edge contexts.
    /// </summary>
    /// <param name="plane">The luma or chroma component class.</param>
    /// <param name="topContexts">The packed contexts immediately above the transform block.</param>
    /// <param name="leftContexts">The packed contexts immediately left of the transform block.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The coefficient skip and DC-sign contexts selected by both transform edges.</returns>
    public static Av1TransformBlockContext GetTransformBlockContexts(
        Av1ComponentType plane,
        ReadOnlySpan<byte> topContexts,
        ReadOnlySpan<byte> leftContexts,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize)
    {
        int dcSign = 0;
        int top = 0;
        int left = 0;

        // Each context packs a coefficient-level class in the low bits and the DC sign class above it.
        // Accumulating both values in one traversal supplies every luma and chroma context without scratch storage.
        foreach (byte context in topContexts)
        {
            byte sign = (byte)(context >> Av1Constants.CoefficientContextBitCount);
            DebugGuard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
            if (sign == 1)
            {
                dcSign--;
            }
            else if (sign == 2)
            {
                dcSign++;
            }

            top |= context;
        }

        foreach (byte context in leftContexts)
        {
            byte sign = (byte)(context >> Av1Constants.CoefficientContextBitCount);
            DebugGuard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
            if (sign == 1)
            {
                dcSign--;
            }
            else if (sign == 2)
            {
                dcSign++;
            }

            left |= context;
        }

        Av1TransformBlockContext blockContext = default;
        blockContext.DcSignContext = dcSign > 0 ? 2 : dcSign < 0 ? 1 : 0;
        if (plane == Av1ComponentType.Luminance)
        {
            if (planeBlockSize == transformSize.ToBlockSize())
            {
                blockContext.SkipContext = 0;
            }
            else
            {
                top &= Av1Constants.CoefficientContextMask;
                left &= Av1Constants.CoefficientContextMask;
                blockContext.SkipContext = Av1SymbolContextHelper.GetTransformBlockSkipContext(top, left);
            }
        }
        else
        {
            // Chroma contexts use only the presence of nonzero levels on each edge, plus an offset
            // that distinguishes a transform smaller than its containing plane block.
            int contextBase = (left != 0 ? 1 : 0) + (top != 0 ? 1 : 0);
            int contextOffset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            blockContext.SkipContext = contextBase + contextOffset;
        }

        return blockContext;
    }

    /// <summary>
    /// Writes or predicts a block segment identifier and updates the frame segmentation map.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="block">The encoder block state.</param>
    /// <param name="skip">A value indicating whether residual coefficients are omitted.</param>
    /// <param name="beforeSkip">Whether the segment identifier is written before the skip flag.</param>
    private static void WriteSegmentId(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Point blockOrigin,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct block,
        bool skip,
        bool beforeSkip)
    {
        ObuSegmentationParameters segmentation_params = pcs.Parent.FrameHeader.SegmentationParameters;
        if (!segmentation_params.Enabled)
        {
            return;
        }

        int spatial_pred = GetSpatialSegmentationPrediction(pcs, macroBlock, blockOrigin, out int cdf_num);
        if (!beforeSkip && skip)
        {
            // Post-skip segment syntax can infer the spatial predictor once the decoder already knows the block is skipped.
            pcs.UpdateSegmentation(blockSize, blockOrigin, spatial_pred);
            block.SegmentId = spatial_pred;
            return;
        }

        int coded_id = Av1SymbolContextHelper.NegativeDeinterleave(block.SegmentId, spatial_pred, segmentation_params.LastActiveSegmentId + 1);
        writer.WriteSegmentId(coded_id, cdf_num);
        pcs.UpdateSegmentation(blockSize, blockOrigin, block.SegmentId);
    }

    /// <summary>
    /// Derives a segment identifier predictor and entropy context from the upper-left, above, and left neighbors.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="xd">The current macroblock and its neighbor availability.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="cdf_index">The entropy context selected by matching neighbor identifiers.</param>
    /// <returns>The spatially predicted segment identifier.</returns>
    private static int GetSpatialSegmentationPrediction(
        Av1PictureControlSet pcs,
        Av1MacroBlockD xd,
        Point blockOrigin,
        out int cdf_index)
    {
        const int unavailableSegmentId = -1;
        int prev_ul = unavailableSegmentId;
        int prev_l = unavailableSegmentId;
        int prev_u = unavailableSegmentId;

        int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        bool left_available = xd.IsLeftAvailable;
        bool up_available = xd.IsUpAvailable;
        Av1EncoderCommon cm = pcs.Parent.Common;
        Span<byte> segmentation_map = pcs.SegmentationNeighborMap.Span;

        if (up_available && left_available)
        {
            prev_ul = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col - 1, mi_row - 1));
        }

        if (up_available)
        {
            prev_u = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col, mi_row - 1));
        }

        if (left_available)
        {
            prev_l = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_col - 1, mi_row));
        }

        // The entropy context records whether zero, two, or all three neighboring IDs agree.
        // Any unavailable neighbor falls back to the least-specific context.
        if (prev_ul < 0 || prev_u < 0 || prev_l < 0)
        {
            cdf_index = 0;
        }
        else if ((prev_ul == prev_u) && (prev_ul == prev_l))
        {
            cdf_index = 2;
        }
        else if ((prev_ul == prev_u) || (prev_ul == prev_l) || (prev_u == prev_l))
        {
            cdf_index = 1;
        }
        else
        {
            cdf_index = 0;
        }

        // Select the majority value when possible; otherwise AV1 gives the left neighbor precedence.
        if (prev_u == unavailableSegmentId)
        {
            return prev_l == unavailableSegmentId ? 0 : prev_l;
        }

        if (prev_l == unavailableSegmentId)
        {
            return prev_u;
        }

        return (prev_ul == prev_u) ? prev_u : prev_l;
    }

    /// <summary>
    /// Gets the block skip context from the available above and left modes.
    /// </summary>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <returns>The sum of the available above and left skip states.</returns>
    public static int GetSkipContext(Av1MacroBlockD macroBlock)
    {
        bool aboveSkipped = macroBlock.IsUpAvailable &&
            macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Skip;

        bool leftSkipped = macroBlock.IsLeftAvailable && macroBlock.GetRelativeModeInfo(-1).Block.Skip;
        return (aboveSkipped ? 1 : 0) + (leftSkipped ? 1 : 0);
    }

    /// <summary>
    /// Selects block skip when it is cheaper than retaining empty transform syntax.
    /// </summary>
    /// <param name="writer">The live tile symbol encoder.</param>
    /// <param name="skipContext">The neighboring block skip context.</param>
    /// <param name="emptyTransformRate">The complete coefficient rate for the empty transforms.</param>
    /// <returns>
    /// <see langword="true"/> when block skip has a strictly lower rate; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool ShouldSkipCoefficients(
        Av1SymbolEncoder writer,
        int skipContext,
        int emptyTransformRate)
    {
        int skipRate = writer.GetSkipCost(true, skipContext);
        int nonSkipRate = writer.GetSkipCost(false, skipContext) + emptyTransformRate;

        // Current libaom keeps intra blocks non-skipped. Empty transforms make both choices
        // decoder-identical, so select skip only when its complete live rate is strictly lower.
        return skipRate < nonSkipRate;
    }

    /// <summary>
    /// Writes the block skip flag using the sum of available above and left skip states as its context.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="skip">The skip value to write.</param>
    public static void EncodeSkipCoefficients(Av1SymbolEncoder writer, Av1MacroBlockD macroBlock, bool skip)
        => writer.WriteSkip(skip, GetSkipContext(macroBlock));
}
