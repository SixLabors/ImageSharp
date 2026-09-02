// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

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
            ref finalBlockIndex);
    }

    /// <summary>
    /// Writes one selected partition node and recursively visits its split children.
    /// </summary>
    private static void WritePartitionTree(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Av1BlockSize blockSize,
        Point blockOrigin,
        ref int partitionIndex,
        ref int finalBlockIndex)
    {
        Av1EncoderCommon common = pcs.Parent.Common;
        int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        if (modeInfoRow >= common.ModeInfoRowCount || modeInfoColumn >= common.ModeInfoColumnCount)
        {
            return;
        }

        Av1PartitionType partition = (Av1PartitionType)superblock.CodingUnitPartitionTypes[partitionIndex++];
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
                    ref finalBlockIndex);

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
                    ref finalBlockIndex);

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
                        ref finalBlockIndex);
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
                    ref finalBlockIndex);

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
                        ref finalBlockIndex);
                }

                break;
            case Av1PartitionType.Split:
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
                    ref finalBlockIndex);
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
                    ref finalBlockIndex);
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
                    ref finalBlockIndex);
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
                    ref finalBlockIndex);

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
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex);

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
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex);

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
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(0, halfBlockSize),
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex);

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
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, 0),
                    ref finalBlockIndex);
                WriteFinalBlock(
                    pcs,
                    entropyCodingContext,
                    writer,
                    superblock,
                    coefficientBuffer,
                    tileIndex,
                    blockOrigin + new Size(halfBlockSize, halfBlockSize),
                    ref finalBlockIndex);

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
                        ref finalBlockIndex);
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
                        ref finalBlockIndex);
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
    private static void WriteFinalBlock(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        ushort tileIndex,
        Point blockOrigin,
        ref int finalBlockIndex)
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
            coefficientBuffer);
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

        int halfBlockModeInfoCount = blockSize.Get4x4WideCount() >> 1;
        Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
        bool has_rows = modeInfoPosition.Y + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoRowCount;
        bool has_cols = modeInfoPosition.X + halfBlockModeInfoCount < pcs.Parent.Common.ModeInfoColumnCount;

        int partition_context_left_neighbor_index = partition_context_na.GetLeftIndex(blockOrigin);
        int partition_context_top_neighbor_index = partition_context_na.GetTopIndex(blockOrigin);

        int context_index = 0;

        byte above_ctx =
            (byte)(partition_context_na.Top[partition_context_top_neighbor_index].Above == byte.MaxValue
            ? 0
            : partition_context_na.Top[partition_context_top_neighbor_index].Above);
        byte left_ctx =
            (byte)(partition_context_na.Left[partition_context_left_neighbor_index].Left == byte.MaxValue
            ? 0
            : partition_context_na.Left[partition_context_left_neighbor_index].Left);

        int blockSizeLog2 = blockSize.Get4x4WidthLog2() - 1;
        int above = (above_ctx >> blockSizeLog2) & 1, left = (left_ctx >> blockSizeLog2) & 1;

        Guard.IsTrue(blockSize.Get4x4WidthLog2() == blockSize.Get4x4HeightLog2(), nameof(blockSize), "Blocks need to be square.");
        Guard.IsTrue(blockSizeLog2 >= 0, nameof(blockSizeLog2), "bsl needs to be a positive integer.");

        // Each square block-size level owns four contexts selected by the current split bit of its neighbors.
        context_index = ((left * 2) + above) + (blockSizeLog2 * Av1Constants.PartitionProbabilitySet);

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
    private static void WriteModesBlock(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Av1SymbolEncoder writer,
        Av1Superblock tb_ptr,
        ref Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Point blockOrigin,
        Av1EncoderCoefficientBuffer coefficientBuffer)
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
        bool skipWritingCoefficients = macroBlockModeInfo.Block.Skip;
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

        // This encoder path currently writes intra frames only, so every block follows the key-frame mode syntax.
        {
            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(pcs, writer, blockSize, blockOrigin, macroBlock, ref blk_ptr, skipWritingCoefficients);
            }

            EncodeSkipCoefficients(writer, macroBlock, skipWritingCoefficients);

            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && !pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(pcs, writer, blockSize, blockOrigin, macroBlock, ref blk_ptr, skipWritingCoefficients);
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
                    int reduced_delta_qindex = (current_q_index - pcs.Parent.PreviousQIndex[tile_idx]) /
                        frm_hdr.DeltaQParameters.Resolution;

                    writer.WriteDeltaQuantizerIndex(reduced_delta_qindex);
                    pcs.Parent.PreviousQIndex[tile_idx] = current_q_index;
                }
            }

            Av1PredictionMode intra_luma_mode = macroBlockModeInfo.Block.Mode;
            Av1ChromaPredictionMode intra_chroma_mode = macroBlockModeInfo.Block.UvMode;
            if (IsIntraBlockCopyAllowed(pcs.Parent.FrameHeader/*, pcs.Parent.SliceType*/))
            {
                WriteIntraBlockCopyInfo(writer, macroBlockModeInfo);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                EncodeIntraLumaMode(writer, macroBlockModeInfo, macroBlock, ref blk_ptr, blockSize, intra_luma_mode);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy)
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
                        intra_luma_mode,
                        intra_chroma_mode);
                }
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy && IsPaletteAllowed(frm_hdr.AllowScreenContentTools, blockSize))
            {
                WritePaletteModeInfo(
                    scs,
                    writer,
                    macroBlockModeInfo,
                    ref blk_ptr,
                    blockSize,
                    blockOrigin >> Av1Constants.ModeInfoSizeLog2);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsFilterIntraAllowed(scs.SequenceHeader.EnableFilterIntra, blockSize, blk_ptr.PaletteSize[0], intra_luma_mode))
            {
                writer.WriteFilterIntraMode(blk_ptr.FilterIntraMode, blockSize);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                Guard.IsTrue(blk_ptr.PaletteSize[1] == 0, nameof(blk_ptr), "Palette of chroma plane shall be empty.");

                // TOKENEXTRA tok = entropyCodingContext.tok;
                for (int plane = 0; plane < 2; ++plane)
                {
                    int palette_size_plane = blk_ptr.PaletteSize[plane];
                    if (palette_size_plane > 0)
                    {
                        throw new NotImplementedException("Tokenizing palette not implemented.");
                    }
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
                    intra_luma_mode,
                    blockSize,
                    coefficientBuffer,
                    tb_ptr.Index,
                    luma_dc_sign_level_coeff_na,
                    cr_dc_sign_level_coeff_na,
                    cb_dc_sign_level_coeff_na);
            }
        }

        // Neighbor state must be updated after all symbols for the block have used the preceding contexts.
        UpdateNeighbors(pcs, entropyCodingContext, blockOrigin, ref blk_ptr, tile_idx, blockSize);
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
        bool writesTransformSize = !isLossless &&
            frameHeader.TransformMode == Av1TransformMode.Select &&
            blockSize > Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = isLossless
            ? Av1TransformSize.Size4x4
            : writesTransformSize
                ? macroBlockModeInfo.Block.TransformSize
                : blockSize.GetMaximumTransformSize();

        macroBlockModeInfo.Block.TransformSize = transformSize;
        Av1NeighborArrayUnit<byte> transformContexts = pcs.TransformFunctionContexts[tileIndex];
        if (writesTransformSize)
        {
            Av1TransformSize maximumTransformSize = blockSize.GetMaximumTransformSize();
            int above = transformContexts.Top[transformContexts.GetTopIndex(blockOrigin)] >= maximumTransformSize.GetWidth() ? 1 : 0;
            int left = transformContexts.Left[transformContexts.GetLeftIndex(blockOrigin)] >= maximumTransformSize.GetHeight() ? 1 : 0;
            int context = macroBlock.IsUpAvailable
                ? macroBlock.IsLeftAvailable ? above + left : above
                : macroBlock.IsLeftAvailable ? left : 0;

            writer.WriteTransformSize(blockSize, transformSize, context);
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
        bool isChromaFromLumaAllowed = blockSize.AllowsChromaFromLuma(
            frameHeader.LosslessArray[macroBlockModeInfo.Block.SegmentId],
            colorConfig.SubSamplingX,
            colorConfig.SubSamplingY);

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
    /// Writes the key-frame luma prediction mode and any directional angle adjustment.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    private static void EncodeIntraLumaMode(
        Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode)
    {
        GetYModeContext(macroBlock, out byte topContext, out byte leftContext);
        writer.WriteLumaMode(lumaMode, topContext, leftContext);

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.Mode.IsDirectional())
        {
            writer.WriteAngleDelta(blk_ptr.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] + Av1Constants.MaxAngleDelta, lumaMode);
        }
    }

    /// <summary>
    /// Writes luma and chroma palette-mode syntax for a block.
    /// </summary>
    /// <param name="scs">The sequence coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="point">The block position in mode-information units.</param>
    /// <exception cref="NotImplementedException">Palette-mode encoding is not implemented.</exception>
    private static void WritePaletteModeInfo(
        Av1SequenceControlSet scs,
        Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        ref Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Point point)
    {
        throw new NotImplementedException("Palette mode encoding not implemented.");
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
    private static bool IsFilterIntraAllowedBlockSize(bool enableFilterIntra, Av1BlockSize blockSize)
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
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <exception cref="NotImplementedException">The displacement-vector syntax is not implemented when intra block copy is selected.</exception>
    private static void WriteIntraBlockCopyInfo(
        Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo)
    {
        bool use_intrabc = macroBlockModeInfo.Block.UseIntraBlockCopy;
        writer.WriteUseIntraBlockCopy(use_intrabc);
        if (use_intrabc)
        {
            throw new NotImplementedException("Intra block code encoding not implemented.");
        }
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
    /// Determines whether the encoder palette level and block dimensions permit palette mode.
    /// </summary>
    /// <param name="allowPalette">The nonzero encoder palette level.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when palette mode is enabled for the block; otherwise, <see langword="false"/>.</returns>
    private static bool IsPaletteAllowed(int allowPalette, Av1BlockSize blockSize)
    {
        Guard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));
        return allowPalette != 0 &&
            blockSize.GetWidth() <= 64 &&
            blockSize.GetHeight() <= 64 &&
            blockSize >= Av1BlockSize.Block8x8;
    }

    /// <summary>
    /// Determines whether screen-content tools and block dimensions permit palette mode.
    /// </summary>
    /// <param name="allowScreenContentTools">A value indicating whether screen-content tools are enabled.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when palette mode is available for the block; otherwise, <see langword="false"/>.</returns>
    private static bool IsPaletteAllowed(bool allowScreenContentTools, Av1BlockSize blockSize)
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

        // Each superblock begins with all contained 64x64 filter units unassigned.
        if ((modeInfoPosition.Y & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0 &&
            (modeInfoPosition.X & (scs.SequenceHeader.SuperblockModeInfoSize - 1)) == 0)
        {
            pcs.CdefPreset[tileIndex][0] = -1;
            pcs.CdefPreset[tileIndex][1] = -1;
            pcs.CdefPreset[tileIndex][2] = -1;
            pcs.CdefPreset[tileIndex][3] = -1;
        }

        // The strength is coded once, at the first non-skipped block in each 64x64 CDEF filter unit.
        int cdefSize = 1 << (6 - Av1Constants.ModeInfoSizeLog2);
        int unitColumn = (modeInfoPosition.X & cdefSize) != 0 ? 1 : 0;
        int unitRow = (modeInfoPosition.Y & cdefSize) != 0 ? 1 : 0;
        int index = scs.SequenceHeader.Use128x128Superblock ? unitColumn + (2 * unitRow) : 0;

        if (pcs.CdefPreset[tileIndex][index] == -1 && !skip)
        {
            int firstBlockMask = ~(cdefSize - 1);
            Point firstBlockPosition = new(
                modeInfoPosition.X & firstBlockMask,
                modeInfoPosition.Y & firstBlockMask);
            ref Av1MacroBlockModeInfo firstBlock = ref pcs.GetFromModeInfoGrid(firstBlockPosition);

            // CDEF strength belongs to the first mode-info block in the 64x64 filter unit even when skipped
            // blocks delay transmission until a later coding block.
            writer.WriteCdefStrength(firstBlock.CdefStrength, frameHeader.CdefParameters.BitCount);
            pcs.CdefPreset[tileIndex][index] = firstBlock.CdefStrength;
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
        EncodeTransformCoefficientsY(
            pcs,
            ec_ctx,
            writer,
            ref blk_ptr,
            blockOrigin,
            intraLumaDir,
            planeBlockSize,
            coefficientBuffer,
            superblockIndex,
            luma_dc_sign_level_coeff_na);

        EncodeTransformCoefficientsUv(
            pcs,
            ec_ctx,
            writer,
            ref blk_ptr,
            blockOrigin,
            intraLumaDir,
            planeBlockSize,
            coefficientBuffer,
            superblockIndex,
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
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        Span<int> lumaCoefficients = coefficientBuffer.GetPlaneSpan(superblockIndex, Av1Plane.Y);
        Span<Av1EncoderTransformBlockState> lumaTransformBlocks =
            coefficientBuffer.GetTransformBlockSpan(superblockIndex, Av1Plane.Y);
        Av1TransformSize transformSize = entropyCodingContext.MacroBlockModeInfo.Block.TransformSize;
        int transformBlockWidth = transformSize.Get4x4WideCount();
        int transformBlockHeight = transformSize.Get4x4HighCount();
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

        // AV1 visits residuals in bounded 64x64 regions so transform order remains stable for 128x128 blocks.
        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitHeight = Math.Min(maximumUnitBlocksHigh + regionRow, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitWidth = Math.Min(maximumUnitBlocksWide + regionColumn, maximumBlocksWide);
                for (int blockRow = regionRow; blockRow < unitHeight; blockRow += transformBlockHeight)
                {
                    for (int blockColumn = regionColumn; blockColumn < unitWidth; blockColumn += transformBlockWidth)
                    {
                        int transformStateIndex = entropyCodingContext.CodedAreaSuperblock /
                            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                        ref Av1EncoderTransformBlockState transformBlock = ref lumaTransformBlocks[transformStateIndex];
                        Point transformOrigin = blockOrigin + new Size(
                            blockColumn << Av1Constants.ModeInfoSizeLog2,
                            blockRow << Av1Constants.ModeInfoSizeLog2);
                        Span<int> coefficients = lumaCoefficients[entropyCodingContext.CodedAreaSuperblock..];
                        Av1TransformBlockContext blockContext = GetTransformBlockContexts(
                            Av1ComponentType.Luminance,
                            luma_dc_sign_level_coeff_na,
                            transformOrigin,
                            plane_bsize,
                            transformSize);

                        Av1TransformType transformType = transformBlock.TransformType;
                        ushort endOfBlock = transformBlock.EndOfBlock;
                        if (endOfBlock == 0)
                        {
                            // Empty transform blocks use the canonical transform type even when mode decision retained another candidate.
                            transformType = transformBlock.TransformType = Av1TransformType.DctDct;
                        }

                        int culLevelY = writer.WriteCoefficients(
                            transformSize,
                            transformType,
                            intraLumaDir,
                            coefficients,
                            Av1ComponentType.Luminance,
                            blockContext,
                            endOfBlock,
                            frameHeader.UseReducedTransformSet,
                            blk_ptr.FilterIntraMode);

                        int transformWidth = transformSize.GetWidth();
                        int transformHeight = transformSize.GetHeight();
                        luma_dc_sign_level_coeff_na.UnitModeWrite(
                            (byte)culLevelY,
                            transformOrigin,
                            new Size(transformWidth, transformHeight),
                            Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

                        entropyCodingContext.CodedAreaSuperblock += transformWidth * transformHeight;
                    }
                }
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

        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        Span<int> blueCoefficients = coefficientBuffer.GetPlaneSpan(superblockIndex, Av1Plane.U);
        Span<int> redCoefficients = coefficientBuffer.GetPlaneSpan(superblockIndex, Av1Plane.V);
        Span<Av1EncoderTransformBlockState> blueTransformBlocks =
            coefficientBuffer.GetTransformBlockSpan(superblockIndex, Av1Plane.U);
        Span<Av1EncoderTransformBlockState> redTransformBlocks =
            coefficientBuffer.GetTransformBlockSpan(superblockIndex, Av1Plane.V);

        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        Av1BlockSize chromaBlockSize = plane_bsize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
        Point chromaBlockOrigin = GetChromaBlockOrigin(blockOrigin, subsamplingX, subsamplingY);
        Av1TransformSize chromaTransformSize = frameHeader.LosslessArray[entropyCodingContext.MacroBlockModeInfo.Block.SegmentId]
            ? Av1TransformSize.Size4x4
            : plane_bsize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);
        int transformBlockWidth = chromaTransformSize.Get4x4WideCount();
        int transformBlockHeight = chromaTransformSize.Get4x4HighCount();
        int transformWidth = chromaTransformSize.GetWidth();
        int transformHeight = chromaTransformSize.GetHeight();
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

        // Chroma follows the same bounded-region order after scaling both the block and frame edges to its plane.
        for (int regionRow = 0; regionRow < maximumBlocksHigh; regionRow += maximumUnitBlocksHigh)
        {
            int unitHeight = Math.Min(maximumUnitBlocksHigh + regionRow, maximumBlocksHigh);
            for (int regionColumn = 0; regionColumn < maximumBlocksWide; regionColumn += maximumUnitBlocksWide)
            {
                int unitWidth = Math.Min(maximumUnitBlocksWide + regionColumn, maximumBlocksWide);
                for (int blockRow = regionRow; blockRow < unitHeight; blockRow += transformBlockHeight)
                {
                    for (int blockColumn = regionColumn; blockColumn < unitWidth; blockColumn += transformBlockWidth)
                    {
                        int transformStateIndex = entropyCodingContext.CodedAreaSuperblockUv /
                            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                        ref Av1EncoderTransformBlockState blueTransformBlock = ref blueTransformBlocks[transformStateIndex];
                        ref Av1EncoderTransformBlockState redTransformBlock = ref redTransformBlocks[transformStateIndex];
                        Point chromaOrigin = chromaBlockOrigin + new Size(
                            blockColumn << Av1Constants.ModeInfoSizeLog2,
                            blockRow << Av1Constants.ModeInfoSizeLog2);

                        // U and V share transform geometry and type while retaining independent coefficient and EOB state.
                        Span<int> coefficients = blueCoefficients[entropyCodingContext.CodedAreaSuperblockUv..];
                        Av1TransformBlockContext blockContext = GetTransformBlockContexts(
                            Av1ComponentType.Chroma,
                            cb_dc_sign_level_coeff_na,
                            chromaOrigin,
                            chromaBlockSize,
                            chromaTransformSize);

                        Av1TransformType chromaTransformType = blueTransformBlock.TransformType;
                        int culLevelCb = writer.WriteCoefficients(
                            chromaTransformSize,
                            chromaTransformType,
                            intraLumaDir,
                            coefficients,
                            Av1ComponentType.Chroma,
                            blockContext,
                            blueTransformBlock.EndOfBlock,
                            frameHeader.UseReducedTransformSet,
                            blk_ptr.FilterIntraMode);

                        coefficients = redCoefficients[entropyCodingContext.CodedAreaSuperblockUv..];
                        blockContext = GetTransformBlockContexts(
                            Av1ComponentType.Chroma,
                            cr_dc_sign_level_coeff_na,
                            chromaOrigin,
                            chromaBlockSize,
                            chromaTransformSize);

                        int culLevelCr = writer.WriteCoefficients(
                            chromaTransformSize,
                            chromaTransformType,
                            intraLumaDir,
                            coefficients,
                            Av1ComponentType.Chroma,
                            blockContext,
                            redTransformBlock.EndOfBlock,
                            frameHeader.UseReducedTransformSet,
                            blk_ptr.FilterIntraMode);

                        cb_dc_sign_level_coeff_na.UnitModeWrite(
                            (byte)culLevelCb,
                            chromaOrigin,
                            new Size(transformWidth, transformHeight),
                            Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

                        cr_dc_sign_level_coeff_na.UnitModeWrite(
                            (byte)culLevelCr,
                            chromaOrigin,
                            new Size(transformWidth, transformHeight),
                            Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

                        entropyCodingContext.CodedAreaSuperblockUv += transformWidth * transformHeight;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts a luma origin to the shared 4x4 chroma-block origin for the active subsampling.
    /// </summary>
    /// <param name="lumaOrigin">The luma sample position.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <returns>The aligned origin in chroma samples.</returns>
    private static Point GetChromaBlockOrigin(Point lumaOrigin, int subsamplingX, int subsamplingY)
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
    private static void WriteSegmentId(
        Av1PictureControlSet pcs,
        Av1SymbolEncoder writer,
        Av1BlockSize blockSize,
        Point blockOrigin,
        Av1MacroBlockD macroBlock,
        ref Av1EncoderBlockStruct block,
        bool skip)
    {
        ObuSegmentationParameters segmentation_params = pcs.Parent.FrameHeader.SegmentationParameters;
        if (!segmentation_params.Enabled)
        {
            return;
        }

        int spatial_pred = GetSpatialSegmentationPrediction(pcs, macroBlock, blockOrigin, out int cdf_num);
        if (skip)
        {
            // With segment-id-before-skip syntax, a skipped block inherits the spatial predictor without coding a residual ID.
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
    /// Writes the block skip flag using the sum of available above and left skip states as its context.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlock">The reusable macroblock edge and neighbor state.</param>
    /// <param name="skip">The skip value to write.</param>
    public static void EncodeSkipCoefficients(Av1SymbolEncoder writer, Av1MacroBlockD macroBlock, bool skip)
    {
        int above_skip = macroBlock.IsUpAvailable && macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Skip ? 1 : 0;
        int left_skip = macroBlock.IsLeftAvailable && macroBlock.GetRelativeModeInfo(-1).Block.Skip ? 1 : 0;
        writer.WriteSkip(skip, above_skip + left_skip);
    }
}
