// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    /// Gets or sets the row stride of <see cref="ModeInfoGrid"/> in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoStride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mode-information backing store uses 8x8 rather than 4x4 granularity.
    /// </summary>
    public bool Disallow4x4AllFrames { get; set; }

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter presets for each filter block.
    /// </summary>
    public required int[][] CdefPreset { get; set; }

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
    {
        Av1EncoderCommon cm = this.Parent.Common;
        Span<byte> segment_ids = this.SegmentationNeighborMap.Span;
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
