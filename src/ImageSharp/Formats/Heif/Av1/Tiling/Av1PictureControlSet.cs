// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds the coding decisions, buffers, and sequence context for one AV1 picture pass.
/// </summary>
internal class Av1PictureControlSet
{
    /// <summary>
    /// Gets or sets the partition neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<Av1PartitionContext>[] PartitionContexts { get; internal set; }

    /// <summary>
    /// Gets or sets the luma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] LuminanceDcSignLevelCoefficientNeighbors { get; internal set; }

    /// <summary>
    /// Gets or sets the red-difference chroma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] CrDcSignLevelCoefficientNeighbors { get; internal set; }

    /// <summary>
    /// Gets or sets the blue-difference chroma DC-sign and coefficient-level neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] CbDcSignLevelCoefficientNeighbors { get; internal set; }

    /// <summary>
    /// Gets or sets the transform-function neighbor contexts for each tile.
    /// </summary>
    public required Av1NeighborArrayUnit<byte>[] TransformFunctionContexts { get; internal set; }

    /// <summary>
    /// Gets or sets the sequence-wide encoder state.
    /// </summary>
    public required Av1SequenceControlSet Sequence { get; internal set; }

    /// <summary>
    /// Gets or sets the parent picture state shared across coding passes.
    /// </summary>
    public required Av1PictureParentControlSet Parent { get; internal set; }

    /// <summary>
    /// Gets or sets the frame segmentation identifiers used for spatial prediction.
    /// </summary>
    public required byte[] SegmentationNeighborMap { get; internal set; }

    /// <summary>
    /// Gets the frame grid that maps each 4x4 position to its mode-information span.
    /// </summary>
    public Av1ModeInfo[][] ModeInfoGrid { get; } = [];

    /// <summary>
    /// Gets or sets the contiguous mode-information storage addressed by <see cref="ModeInfoGrid"/>.
    /// </summary>
    public required Av1ModeInfo[] Mip { get; internal set; }

    /// <summary>
    /// Gets or sets the row stride of <see cref="ModeInfoGrid"/> in 4x4 mode-information units.
    /// </summary>
    public int ModeInfoStride { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mode-information backing store uses 8x8 rather than 4x4 granularity.
    /// </summary>
    public bool Disallow4x4AllFrames { get; internal set; }

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter presets for each filter block.
    /// </summary>
    public required int[][] CdefPreset { get; internal set; }

    /// <summary>
    /// Gets the mode-information span mapped to a frame position.
    /// </summary>
    /// <param name="position">The frame position in 4x4 mode-information units.</param>
    /// <returns>The mode-information span beginning at the position.</returns>
    public Span<Av1ModeInfo> GetFromModeInfoGrid(Point position)
        => this.ModeInfoGrid[(position.Y * this.ModeInfoStride) + position.X];

    /// <summary>
    /// Maps a frame position to the supplied mode-information span.
    /// </summary>
    /// <param name="position">The frame position in 4x4 mode-information units.</param>
    /// <param name="span">The mode-information entries to map.</param>
    public void SetModeInfoGridRow(Point position, Span<Av1ModeInfo> span)
        => this.SetModeInfoGridRow((position.Y * this.ModeInfoStride) + position.X, span);

    /// <summary>
    /// Maps a linear grid offset to the supplied mode-information span.
    /// </summary>
    /// <param name="offset">The linear grid offset.</param>
    /// <param name="span">The mode-information entries to map.</param>
    public void SetModeInfoGridRow(int offset, Span<Av1ModeInfo> span)
    {
        // Grid entries own their arrays because the source span can refer to temporary traversal state.
        this.ModeInfoGrid[offset] = new Av1ModeInfo[span.Length];
        span.CopyTo(this.ModeInfoGrid[offset]);
    }

    /// <summary>
    /// Gets the macroblock mode information at a block origin and refreshes its grid mapping.
    /// </summary>
    /// <param name="blockOrigin">The block origin in 4x4 mode-information units.</param>
    /// <returns>The macroblock mode information at the origin.</returns>
    internal Av1MacroBlockModeInfo GetMacroBlockModeInfo(Point blockOrigin)
    {
        int modeInfoStride = this.ModeInfoStride;
        int offset = (blockOrigin.Y * modeInfoStride) + blockOrigin.X;

        // Rectangular mode-decision blocks can replace grid entries. Restore the entry from the
        // contiguous backing store, whose index is halved when 4x4 blocks are globally disabled.
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int mipOffset = ((blockOrigin.Y >> disallow4x4) * (modeInfoStride >> disallow4x4)) + (blockOrigin.X >> disallow4x4);
        this.SetModeInfoGridRow(offset, ((Span<Av1ModeInfo>)this.Mip)[mipOffset..]);

        // The first mapped entry owns the macroblock state for the entire block.
        Av1ModeInfo modeInfo = this.ModeInfoGrid[offset][0];
        return modeInfo.MacroBlockModeInfo;
    }

    /// <summary>
    /// Writes a segment identifier to every segmentation-map entry covered by a block.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <param name="origin">The block origin in samples.</param>
    /// <param name="segmentId">The segment identifier.</param>
    internal void UpdateSegmentation(Av1BlockSize blockSize, Point origin, int segmentId)
    {
        Av1EncoderCommon cm = this.Parent.Common;
        Span<byte> segment_ids = this.SegmentationNeighborMap;
        int mi_col = origin.X >> Av1Constants.ModeInfoSizeLog2;
        int mi_row = origin.Y >> Av1Constants.ModeInfoSizeLog2;
        int mi_offset = (mi_row * cm.ModeInfoColumnCount) + mi_col;
        int bw = blockSize.GetWidth();
        int bh = blockSize.GetHeight();
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
