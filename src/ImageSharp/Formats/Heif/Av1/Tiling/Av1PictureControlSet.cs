// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds the coding decisions, buffers, and sequence context for one AV1 picture pass.
/// </summary>
internal class Av1PictureControlSet
{
    /// <summary>
    /// Gets or sets the partition neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<Av1PartitionContext>[] PartitionContexts { get; set; }

    /// <summary>
    /// Gets or sets the luma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] LuminanceDcSignLevelCoefficientNeighbors { get; set; }

    /// <summary>
    /// Gets or sets the red-difference chroma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] CrDcSignLevelCoefficientNeighbors { get; set; }

    /// <summary>
    /// Gets or sets the blue-difference chroma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] CbDcSignLevelCoefficientNeighbors { get; set; }

    /// <summary>
    /// Gets or sets the transform-function neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] TransformFunctionContexts { get; set; }

    /// <summary>
    /// Gets or sets the palette sizes and base colors exposed by the above and left block edges for each tile.
    /// </summary>
    public Av1NeighborArrayUnit<Av1EncoderPaletteInfo>[] PaletteContexts { get; set; } = [];

    /// <summary>
    /// Gets or sets the sequence-wide encoder state.
    /// </summary>
    public required Av1SequenceControlSet Sequence { get; set; }

    /// <summary>
    /// Gets or sets the parent picture state shared across coding passes.
    /// </summary>
    public required Av1PictureParentControlSet Parent { get; set; }

    /// <summary>
    /// Gets or sets the frame segmentation identifiers used for spatial prediction.
    /// </summary>
    public required Memory<byte> SegmentationNeighborMap { get; set; }

    /// <summary>
    /// Gets or sets the frame grid that maps each 4x4 position to its mode-information allocation index.
    /// </summary>
    public required Memory<int> ModeInfoGrid { get; set; }

    /// <summary>
    /// Gets or sets the contiguous mode-information storage addressed by <see cref="ModeInfoGrid"/>.
    /// </summary>
    public required Memory<Av1MacroBlockModeInfo> ModeInfoAllocation { get; set; }

    /// <summary>
    /// Gets or sets the packed displacement vectors present only when the frame permits intra-block copy.
    /// </summary>
    public Memory<Av1EncoderDisplacementVector> DisplacementVectors { get; set; }

    /// <summary>
    /// Gets or sets the reference contexts retained at each allocated block origin.
    /// </summary>
    public Memory<Av1EncoderReferenceContext> ReferenceContexts { get; set; }

    /// <summary>
    /// Gets or sets the prediction parameters retained at each allocated block origin.
    /// </summary>
    public Memory<Av1EncoderBlockStruct> BlockEncodings { get; set; }

    /// <summary>
    /// Gets or sets the palette colors retained at each allocated block origin.
    /// </summary>
    public Memory<Av1EncoderPaletteInfo> BlockPalettes { get; set; }

    /// <summary>
    /// Gets or sets the packed color-map tokens retained for final syntax packing.
    /// </summary>
    public Memory<byte> PaletteTokens { get; set; }

    /// <summary>
    /// Gets or sets the non-owning visible-frame hash index used by intra-block-copy motion search.
    /// </summary>
    public Av1IntraBlockCopySearchIndex IntraBlockCopySearch { get; set; }

    /// <summary>
    /// Gets or sets the row stride of <see cref="ModeInfoGrid"/> in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoStride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mode-information backing store uses 8x8 rather than 4x4 granularity.
    /// </summary>
    public bool Disallow4x4AllFrames { get; set; }

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter presets for each tile.
    /// Each tile occupies <see cref="Av1Constants.CdefUnitsPerSuperblock"/> consecutive entries.
    /// </summary>
    public required Memory<int> CdefPreset { get; set; }

    /// <summary>
    /// Gets or sets the starting byte of each tile in the shared encoded output buffer.
    /// </summary>
    public required Memory<int> TileDataOffsets { get; set; }

    /// <summary>
    /// Gets or sets the encoded byte length of each tile.
    /// </summary>
    public required Memory<int> TileDataLengths { get; set; }

    /// <summary>
    /// Gets or sets the selected restoration units of every component plane, plane after plane. A loop over the planes
    /// reads this span once and slices each plane at its <see cref="RestorationUnitOffsets"/> entry for the plane's unit count.
    /// Reference: the unit_info of each rst_info plane.
    /// </summary>
    public Memory<Av1LoopRestorationUnit> RestorationUnits { get; set; }

