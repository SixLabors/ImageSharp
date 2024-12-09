// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

    public required Av1BlockModeInfo[] ModeInfoGrid { get; internal set; }

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
