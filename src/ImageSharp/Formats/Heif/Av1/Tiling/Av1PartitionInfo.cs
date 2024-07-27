// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PartitionInfo
{
    private int modeBlockToLeftEdge;
    private int modeBlockToRightEdge;
    private int modeBlockToTopEdge;
    private int modeBlockToBottomEdge;

    public Av1PartitionInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo, bool isChroma, Av1PartitionType partitionType)
    {
        this.ModeInfo = modeInfo;
        this.SuperblockInfo = superblockInfo;
        this.IsChroma = isChroma;
        this.Type = partitionType;
        this.CdefStrength = [];
        this.ReferenceFrame = [-1, -1];
    }

    public Av1BlockModeInfo ModeInfo { get; }

    /// <summary>
    /// Gets the <see cref="Av1SuperblockInfo"/> this partition resides inside.
    /// </summary>
    public Av1SuperblockInfo SuperblockInfo { get; }

    public bool IsChroma { get; }

    public Av1PartitionType Type { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block above can be used on the luma plane.
    /// </summary>
    public bool AvailableAbove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block left can be used on the luma plane.
    /// </summary>
    public bool AvailableLeft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block above can be used on the chroma plane.
    /// </summary>
    public bool AvailableAboveForChroma { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the information from the block left can be used on the chroma plane.
    /// </summary>
    public bool AvailableLeftForChroma { get; set; }

    /// <summary>
    /// Gets or sets the horizontal location of the block in units of 4x4 luma samples.
    /// </summary>
    public int ColumnIndex { get; set; }

    /// <summary>
    /// Gets or sets the vertical location of the block in units of 4x4 luma samples.
    /// </summary>
    public int RowIndex { get; set; }

    public Av1BlockModeInfo? AboveModeInfo { get; set; }

    public Av1BlockModeInfo? LeftModeInfo { get; set; }

    public int[][] CdefStrength { get; set; }

    public int[] ReferenceFrame { get; set; }

    public int ModeBlockToRightEdge => this.modeBlockToRightEdge;

    public int ModeBlockToBottomEdge => this.modeBlockToBottomEdge;

    public void ComputeBoundaryOffsets(ObuFrameHeader frameInfo, Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = this.ModeInfo.BlockSize;
        int bw4 = blockSize.Get4x4WideCount();
        int bh4 = blockSize.Get4x4HighCount();
        this.AvailableAbove = this.RowIndex > tileInfo.ModeInfoRowStart;
        this.AvailableLeft = this.ColumnIndex > tileInfo.ModeInfoColumnStart;
        this.AvailableAboveForChroma = this.AvailableAbove;
        this.AvailableLeftForChroma = this.AvailableLeft;
        int shift = Av1Constants.ModeInfoSizeLog2 + 3;
        this.modeBlockToLeftEdge = -this.ColumnIndex << shift;
        this.modeBlockToRightEdge = (frameInfo.ModeInfoColumnCount - bw4 - this.ColumnIndex) << shift;
        this.modeBlockToTopEdge = -this.RowIndex << shift;
        this.modeBlockToBottomEdge = (frameInfo.ModeInfoRowCount - bh4 - this.RowIndex) << shift;
    }

    public int GetMaxBlockWide(Av1BlockSize blockSize, bool subX)
    {
        int maxBlockWide = blockSize.GetWidth();
        if (this.modeBlockToRightEdge < 0)
        {
            int shift = subX ? 4 : 3;
            maxBlockWide += this.modeBlockToRightEdge >> shift;
        }

        return maxBlockWide >> 2;
    }

    public int GetMaxBlockHigh(Av1BlockSize blockSize, bool subY)
    {
        int maxBlockHigh = blockSize.GetHeight();
        if (this.modeBlockToBottomEdge < 0)
        {
            int shift = subY ? 4 : 3;
            maxBlockHigh += this.modeBlockToBottomEdge >> shift;
        }

        return maxBlockHigh >> 2;
    }
}
