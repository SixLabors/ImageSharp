// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PictureControlSet
{
    public required Av1NeighborArrayUnit<Av1PartitionContext>[] PartitionContexts { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] LuminanceDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] CrDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] CbDcSignLevelCoefficientNeighbors { get; internal set; }

    public required Av1NeighborArrayUnit<byte>[] TransformFunctionContexts { get; internal set; }

    public required Av1SequenceControlSet Sequence { get; internal set; }

    public required Av1PictureParentControlSet Parent { get; internal set; }

    public required byte[] SegmentationNeighborMap { get; internal set; }

    public Av1ModeInfo[][] ModeInfoGrid { get; } = [];

    public required Av1ModeInfo[] Mip { get; internal set; }

    public int ModeInfoStride { get; internal set; }

    // true if 4x4 blocks are disallowed for all frames, and NSQ is disabled (since granularity is
    // needed for 8x8 NSQ blocks).  Used to compute the offset for mip.
    public bool Disallow4x4AllFrames { get; internal set; }

    public required int[][] CdefPreset { get; internal set; }

    public Span<Av1ModeInfo> GetFromModeInfoGrid(Point position)
        => this.ModeInfoGrid[(position.Y * this.ModeInfoStride) + position.X];

    public void SetModeInfoGridRow(Point position, Span<Av1ModeInfo> span)
        => this.SetModeInfoGridRow((position.Y * this.ModeInfoStride) + position.X, span);

    public void SetModeInfoGridRow(int offset, Span<Av1ModeInfo> span)
    {
        this.ModeInfoGrid[offset] = new Av1ModeInfo[span.Length];
        span.CopyTo(this.ModeInfoGrid[offset]);
    }

    /// <summary>
    /// SVT: get_mbmi
    /// </summary>
    internal Av1MacroBlockModeInfo GetMacroBlockModeInfo(Point blockOrigin)
    {
        int modeInfoStride = this.ModeInfoStride;
        int offset = (blockOrigin.Y * modeInfoStride) + blockOrigin.X;

        // Reset the mi_grid (needs to be done here in case it was changed for NSQ blocks during MD - svt_aom_init_xd())
        // mip offset may be different from grid offset when 4x4 blocks are disallowed
        int disallow4x4 = this.Disallow4x4AllFrames ? 1 : 0;
        int mipOffset = ((blockOrigin.Y >> disallow4x4) * (modeInfoStride >> disallow4x4)) + (blockOrigin.X >> disallow4x4);
        this.SetModeInfoGridRow(offset, ((Span<Av1ModeInfo>)this.Mip)[mipOffset..]);

        // use idx 0 as that's the first MacroBlockModeInfo in the block.
        Av1ModeInfo modeInfo = this.ModeInfoGrid[offset][0];
        return modeInfo.MacroBlockModeInfo;
    }

    /// <summary>
    /// SVT: svt_av1_update_segmentation_map
    /// </summary>
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
        for (int y = 0; y < ymis; ++y)
        {
            int offset = mi_offset + (y * cm.ModeInfoColumnCount);
            segment_ids.Slice(offset, xmis).Fill((byte)segmentId);
        }
    }
}