    /// <summary>
    /// Gets or sets the offset of each component plane's units in <see cref="RestorationUnits"/>.
    /// </summary>
    public InlineArray3<int> RestorationUnitOffsets { get; set; }

    /// <summary>
    /// Gets or sets the per-tile, per-plane restoration coefficient histories.
    /// </summary>
    public Memory<Av1LoopRestorationUnit> RestorationReferences { get; set; }

    /// <summary>
    /// Restores initial tile entropy edges while preserving selected block decisions and reconstruction.
    /// </summary>
    public void ResetEntropyContexts()
    {
        this.SegmentationNeighborMap.Span.Clear();
        for (int tileIndex = 0; tileIndex < this.PartitionContexts.Length; tileIndex++)
        {
            this.PartitionContexts[tileIndex].Clear();
            this.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].Clear();
            this.CbDcSignLevelCoefficientNeighbors[tileIndex].Clear();
            this.CrDcSignLevelCoefficientNeighbors[tileIndex].Clear();

            // Transform contexts use the maximum-size sentinel until a preceding block supplies a size.
            this.TransformFunctionContexts[tileIndex].Fill((byte)Av1Constants.MaxTransformSize);
        }

        foreach (Av1NeighborArrayUnit<Av1EncoderPaletteInfo> context in this.PaletteContexts)
        {
            context.Clear();
        }

