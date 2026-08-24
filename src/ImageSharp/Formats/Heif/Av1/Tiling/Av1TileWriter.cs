// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;
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
    /// <param name="frameBuffer">The transformed coefficients for the frame.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <remarks>Corresponds to <c>svt_aom_write_sb</c> in SVT-AV1.</remarks>
    public static void WriteSuperblock(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
        ref Av1SymbolEncoder writer,
        Av1Superblock superblock,
        Av1FrameBuffer<int> frameBuffer,
        ushort tileIndex)
    {
        Av1SequenceControlSet scs = pcs.Sequence;
        Av1NeighborArrayUnit<Av1PartitionContext> partitionContextNeighbors = pcs.PartitionContexts[tileIndex];

        // The geometry scan includes both partition nodes and final coding blocks. These two indices
        // advance independently because a split node consumes geometry without consuming FinalBlocks.
        int blockIndex = 0;
        uint finalBlockIndex = 0;

        ec_ctx.CodedAreaSuperblock = 0;
        ec_ctx.CodedAreaSuperblockUv = 0;
        Av1SuperblockGeometry sb_geom = pcs.Parent.SuperblockGeometry[superblock.Index];
        bool check_blk_out_of_bound = !sb_geom.IsComplete;
        do
        {
            bool code_blk_cond = true;
            Av1EncoderBlockStruct blk_ptr = superblock.FinalBlocks[finalBlockIndex];
            Av1BlockGeometry blk_geom = Av1BlockGeometryFactory.GetBlockGeometryByModeDecisionScanIndex(blockIndex);

            Av1BlockSize bsize = blk_geom.BlockSize;
            Point blockOrigin = blk_geom.Origin;
            Guard.IsTrue(bsize < Av1BlockSize.AllSizes, nameof(bsize), "Block size must be a valid value.");

            if (check_blk_out_of_bound)
            {
                // Edge superblocks retain their complete geometry tree, but only nodes whose center or
                // origin reaches the visible frame can contribute coding syntax.
                code_blk_cond = (((blockOrigin.X + (blk_geom.BlockWidth / 2)) < pcs.Parent.AlignedWidth) ||
                                 ((blockOrigin.Y + (blk_geom.BlockHeight / 2)) < pcs.Parent.AlignedHeight)) &&
                    (blockOrigin.X < pcs.Parent.AlignedWidth && blockOrigin.Y < pcs.Parent.AlignedHeight);
            }

            if (code_blk_cond)
            {
                int hbs = bsize.Get4x4WideCount() >> 1;
                int quarter_step = bsize.Get4x4WideCount() >> 2;
                Av1EncoderCommon cm = pcs.Parent.Common;
                int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
                int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;

                if (bsize >= Av1BlockSize.Block8x8)
                {
                    for (int plane = 0; plane < 3; ++plane)
                    {
                        /* TODO: Implement
                        if (svt_av1_loop_restoration_corners_in_sb(cm,
                                                                   scs.SequenceHeader,
                                                                   plane,
                                                                   mi_row,
                                                                   mi_col,
                                                                   bsize,
                                                                   out int rcol0,
                                                                   out int rcol1,
                                                                   out int rrow0,
                                                                   out int rrow1,
                                                                   out int tile_tl_idx))
                        {
                            int rstride = pcs.RestorationInfos[plane].HorizontalUnitCountPerTile;
                            for (int rrow = rrow0; rrow < rrow1; ++rrow)
                            {
                                for (int rcol = rcol0; rcol < rcol1; ++rcol)
                                {
                                    int runit_idx = tile_tl_idx + rcol + (rrow * rstride);
                                    Av1RestorationUnitInfo rui = pcs.RestorationUnitInfos[plane].UnitInfo[runit_idx];
                                    loop_restoration_write_sb_coeffs(
                                        pcs,
                                        ref writer,
                                        tileIndex,
                                        rui,
                                        plane);
                                }
                            }
                        }*/
                    }

                    // Blocks below 8x8 cannot be partition points in the AV1 syntax.
                    EncodePartition(
                        pcs,
                        ref writer,
                        bsize,
                        superblock.CodingUnitPartitionTypes[blockIndex],
                        blockOrigin,
                        partitionContextNeighbors);
                }

                Guard.IsTrue(Av1Math.Implies(bsize == Av1BlockSize.Block4x4, superblock.CodingUnitPartitionTypes[blockIndex] == Av1PartitionType.None), nameof(bsize), string.Empty);
                switch (superblock.CodingUnitPartitionTypes[blockIndex])
                {
                    case Av1PartitionType.None:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        break;

                    case Av1PartitionType.Horizontal:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        if (mi_row + hbs < cm.ModeInfoRowCount)
                        {
                            finalBlockIndex++;
                            blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                            WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        }

                        break;

                    case Av1PartitionType.Vertical:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        if (mi_col + hbs < cm.ModeInfoColumnCount)
                        {
                            finalBlockIndex++;
                            blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                            WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        }

                        break;
                    case Av1PartitionType.Split:
                        break;
                    case Av1PartitionType.HorizontalA:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        break;
                    case Av1PartitionType.HorizontalB:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        break;
                    case Av1PartitionType.VerticalA:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        break;
                    case Av1PartitionType.VerticalB:
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        finalBlockIndex++;
                        blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                        WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);

                        break;
                    case Av1PartitionType.Horizontal4:
                        for (int i = 0; i < 4; ++i)
                        {
                            int this_mi_row = mi_row + (i * quarter_step);
                            if (i > 0 && this_mi_row >= cm.ModeInfoRowCount)
                            {
                                // Only the last block is able to be outside the picture boundary. If one of the first
                                // 3 blocks is outside the boundary, H4 is not a valid partition (see AV1 spec 5.11.4)
                                Guard.IsTrue(i == 3, nameof(i), "Only the last block can be partial");
                                break;
                            }

                            if (i > 0)
                            {
                                finalBlockIndex++;
                                blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                            }

                            WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        }

                        break;
                    case Av1PartitionType.Vertical4:
                        for (int i = 0; i < 4; ++i)
                        {
                            int this_mi_col = mi_col + (i * quarter_step);
                            if (i > 0 && this_mi_col >= cm.ModeInfoColumnCount)
                            {
                                // Only the last block is able to be outside the picture boundary. If one of the first
                                // 3 blocks is outside the boundary, H4 is not a valid partition (see AV1 spec 5.11.4)
                                Guard.IsTrue(i == 3, nameof(i), "Only the last block can be partial");
                                break;
                            }

                            if (i > 0)
                            {
                                finalBlockIndex++;
                                blk_ptr = superblock.FinalBlocks[finalBlockIndex];
                            }

                            WriteModesBlock(pcs, ec_ctx, ref writer, superblock, blk_ptr, tileIndex, frameBuffer);
                        }

                        break;
                }

                if (superblock.CodingUnitPartitionTypes[blockIndex] != Av1PartitionType.Split)
                {
                    finalBlockIndex++;
                    blockIndex += blk_geom.NextDepthOffset;
                }
                else
                {
                    blockIndex += blk_geom.Depth1Offset;
                }
            }
            else
            {
                blockIndex += blk_geom.Depth1Offset;
            }
        }
        while (blockIndex < scs.MaxBlockCount);
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
    /// <remarks>Corresponds to <c>encode_partition_av1</c> in SVT-AV1.</remarks>
    private static void EncodePartition(
        Av1PictureControlSet pcs,
        ref Av1SymbolEncoder writer,
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

        int hbs = (blockSize.Get4x4WideCount() << 2) >> 1;
        bool has_rows = (blockOrigin.Y + hbs) < pcs.Parent.AlignedHeight;
        bool has_cols = (blockOrigin.X + hbs) < pcs.Parent.AlignedWidth;

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
            writer.WriteSplitOrVertical(partitionType, blockSize, context_index);
        }
        else
        {
            writer.WriteSplitOrHorizontal(partitionType, blockSize, context_index);
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
    /// <param name="coeff_ptr">The transformed coefficients for the frame.</param>
    /// <remarks>Corresponds to <c>write_modes_b</c> in SVT-AV1.</remarks>
    private static void WriteModesBlock(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        ref Av1SymbolEncoder writer,
        Av1Superblock tb_ptr,
        Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Av1FrameBuffer<int> coeff_ptr)
    {
        Av1SequenceControlSet scs = pcs.Sequence;
        ObuFrameHeader frm_hdr = pcs.Parent.FrameHeader;
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na = pcs.LuminanceDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na = pcs.CrDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na = pcs.CbDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> txfm_context_array = pcs.TransformFunctionContexts[tile_idx];
        Av1BlockGeometry blockGeometry = Av1BlockGeometryFactory.GetBlockGeometryByModeDecisionScanIndex(blk_ptr.ModeDecisionScanIndex);
        Point blockOrigin = Point.Add(entropyCodingContext.SuperblockOrigin, (Size)blockGeometry.Origin);
        Av1BlockSize blockSize = blockGeometry.BlockSize;
        Av1MacroBlockModeInfo macroBlockModeInfo = pcs.GetMacroBlockModeInfo(blockOrigin);
        bool skipWritingCoefficients = macroBlockModeInfo.Block.Skip;
        entropyCodingContext.MacroBlockModeInfo = macroBlockModeInfo;

        bool skip_mode = macroBlockModeInfo.Block.SkipMode;

        Guard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.AllSizes, nameof(blockSize));
        int mi_row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
        int mi_col = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_stride = pcs.Parent.Common.ModeInfoStride;
        int offset = (mi_row * mi_stride) + mi_col;
        Point modeInfoPosition = new(mi_col, mi_row);
        blk_ptr.MacroBlock.ModeInfo = pcs.ModeInfoGrid[offset];
        blk_ptr.MacroBlock.Tile = new Av1TileInfo(tb_ptr.TileInfo);
        blk_ptr.MacroBlock.IsUpAvailable = modeInfoPosition.Y > tb_ptr.TileInfo.ModeInfoRowStart;
        blk_ptr.MacroBlock.IsLeftAvailable = modeInfoPosition.X > tb_ptr.TileInfo.ModeInfoColumnStart;

        if (blk_ptr.MacroBlock.IsUpAvailable)
        {
            blk_ptr.MacroBlock.AboveMacroBlock = blk_ptr.MacroBlock.ModeInfo[-mi_stride].MacroBlockModeInfo;
        }
        else
        {
            blk_ptr.MacroBlock.AboveMacroBlock = null;
        }

        if (blk_ptr.MacroBlock.IsLeftAvailable)
        {
            blk_ptr.MacroBlock.LeftMacroBlock = blk_ptr.MacroBlock.ModeInfo[-1].MacroBlockModeInfo;
        }
        else
        {
            blk_ptr.MacroBlock.LeftMacroBlock = null;
        }

        SetModeInfoRowAndColumn(
            pcs,
            blk_ptr.MacroBlock,
            blk_ptr.MacroBlock.Tile,
            modeInfoPosition,
            blockSize,
            mi_stride,
            pcs.Parent.Common.ModeInfoRowCount,
            pcs.Parent.Common.ModeInfoColumnCount);

        // This encoder path currently writes intra frames only, so every block follows the key-frame mode syntax.
        {
            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(pcs, ref writer, blockGeometry.BlockSize, blockOrigin, blk_ptr, skipWritingCoefficients);
            }

            EncodeSkipCoefficients(ref writer, blk_ptr, skipWritingCoefficients);

            if (pcs.Parent.FrameHeader.SegmentationParameters.Enabled && !pcs.Parent.FrameHeader.SegmentationParameters.SegmentIdPrecedesSkip)
            {
                WriteSegmentId(pcs, ref writer, blockGeometry.BlockSize, blockOrigin, blk_ptr, skipWritingCoefficients);
            }

            WriteCdef(
                scs,
                pcs,
                ref writer,
                tile_idx,
                skipWritingCoefficients,
                blockOrigin << Av1Constants.ModeInfoSizeLog2);

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
            Av1PredictionMode intra_chroma_mode = macroBlockModeInfo.Block.UvMode;
            if (IsIntraBlockCopyAllowed(pcs.Parent.FrameHeader/*, pcs.Parent.SliceType*/))
            {
                WriteIntraBlockCopyInfo(ref writer, macroBlockModeInfo, blk_ptr);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                EncodeIntraLumaMode(ref writer, macroBlockModeInfo, blk_ptr, blockSize, intra_luma_mode);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy)
            {
                if (blockGeometry.HasUv)
                {
                    EncodeIntraChromaMode(
                        ref writer,
                        macroBlockModeInfo,
                        blk_ptr,
                        blockSize,
                        intra_luma_mode,
                        intra_chroma_mode,
                        blockGeometry.BlockWidth <= 32 && blockGeometry.BlockHeight <= 32);
                }
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy && IsPaletteAllowed(frm_hdr.AllowScreenContentTools, blockGeometry.BlockSize))
            {
                WritePaletteModeInfo(
                    scs,
                    ref writer,
                    macroBlockModeInfo,
                    blk_ptr,
                    blockGeometry.BlockSize,
                    blockOrigin >> Av1Constants.ModeInfoSizeLog2);
            }

            if (!macroBlockModeInfo.Block.UseIntraBlockCopy &&
                IsFilterIntraAllowed(scs.SequenceHeader.FilterIntraLevel > 0, blockSize, blk_ptr.PaletteSize[0], intra_luma_mode))
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
                        /*
                        Av1TransformSize tx_size =
                            blockGeometry.TransformSize[macroBlockModeInfo.Block.TransformDepth]; // inherit tx_size from 1st transform block;
                        svt_av1_tokenize_color_map(
                            frame_context,
                            blk_ptr,
                            plane,
                            tok,
                            blockSize,
                            tx_size,
                            PALETTE_MAP,
                            0); // NO CDF update in entropy, the update will take place in arithmetic encode
                        assert(macroBlockModeInfo.Block.UseIntraBlockCopy);
                        assert(IsPaletteAllowed(pcs.Parent.FrameHeader.AllowScreenContentTools, blockGeometry.BlockSize));
                        svt_aom_get_block_dimensions(blockGeometry.BlockSize, plane, blk_ptr.MacroBlock, null, null, out int rowCount, out int columnCount);
                        pack_map_tokens(ref writer, ref entropyCodingContext.tok, palette_size_plane, rowCount * columnCount);

                        // advance the pointer
                        entropyCodingContext.tok = tok;
                        */
                    }
                }
            }

            if (frm_hdr.TransformMode == Av1TransformMode.Select)
            {
                // TODO: Implement when Selecting transform block size is supported.
                // CodeTransformSize(
                //    pcs,
                //    ref writer,
                //    blockOrigin,
                //    blk_ptr,
                //    blockGeometry,
                //    txfm_context_array,
                //    skipWritingCoefficients);
            }

            if (!skipWritingCoefficients)
            {
                EncodeCoefficients1d(
                    pcs,
                    entropyCodingContext,
                    ref writer,
                    entropyCodingContext.MacroBlockModeInfo,
                    blk_ptr,
                    blockOrigin,
                    intra_luma_mode,
                    blockSize,
                    coeff_ptr,
                    luma_dc_sign_level_coeff_na,
                    cr_dc_sign_level_coeff_na,
                    cb_dc_sign_level_coeff_na);
            }
        }

        // Neighbor state must be updated after all symbols for the block have used the preceding contexts.
        UpdateNeighbors(pcs, entropyCodingContext, blockOrigin, blk_ptr, tile_idx, blockSize);

        if (IsPaletteAllowed(pcs.Parent.PaletteLevel, blockGeometry.BlockSize))
        {
            /*
            // free ENCDEC palette info buffer
            assert(blk_ptr.palette_info.color_idx_map != null && "free palette:Null");
            EB_FREE(blk_ptr.palette_info.color_idx_map);
            blk_ptr.palette_info.color_idx_map = null;
            EB_FREE(blk_ptr.palette_info);*/
        }
    }

    /// <summary>
    /// Writes the chroma intra mode, chroma-from-luma alpha values, and directional angle adjustment for a block.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <param name="chromaMode">The selected chroma prediction mode.</param>
    /// <param name="isChromaFromLumaAllowed">A value indicating whether chroma-from-luma mode is available.</param>
    private static void EncodeIntraChromaMode(
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode,
        Av1PredictionMode chromaMode,
        bool isChromaFromLumaAllowed)
    {
        writer.WriteChromaMode(chromaMode, isChromaFromLumaAllowed, lumaMode);

        if (chromaMode == Av1PredictionMode.UvChromaFromLuma)
        {
            writer.WriteChromaFromLumaAlphas(
                blk_ptr.PredictionUnits[0].ChromaFromLumaIndex,
                blk_ptr.PredictionUnits[0].ChromaFromLumaSigns);
        }

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.UvMode.IsDirectional())
        {
            writer.WriteAngleDelta(blk_ptr.PredictionUnits[0].AngleDelta[(int)Av1PlaneType.Uv] + Av1Constants.MaxAngleDelta, chromaMode);
        }
    }

    /// <summary>
    /// Gets the above and left key-frame contexts used to write an intra luma mode.
    /// </summary>
    /// <param name="xd">The current macroblock and its mapped neighbors.</param>
    /// <param name="above_ctx">The context derived from the above luma mode.</param>
    /// <param name="left_ctx">The context derived from the left luma mode.</param>
    /// <remarks>Corresponds to <c>svt_aom_get_kf_y_mode_ctx</c> in SVT-AV1.</remarks>
    private static void GetYModeContext(Av1MacroBlockD xd, out byte above_ctx, out byte left_ctx)
    {
        Av1PredictionMode intraLumaLeftMode = Av1PredictionMode.DC;
        Av1PredictionMode intraLumaTopMode = Av1PredictionMode.DC;
        if (xd.IsLeftAvailable)
        {
            // Key-frame neighbors are intra blocks, so their luma modes directly select the context class.
            intraLumaLeftMode = xd.ModeInfo[-1].MacroBlockModeInfo.Block.Mode;
        }

        if (xd.IsUpAvailable)
        {
            intraLumaTopMode = xd.ModeInfo[-xd.ModeInfoStride].MacroBlockModeInfo.Block.Mode;
        }

        above_ctx = IntraModeContextLookup[(int)intraLumaTopMode];
        left_ctx = IntraModeContextLookup[(int)intraLumaLeftMode];
    }

    /// <summary>
    /// Writes the key-frame luma prediction mode and any directional angle adjustment.
    /// </summary>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="macroBlockModeInfo">The selected block modes.</param>
    /// <param name="blk_ptr">The encoder prediction-unit state.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="lumaMode">The selected luma prediction mode.</param>
    /// <remarks>Corresponds to <c>encode_intra_luma_mode_kf_av1</c> in SVT-AV1.</remarks>
    private static void EncodeIntraLumaMode(
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Av1PredictionMode lumaMode)
    {
        GetYModeContext(blk_ptr.MacroBlock, out byte topContext, out byte leftContext);
        writer.WriteLumaMode(lumaMode, topContext, leftContext);

        if (blockSize >= Av1BlockSize.Block8x8 && macroBlockModeInfo.Block.Mode.IsDirectional())
        {
            writer.WriteAngleDelta(blk_ptr.PredictionUnits[0].AngleDelta[(int)Av1PlaneType.Y] + Av1Constants.MaxAngleDelta, lumaMode);
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
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1EncoderBlockStruct blk_ptr,
        Av1BlockSize blockSize,
        Point point)
    {/*
        Av1PredictionMode intra_luma_mode = macroBlockModeInfo.Mode;
        Av1PredictionMode intra_chroma_mode = macroBlockModeInfo.ModeUv;

        Av1PaletteModeInfo pmi = blk_ptr.PaletteInfo.pmi;
        int bsize_ctx = svt_aom_get_palette_bsize_ctx(bsize);
        Guard.MustBeGreaterThanOrEqualTo(bsize_ctx, 0, nameof(bsize_ctx));
        if (intra_luma_mode == Av1PredictionMode.DC)
        {
            int n = blk_ptr.PaletteSize[0];
            int palette_y_mode_ctx = svt_aom_get_palette_mode_ctx(blk_ptr->av1xd);
            writer.WriteYMode(n > 0, bsize_ctx, palette_y_mode_ctx);
            if (n > 0)
            {
                writer.WriteYSize(n - PALETTE_MIN_SIZE, bsize_ctx);
                write_palette_colors_y(blk_ptr.MacroBlock, pmi, scs.StaticConfig.EncoderBitDepth, ref writer, n);
            }
        }

        bool uv_dc_pred = intra_chroma_mode == Av1PredictionMode.DC && is_chroma_reference(point, blockSize, 1, 1);
        if (uv_dc_pred)
        {
            // assert(blk_ptr->palette_size[1] == 0); //remove when chroma is on
            bool palette_uv_mode_ctx = blk_ptr.PaletteSize[0] > 0;
            writer.WriteUvMode(false, palette_uv_mode_ctx);
        }*/
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
    /// <remarks>Corresponds to <c>svt_aom_filter_intra_allowed</c> in SVT-AV1.</remarks>
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
    /// <remarks>Corresponds to <c>svt_aom_filter_intra_allowed_bsize</c> in SVT-AV1.</remarks>
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
    /// <param name="block">The encoder block state.</param>
    /// <exception cref="NotImplementedException">The displacement-vector syntax is not implemented when intra block copy is selected.</exception>
    /// <remarks>Corresponds to <c>write_intrabc_info</c> in SVT-AV1.</remarks>
    private static void WriteIntraBlockCopyInfo(
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo macroBlockModeInfo,
        Av1EncoderBlockStruct block)
    {
        bool use_intrabc = macroBlockModeInfo.Block.UseIntraBlockCopy;
        writer.WriteUseIntraBlockCopy(use_intrabc);
        if (use_intrabc)
        {
            throw new NotImplementedException("Intra block code encoding not implemented.");
            /*
            //assert(mbmi->mode == DC_PRED);
            //assert(mbmi->uv_mode == UV_DC_PRED);
            //assert(mbmi->motion_mode == SIMPLE_TRANSLATION);
            IntMv dv_ref = block->predmv[0]; // mbmi_ext->ref_mv_stack[INTRA_FRAME][0].this_mv;
            MV mv;
            mv = macroBlockModeInfo.Block.mv[INTRA_FRAME].as_mv;
            svt_av1_encode_dv(w, &mv, &dv_ref.as_mv, &ec_ctx->ndvc);*/
        }
    }

    /// <summary>
    /// Determines whether the current frame permits intra block copy.
    /// </summary>
    /// <param name="frameHeader">The current frame header.</param>
    /// <returns><see langword="true"/> when both screen-content tools and intra block copy are enabled; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Corresponds to <c>svt_aom_allow_intrabc</c> in SVT-AV1.</remarks>
    private static bool IsIntraBlockCopyAllowed(ObuFrameHeader frameHeader)
        => frameHeader.AllowScreenContentTools && frameHeader.AllowIntraBlockCopy;

    /// <summary>
    /// Updates partition and coefficient neighbor arrays after writing a block.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="tile_idx">The zero-based tile index.</param>
    /// <param name="blockSize">The block size.</param>
    /// <remarks>Corresponds to <c>ec_update_neighbors</c> in SVT-AV1.</remarks>
    private static void UpdateNeighbors(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        Point blockOrigin,
        Av1EncoderBlockStruct blk_ptr,
        ushort tile_idx,
        Av1BlockSize blockSize)
    {
        Av1NeighborArrayUnit<Av1PartitionContext> partition_context_na = pcs.PartitionContexts[tile_idx];
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na = pcs.LuminanceDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na = pcs.CrDcSignLevelCoefficientNeighbors[tile_idx];
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na = pcs.CbDcSignLevelCoefficientNeighbors[tile_idx];
        Av1BlockGeometry blk_geom = Av1BlockGeometryFactory.GetBlockGeometryByModeDecisionScanIndex(blk_ptr.ModeDecisionScanIndex);
        Av1MacroBlockModeInfo mbmi = pcs.GetMacroBlockModeInfo(blockOrigin);
        bool skip_coeff = mbmi.Block.Skip;

        // Store the block-size split mask across the edges that future partition symbols can observe.
        Av1PartitionContext partition = new(
            PartitionContextLookup[(int)blockSize].Above,
            PartitionContextLookup[(int)blockSize].Left);
        Size size = new(blk_geom.BlockWidth, blk_geom.BlockHeight);
        Span<Av1PartitionContext> partitionSpan = new(ref partition);
        partition_context_na.UnitModeWrite(
            partitionSpan,
            blockOrigin,
            size,
            Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left | Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top);
        if (skip_coeff)
        {
            // A skipped block has an all-zero residual, so publish a zero sign/level context over its edges
            // and advance coefficient positions without reading transform units.
            byte dcSignLevelCoefficient = 0;
            Span<byte> dcSignSpan = new(ref dcSignLevelCoefficient);

            luma_dc_sign_level_coeff_na.UnitModeWrite(
                dcSignSpan,
                blockOrigin,
                size,
                Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left | Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top);

            if (blk_geom.HasUv)
            {
                cb_dc_sign_level_coeff_na.UnitModeWrite(
                    dcSignSpan,
                    ((blockOrigin >> 3) << 3) >> 1,
                    size,
                    Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left | Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top);
                cr_dc_sign_level_coeff_na.UnitModeWrite(
                    dcSignSpan,
                    ((blockOrigin >> 3) << 3) >> 1,
                    size,
                    Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Left | Av1NeighborArrayUnit<Av1PartitionContext>.UnitMask.Top);
                entropyCodingContext.CodedAreaSuperblockUv += blk_geom.BlockWidthUv * blk_geom.BlockHeightUv;
            }

            entropyCodingContext.CodedAreaSuperblock += blk_geom.BlockWidth * blk_geom.BlockHeight;
        }
    }

    /// <summary>
    /// Determines whether the encoder palette level and block dimensions permit palette mode.
    /// </summary>
    /// <param name="allowPalette">The nonzero encoder palette level.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns><see langword="true"/> when palette mode is enabled for the block; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Corresponds to <c>svt_av1_allow_palette</c> in SVT-AV1.</remarks>
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
    /// <remarks>Corresponds to <c>svt_aom_allow_palette</c> in SVT-AV1.</remarks>
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
    /// <remarks>Corresponds to <c>write_cdef</c> in SVT-AV1.</remarks>
    private static void WriteCdef(
        Av1SequenceControlSet scs,
        Av1PictureControlSet pcs,
        ref Av1SymbolEncoder writer,
        int tileIndex,
        bool skip,
        Point modeInfoPosition)
    {
        Av1EncoderCommon cm = pcs.Parent.Common;
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;

        if (frameHeader.CodedLossless || frameHeader.AllowIntraBlockCopy)
        {
            // Lossless and intra-block-copy frames disable CDEF, so normalize the header to its single zero-strength form.
            frameHeader.CdefParameters.BitCount = 0;
            frameHeader.CdefParameters.YStrength[0] = 0;
            frameHeader.CdefParameters.UvStrength[0] = 0;

            // pcs.Parent.nb_cdef_strengths = 1;
            return;
        }

        // int m = ~((1 << (6 - Av1Constants.ModeInfoSizeLog2)) - 1);
        // cm->mi_grid_visible[(mi_row & m) * cm->mi_stride + (mi_col & m)];
        Av1ModeInfo mi = pcs.GetFromModeInfoGrid(modeInfoPosition)[0];

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
        int mask = 1 << (6 - Av1Constants.ModeInfoSizeLog2);
        int index = scs.SequenceHeader.Use128x128Superblock ? Math.Max(1, modeInfoPosition.X & mask) + (2 * Math.Max(1, modeInfoPosition.Y & mask)) : 0;

        if (pcs.CdefPreset[tileIndex][index] == -1 && !skip)
        {
            writer.WriteCdefStrength(mi.MacroBlockModeInfo.CdefStrength, frameHeader.CdefParameters.BitCount);
            pcs.CdefPreset[tileIndex][index] = mi.MacroBlockModeInfo.CdefStrength;
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
    /// <remarks>Corresponds to <c>set_mi_row_col</c> in SVT-AV1.</remarks>
    private static void SetModeInfoRowAndColumn(
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
        macroBlock.ToBottomEdge = ((modeInfoRowCount - blockSize.GetHeight() - modeInfoPosition.Y) << Av1Constants.ModeInfoSizeLog2) << 3;
        macroBlock.ToLeftEdge = -((modeInfoPosition.X << Av1Constants.ModeInfoSizeLog2) << 3);
        macroBlock.ToRightEdge = ((modeInfoColumnCount - blockSize.GetWidth() - modeInfoPosition.X) << Av1Constants.ModeInfoSizeLog2) << 3;

        macroBlock.ModeInfoStride = modeInfoStride;

        // Prediction cannot cross tile boundaries even when frame mode information exists there.
        macroBlock.IsUpAvailable = modeInfoPosition.Y > tile.ModeInfoRowStart;
        macroBlock.IsLeftAvailable = modeInfoPosition.X > tile.ModeInfoColumnStart;
        macroBlock.ModeInfo = pcs.GetFromModeInfoGrid(modeInfoPosition);

        if (macroBlock.IsUpAvailable)
        {
            macroBlock.AboveMacroBlock = macroBlock.ModeInfo[-modeInfoStride].MacroBlockModeInfo;
        }
        else
        {
            macroBlock.AboveMacroBlock = null;
        }

        if (macroBlock.IsLeftAvailable)
        {
            macroBlock.LeftMacroBlock = macroBlock.ModeInfo[-1].MacroBlockModeInfo;
        }
        else
        {
            macroBlock.LeftMacroBlock = null;
        }

        macroBlock.N8Size = new Size(blockSize.GetWidth(), blockSize.GetHeight());
        macroBlock.IsSecondRectangle = false;
        if (macroBlock.N8Size.Width < macroBlock.N8Size.Height)
        {
            // Only the last sub-block of a rectangular partition selects the secondary transform context.
            // Vertical-four therefore maps to (0, 0, 0, 1), while two-way partitions map to (0, 1).
            if (((modeInfoPosition.X + macroBlock.N8Size.Width) & (macroBlock.N8Size.Height - 1)) == 0)
            {
                macroBlock.IsSecondRectangle = true;
            }
        }

        if (macroBlock.N8Size.Width > macroBlock.N8Size.Height)
        {
            if ((modeInfoPosition.Y & (macroBlock.N8Size.Width - 1)) > 0)
            {
                macroBlock.IsSecondRectangle = true;
            }
        }
    }

    /// <summary>
    /// Writes luma and chroma transform coefficients for a block in plane order.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="ec_ctx">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="mbmi">The selected macroblock modes.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction.</param>
    /// <param name="planeBlockSize">The luma block size.</param>
    /// <param name="coeff_ptr">The transformed coefficients for the frame.</param>
    /// <param name="luma_dc_sign_level_coeff_na">The luma coefficient neighbor contexts.</param>
    /// <param name="cr_dc_sign_level_coeff_na">The red-difference chroma coefficient neighbor contexts.</param>
    /// <param name="cb_dc_sign_level_coeff_na">The blue-difference chroma coefficient neighbor contexts.</param>
    /// <exception cref="NotImplementedException">The transform-depth path required by the block is not implemented.</exception>
    /// <remarks>Corresponds to <c>av1_encode_coeff_1d</c> in SVT-AV1.</remarks>
    private static void EncodeCoefficients1d(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext ec_ctx,
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo mbmi,
        Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize planeBlockSize,
        Av1FrameBuffer<int> coeff_ptr,
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na)
    {
        if (mbmi.Block.TransformDepth != 0)
        {
            EncodeTransformCoefficientsY(
                pcs,
                ec_ctx,
                ref writer,
                mbmi,
                blk_ptr,
                blockOrigin,
                intraLumaDir,
                planeBlockSize,
                coeff_ptr,
                luma_dc_sign_level_coeff_na);

            EncodeTransformCoefficientsUv(
                pcs,
                ec_ctx,
                ref writer,
                mbmi,
                blk_ptr,
                blockOrigin,
                intraLumaDir,
                planeBlockSize,
                coeff_ptr,
                cr_dc_sign_level_coeff_na,
                cb_dc_sign_level_coeff_na);
        }
        else
        {
            throw new NotImplementedException("Only capable to encode Largest transform mode.");
        }
    }

    /// <summary>
    /// Writes each luma transform block and updates its DC-sign and coefficient-level neighbor contexts.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="mbmi">The selected macroblock modes.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction.</param>
    /// <param name="plane_bsize">The luma block size.</param>
    /// <param name="coeff_ptr">The transformed coefficients for the frame.</param>
    /// <param name="luma_dc_sign_level_coeff_na">The luma coefficient neighbor contexts.</param>
    /// <remarks>Corresponds to <c>av1_encode_tx_coef_y</c> in SVT-AV1.</remarks>
    public static void EncodeTransformCoefficientsY(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo mbmi,
        Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize plane_bsize,
        Av1FrameBuffer<int> coeff_ptr,
        Av1NeighborArrayUnit<byte> luma_dc_sign_level_coeff_na)
    {
        // This writer currently emits intra frames, so coefficient contexts use only intra prediction state.
        Av1BlockGeometry blockGeometry = Av1BlockGeometryFactory.GetBlockGeometryByModeDecisionScanIndex(blk_ptr.ModeDecisionScanIndex);
        int tx_depth = mbmi.Block.TransformDepth;
        int txb_count = blockGeometry.TransformBlockCount[mbmi.Block.TransformDepth];
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;

        for (int tx_index = 0; tx_index < txb_count; tx_index++)
        {
            int txb_itr = tx_index;

            Av1TransformSize tx_size = blockGeometry.TransformSize[tx_depth];

            int coeff1d_offset = entropyCodingContext.CodedAreaSuperblock;
            Span<int> coeff_buffer = coeff_ptr.BufferY!.DangerousGetSingleSpan()[coeff1d_offset..];

            Av1TransformBlockContext blockContext = new();
            Point transformOrigin = blockGeometry.TransformOrigin[tx_depth][txb_itr];
            GetTransformBlockContexts(
                pcs,
                Av1ComponentType.Luminance,
                luma_dc_sign_level_coeff_na,
                blockOrigin + (Size)transformOrigin - (Size)blockGeometry.Origin,
                plane_bsize,
                tx_size,
                blockContext);

            Av1TransformType tx_type = blk_ptr.TransformBlocks[txb_itr].TransformType[(int)Av1ComponentType.Luminance];
            int eob = blk_ptr.TransformBlocks[txb_itr].NzCoefficientCount[0];
            if (eob == 0)
            {
                // AV1 requires the canonical DCT_DCT transform type when a transform block has no coefficients.
                tx_type = blk_ptr.TransformBlocks[txb_itr].TransformType[(int)Av1PlaneType.Y] = Av1TransformType.DctDct;
                Guard.IsTrue(tx_type == Av1TransformType.DctDct, nameof(tx_type), string.Empty);
            }

            int cul_level_y = writer.WriteCoefficients(
                tx_size,
                tx_type,
                intraLumaDir,
                coeff_buffer,
                Av1ComponentType.Luminance,
                blockContext,
                (ushort)eob,
                frameHeader.UseReducedTransformSet,
                blk_ptr.FilterIntraMode);

            // WriteCoefficients packs the DC sign and cumulative level into one integer; publish its bytes
            // across the transform edges so the next blocks derive identical entropy contexts.
            Span<int> culLevelSpan = new(ref cul_level_y);
            ReadOnlySpan<byte> dc_sign_level_coeff = MemoryMarshal.AsBytes(culLevelSpan);

            int transformWidth = blockGeometry.TransformSize[tx_depth].GetWidth();
            int transformHeight = blockGeometry.TransformSize[tx_depth].GetHeight();
            luma_dc_sign_level_coeff_na.UnitModeWrite(
                dc_sign_level_coeff,
                blockOrigin + (Size)transformOrigin - (Size)blockGeometry.Origin,
                new Size(transformWidth, transformHeight),
                Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

            entropyCodingContext.CodedAreaSuperblock += transformWidth * transformHeight;
        }
    }

    /// <summary>
    /// Writes both chroma transform blocks and updates their DC-sign and coefficient-level neighbor contexts.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="entropyCodingContext">The entropy-coding position state for the superblock.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="mbmi">The selected macroblock modes.</param>
    /// <param name="blk_ptr">The encoder block state.</param>
    /// <param name="blockOrigin">The luma block origin in samples.</param>
    /// <param name="intraLumaDir">The luma prediction direction used by coefficient contexts.</param>
    /// <param name="plane_bsize">The luma block size.</param>
    /// <param name="coeff_ptr">The transformed coefficients for the frame.</param>
    /// <param name="cr_dc_sign_level_coeff_na">The red-difference chroma coefficient neighbor contexts.</param>
    /// <param name="cb_dc_sign_level_coeff_na">The blue-difference chroma coefficient neighbor contexts.</param>
    /// <remarks>Corresponds to <c>av1_encode_tx_coef_uv</c> in SVT-AV1.</remarks>
    private static void EncodeTransformCoefficientsUv(
        Av1PictureControlSet pcs,
        Av1EntropyCodingContext entropyCodingContext,
        ref Av1SymbolEncoder writer,
        Av1MacroBlockModeInfo mbmi,
        Av1EncoderBlockStruct blk_ptr,
        Point blockOrigin,
        Av1PredictionMode intraLumaDir,
        Av1BlockSize plane_bsize,
        Av1FrameBuffer<int> coeff_ptr,
        Av1NeighborArrayUnit<byte> cr_dc_sign_level_coeff_na,
        Av1NeighborArrayUnit<byte> cb_dc_sign_level_coeff_na)
    {
        Av1BlockGeometry blockGeometry = Av1BlockGeometryFactory.GetBlockGeometryByModeDecisionScanIndex(blk_ptr.ModeDecisionScanIndex);

        if (!blockGeometry.HasUv)
        {
            return;
        }

        int tx_depth = mbmi.Block.TransformDepth;
        uint txb_count = 1;
        ObuFrameHeader frameHeader = pcs.Parent.FrameHeader;
        int transformWidth = blockGeometry.TransformSize[tx_depth].GetWidth();
        int transformHeight = blockGeometry.TransformSize[tx_depth].GetHeight();

        for (uint tx_index = 0; tx_index < txb_count; ++tx_index)
        {
            Av1TransformSize chroma_tx_size = blockGeometry.TransformSizeUv[tx_depth];

            if (blockGeometry.HasUv)
            {
                // Both chroma planes share transform geometry but retain independent coefficient contexts.
                Span<int> coeff_buffer = coeff_ptr.BufferCb!.DangerousGetSingleSpan().Slice(entropyCodingContext.CodedAreaSuperblockUv);
                Av1TransformBlockContext blockContext = new();
                Point transformOrigin = blockGeometry.TransformOrigin[tx_depth][tx_index];
                GetTransformBlockContexts(
                    pcs,
                    Av1ComponentType.Chroma,
                    cb_dc_sign_level_coeff_na,
                    RoundUv(blockOrigin + (Size)transformOrigin - (Size)blockGeometry.Origin) >> 1,
                    blockGeometry.BlockSizeUv,
                    chroma_tx_size,
                    blockContext);
                Av1TransformType chroma_tx_type = blk_ptr.TransformBlocks[tx_index].TransformType[(int)Av1ComponentType.Chroma];
                int endOfBlockCb = blk_ptr.TransformBlocks[tx_index].NzCoefficientCount[1];
                int cul_level_cb = writer.WriteCoefficients(
                    chroma_tx_size,
                    chroma_tx_type,
                    intraLumaDir,
                    coeff_buffer,
                    Av1ComponentType.Chroma,
                    blockContext,
                    (ushort)endOfBlockCb,
                    frameHeader.UseReducedTransformSet,
                    blk_ptr.FilterIntraMode);

                coeff_buffer = coeff_ptr.BufferCr!.DangerousGetSingleSpan().Slice(entropyCodingContext.CodedAreaSuperblockUv);
                blockContext = new();
                int endOfBlockCr = blk_ptr.TransformBlocks[tx_index].NzCoefficientCount[2];

                GetTransformBlockContexts(
                    pcs,
                    Av1ComponentType.Chroma,
                    cr_dc_sign_level_coeff_na,
                    RoundUv(blockOrigin + (Size)transformOrigin - (Size)blockGeometry.Origin) >> 1,
                    blockGeometry.BlockSizeUv,
                    chroma_tx_size,
                    blockContext);

                int cul_level_cr = writer.WriteCoefficients(
                    chroma_tx_size,
                    chroma_tx_type,
                    intraLumaDir,
                    coeff_buffer,
                    Av1ComponentType.Chroma,
                    blockContext,
                    (ushort)endOfBlockCr,
                    frameHeader.UseReducedTransformSet,
                    blk_ptr.FilterIntraMode);

                // Publish each plane's packed sign/level summary across its transform edges.
                Span<int> culLevelCbSpan = new(ref cul_level_cb);
                ReadOnlySpan<byte> dc_sign_level_coeff = MemoryMarshal.AsBytes(culLevelCbSpan);
                cb_dc_sign_level_coeff_na.UnitModeWrite(
                    dc_sign_level_coeff,
                    RoundUv(transformOrigin) >> 1,
                    new Size(transformWidth, transformHeight),
                    Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

                Span<int> culLevelCrSpan = new(ref cul_level_cr);
                dc_sign_level_coeff = MemoryMarshal.AsBytes(culLevelCrSpan);
                cr_dc_sign_level_coeff_na.UnitModeWrite(
                    dc_sign_level_coeff,
                    RoundUv(transformOrigin) >> 1,
                    new Size(transformWidth, transformHeight),
                    Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);
            }

            entropyCodingContext.CodedAreaSuperblockUv += transformWidth * transformHeight;
        }
    }

    /// <summary>
    /// Rounds a luma sample position down to the 8-sample alignment used before chroma subsampling.
    /// </summary>
    /// <param name="point">The luma sample position.</param>
    /// <returns>The aligned luma position.</returns>
    private static Point RoundUv(Point point) => (point >> 3) << 3;

    /// <summary>
    /// Derives coefficient skip and DC-sign contexts from the transform block's above and left neighbors.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="plane">The luma or chroma component class.</param>
    /// <param name="dcSignLevelCoefficientNeighborArray">The packed DC-sign and coefficient-level neighbor contexts.</param>
    /// <param name="blockOrigin">The transform-block origin in samples of the target plane.</param>
    /// <param name="planeBlockSize">The containing block size on the target plane.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="blockContext">The context object to populate.</param>
    /// <remarks>Corresponds to <c>svt_aom_get_txb_ctx</c> in SVT-AV1.</remarks>
    private static void GetTransformBlockContexts(
        Av1PictureControlSet pcs,
        Av1ComponentType plane,
        Av1NeighborArrayUnit<byte> dcSignLevelCoefficientNeighborArray,
        Point blockOrigin,
        Av1BlockSize planeBlockSize,
        Av1TransformSize transformSize,
        Av1TransformBlockContext blockContext)
    {
        int dcSignLevelCoefficientLeftNeighborIndex = dcSignLevelCoefficientNeighborArray.GetLeftIndex(blockOrigin);
        int dcSignLevelCoefficientTopNeighborIndex = dcSignLevelCoefficientNeighborArray.GetTopIndex(blockOrigin);

        sbyte[] signs = [0, -1, 1];
        int transformBlockWidth;
        int transformBlockHeight;
        if (plane != Av1ComponentType.Luminance)
        {
            transformBlockWidth = Math.Min(transformSize.GetWidth(), ((pcs.Parent.AlignedWidth / 2) - blockOrigin.X) >> 2);
            transformBlockHeight = Math.Min(transformSize.GetHeight(), ((pcs.Parent.AlignedHeight / 2) - blockOrigin.Y) >> 2);
        }
        else
        {
            transformBlockWidth = Math.Min(transformSize.GetWidth(), (pcs.Parent.AlignedWidth - blockOrigin.X) >> 2);
            transformBlockHeight = Math.Min(transformSize.GetHeight(), (pcs.Parent.AlignedHeight - blockOrigin.Y) >> 2);
        }

        short dc_sign = 0;
        ushort k = 0;

        byte sign;

        // The high bits encode the DC sign class: zero, negative, or positive. Summing classes over
        // both edges selects whether neighboring DC coefficients bias the current sign context.
        if (dcSignLevelCoefficientNeighborArray.Top[dcSignLevelCoefficientTopNeighborIndex] != Av1NeighborArrayUnit<byte>.InvalidNeighborData)
        {
            do
            {
                sign = (byte)(dcSignLevelCoefficientNeighborArray.Top[k + dcSignLevelCoefficientTopNeighborIndex] >>
                        Av1Constants.CoefficientContextBitCount);
                Guard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
                dc_sign += signs[sign];
            }
            while (++k < transformBlockWidth);
        }

        if (dcSignLevelCoefficientNeighborArray.Left[dcSignLevelCoefficientLeftNeighborIndex] != Av1NeighborArrayUnit<byte>.InvalidNeighborData)
        {
            k = 0;
            do
            {
                sign = (byte)(dcSignLevelCoefficientNeighborArray.Left[k + dcSignLevelCoefficientLeftNeighborIndex] >>
                        Av1Constants.CoefficientContextBitCount);
                Guard.MustBeLessThanOrEqualTo(sign, (byte)2, nameof(sign));
                dc_sign += signs[sign];
            }
            while (++k < transformBlockHeight);
        }

        if (dc_sign > 0)
        {
            blockContext.DcSignContext = 2;
        }
        else if (dc_sign < 0)
        {
            blockContext.DcSignContext = 1;
        }
        else
        {
            blockContext.DcSignContext = 0;
        }

        if (plane == Av1ComponentType.Luminance)
        {
            if (planeBlockSize == transformSize.ToBlockSize())
            {
                blockContext.SkipContext = 0;
            }
            else
            {
                // Luma skip contexts depend on the minimum and union of the clipped edge levels.
                byte[][] skip_contexts = [
                    [1, 2, 2, 2, 3], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 5], [1, 4, 4, 4, 6]
                ];
                int top = 0;
                int left = 0;

                k = 0;
                if (dcSignLevelCoefficientNeighborArray.Top[dcSignLevelCoefficientTopNeighborIndex] !=
                    Av1NeighborArrayUnit<byte>.InvalidNeighborData)
                {
                    do
                    {
                        top |= dcSignLevelCoefficientNeighborArray.Top[k + dcSignLevelCoefficientTopNeighborIndex];
                    }
                    while (++k < transformBlockWidth);
                }

                top &= Av1Constants.CoefficientContextMask;

                if (dcSignLevelCoefficientNeighborArray.Left[dcSignLevelCoefficientLeftNeighborIndex] !=
                    Av1NeighborArrayUnit<byte>.InvalidNeighborData)
                {
                    k = 0;
                    do
                    {
                        left |= dcSignLevelCoefficientNeighborArray.Left[k + dcSignLevelCoefficientLeftNeighborIndex];
                    }
                    while (++k < transformBlockHeight);
                }

                left &= Av1Constants.CoefficientContextMask;
                int max = Math.Min(top | left, 4);
                int min = Math.Min(Math.Min(top, left), 4);

                blockContext.SkipContext = skip_contexts[min][max];
            }
        }
        else
        {
            // Chroma contexts use only the presence of nonzero levels on each edge, plus an offset
            // that distinguishes a transform smaller than its containing plane block.
            short ctx_base_left = 0;
            short ctx_base_top = 0;

            if (dcSignLevelCoefficientNeighborArray.Top[dcSignLevelCoefficientTopNeighborIndex] !=
                Av1NeighborArrayUnit<byte>.InvalidNeighborData)
            {
                k = 0;
                do
                {
                    ctx_base_top +=
                        (dcSignLevelCoefficientNeighborArray.Top[k + dcSignLevelCoefficientTopNeighborIndex] != 0) ? (short)1 : (short)0;
                }
                while (++k < transformBlockWidth);
            }

            if (dcSignLevelCoefficientNeighborArray.Left[dcSignLevelCoefficientLeftNeighborIndex] !=
                Av1NeighborArrayUnit<byte>.InvalidNeighborData)
            {
                k = 0;
                do
                {
                    ctx_base_left += dcSignLevelCoefficientNeighborArray.Left[k + dcSignLevelCoefficientLeftNeighborIndex] != 0 ? (short)1 : (short)0;
                }
                while (++k < transformBlockHeight);
            }

            int ctx_base = ((ctx_base_left != 0) ? 1 : 0) + ((ctx_base_top != 0) ? 1 : 0);
            int ctx_offset = planeBlockSize.GetPelsLog2Count() > transformSize.ToBlockSize().GetPelsLog2Count() ? 10 : 7;
            blockContext.SkipContext = (short)(ctx_base + ctx_offset);
        }
    }

    /// <summary>
    /// Writes or predicts a block segment identifier and updates the frame segmentation map.
    /// </summary>
    /// <param name="pcs">The picture coding state.</param>
    /// <param name="writer">The tile symbol encoder.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="blockOrigin">The block origin in samples.</param>
    /// <param name="block">The encoder block state.</param>
    /// <param name="skip">A value indicating whether residual coefficients are omitted.</param>
    private static void WriteSegmentId(Av1PictureControlSet pcs, ref Av1SymbolEncoder writer, Av1BlockSize blockSize, Point blockOrigin, Av1EncoderBlockStruct block, bool skip)
    {
        ObuSegmentationParameters segmentation_params = pcs.Parent.FrameHeader.SegmentationParameters;
        if (!segmentation_params.Enabled)
        {
            return;
        }

        int spatial_pred = GetSpatialSegmentationPrediction(pcs, block.MacroBlock, blockOrigin, out int cdf_num);
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
    /// <remarks>Corresponds to <c>svt_av1_get_spatial_seg_prediction</c> in SVT-AV1.</remarks>
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
        Span<byte> segmentation_map = pcs.SegmentationNeighborMap;

        if (up_available && left_available)
        {
            prev_ul = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_row - 1, mi_col - 1));
        }

        if (up_available)
        {
            prev_u = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_row - 1, mi_col - 0));
        }

        if (left_available)
        {
            prev_l = Av1SymbolContextHelper.GetSegmentId(cm, segmentation_map, Av1BlockSize.Block4x4, new Point(mi_row - 0, mi_col - 1));
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
    /// <param name="block">The encoder block state.</param>
    /// <param name="skip">The skip value to write.</param>
    internal static void EncodeSkipCoefficients(ref Av1SymbolEncoder writer, Av1EncoderBlockStruct block, bool skip)
    {
        Av1MacroBlockModeInfo? above_mi = block.MacroBlock.AboveMacroBlock;
        Av1MacroBlockModeInfo? left_mi = block.MacroBlock.LeftMacroBlock;
        int above_skip = (above_mi != null && above_mi.Block.Skip) ? 1 : 0;
        int left_skip = (left_mi != null && left_mi.Block.Skip) ? 1 : 0;
        writer.WriteSkip(skip, above_skip + left_skip);
    }
}