        this.CdefPreset.Span.Fill(-1);
        this.Parent.PreviousQIndex.Span.Fill(this.Parent.FrameHeader.QuantizationParameters.BaseQIndex);
        this.RestorationReferences.Span.Fill(Av1LoopRestorationUnit.CreateDefault());
    }

    /// <summary>
    /// Gets the mode-information entry mapped to a frame position.
    /// </summary>
    /// <param name="position">The frame position in 4x4 mode-information units.</param>
    /// <returns>A reference to the mapped mode-information entry.</returns>
    public ref Av1MacroBlockModeInfo GetFromModeInfoGrid(Point position)
    {
        int gridOffset = (position.Y * this.ModeInfoStride) + position.X;
        int allocationOffset = this.ModeInfoGrid.Span[gridOffset];
        return ref this.ModeInfoAllocation.Span[allocationOffset];
    }

    /// <summary>
    /// Gets the displacement vector mapped to a frame position.
    /// </summary>
    /// <param name="position">The frame position in 4x4 mode-information units.</param>
    /// <returns>The displacement vector retained for the covering block.</returns>
    public Av1MotionVector GetDisplacementVector(Point position)
    {
        int gridOffset = (position.Y * this.ModeInfoStride) + position.X;
        int allocationOffset = this.ModeInfoGrid.Span[gridOffset];
        Av1EncoderDisplacementVector vector = this.DisplacementVectors.Span[allocationOffset];
        return new Av1MotionVector(vector.Row, vector.Column);
    }

    /// <summary>
    /// Gets the secondary displacement vector mapped to a compound block.
    /// </summary>
    /// <param name="position">The frame position in 4x4 mode-information units.</param>
    /// <returns>The secondary vector retained for the covering block.</returns>
    public Av1MotionVector GetSecondaryDisplacementVector(Point position)
    {
        int gridOffset = (position.Y * this.ModeInfoStride) + position.X;
        int allocationOffset = this.ModeInfoGrid.Span[gridOffset];
        Av1EncoderDisplacementVector vector = this.ReferenceContexts.Span[allocationOffset].SecondaryVector;
        return new Av1MotionVector(vector.Row, vector.Column);
    }

    /// <summary>
    /// Stores the displacement vector selected at a block origin.
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <param name="vector">The selected integer displacement vector.</param>
    public void SetDisplacementVector(Point modeInfoPosition, Av1MotionVector vector)
    {
        int modeInfoStride = this.ModeInfoStride;
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int allocationOffset = ((modeInfoPosition.Y >> disallow4x4) * (modeInfoStride >> disallow4x4)) + (modeInfoPosition.X >> disallow4x4);
        this.DisplacementVectors.Span[allocationOffset] = new Av1EncoderDisplacementVector
        {
            Row = (short)vector.Row,
            Column = (short)vector.Column
        };
    }

    /// <summary>
    /// Stores the secondary displacement vector selected at a compound block origin.
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <param name="vector">The selected secondary vector.</param>
    public void SetSecondaryDisplacementVector(Point modeInfoPosition, Av1MotionVector vector)
    {
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int allocationOffset = ((modeInfoPosition.Y >> disallow4x4) * (this.ModeInfoStride >> disallow4x4)) +
            (modeInfoPosition.X >> disallow4x4);

        this.ReferenceContexts.Span[allocationOffset].SecondaryVector = new Av1EncoderDisplacementVector
        {
            Row = (short)vector.Row,
            Column = (short)vector.Column
        };
    }

    /// <summary>
    /// Gets the macroblock mode information allocated at a block origin.
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <returns>A reference to the macroblock mode information at the origin.</returns>
    public ref Av1MacroBlockModeInfo GetMacroBlockModeInfo(Point modeInfoPosition)
    {
        int modeInfoStride = this.ModeInfoStride;
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int allocationOffset = ((modeInfoPosition.Y >> disallow4x4) * (modeInfoStride >> disallow4x4)) + (modeInfoPosition.X >> disallow4x4);
        return ref this.ModeInfoAllocation.Span[allocationOffset];
    }

    /// <summary>
    /// Maps every coded 4x4 position covered by a block to the block's mode-information allocation entry.
    /// </summary>
    /// <param name="modeInfoPosition">The block position in 4x4 mode-information units.</param>
    /// <param name="blockSize">The coded block size.</param>
    public void MapModeInfoBlock(Point modeInfoPosition, Av1BlockSize blockSize)
    {
        int modeInfoStride = this.ModeInfoStride;
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int allocationOffset = ((modeInfoPosition.Y >> disallow4x4) * (modeInfoStride >> disallow4x4)) + (modeInfoPosition.X >> disallow4x4);
        int mappedWidth = Math.Min(this.Parent.Common.ModeInfoColumnCount - modeInfoPosition.X, blockSize.Get4x4WideCount());
        int mappedHeight = Math.Min(this.Parent.Common.ModeInfoRowCount - modeInfoPosition.Y, blockSize.Get4x4HighCount());
        Span<int> grid = this.ModeInfoGrid.Span;

        // Libaom's pointer grid aliases every covered 4x4 entry to one mode-info allocation. Integer indices keep
        // the same aliasing without one managed object and one managed reference per grid position.
        for (int row = 0; row < mappedHeight; row++)
        {
            int gridOffset = ((modeInfoPosition.Y + row) * modeInfoStride) + modeInfoPosition.X;
            grid.Slice(gridOffset, mappedWidth).Fill(allocationOffset);
        }
    }

    /// <summary>
    /// Writes a segment identifier to every segmentation-map entry covered by a block.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="segmentId">The segment identifier.</param>
    public void UpdateSegmentation(Av1BlockSize blockSize, Point origin, int segmentId)
        => this.UpdateSegmentation(this.SegmentationNeighborMap.Span, blockSize, origin, segmentId);

    /// <summary>
    /// Writes a segment identifier to every entry of a segment map that a block covers. Reference: set_segment_id().
    /// </summary>
    /// <param name="segment_ids">The segment map, one entry per 4x4 block.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="segmentId">The segment identifier.</param>
    public void UpdateSegmentation(Span<byte> segment_ids, Av1BlockSize blockSize, Point origin, int segmentId)
    {
        Av1EncoderCommon cm = this.Parent.Common;
        int mi_col = origin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_row = origin.Y >> Av1Constants.ModeInfoSizeLog2;
        int mi_offset = (mi_row * cm.ModeInfoColumnCount) + mi_col;
        int bw = blockSize.Get4x4WideCount();
        int bh = blockSize.Get4x4HighCount();
        int xmis = Math.Min(cm.ModeInfoColumnCount - mi_col, bw);
        int ymis = Math.Min(cm.ModeInfoRowCount - mi_row, bh);

        // Clip edge blocks to the coded mode-information grid before filling complete rows.
        for (int y = 0; y < ymis; ++y)
        {
            int offset = mi_offset + (y * cm.ModeInfoColumnCount);
            segment_ids.Slice(offset, xmis).Fill((byte)segmentId);
        }
    }
}
