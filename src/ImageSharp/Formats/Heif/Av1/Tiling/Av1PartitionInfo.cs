// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1PartitionInfo
{
    public Av1PartitionInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo, bool isChroma, Av1PartitionType partitionType)
    {
        this.ModeInfo = modeInfo;
        this.SuperblockInfo = superblockInfo;
        this.IsChroma = isChroma;
        this.Type = partitionType;
        this.CdefStrength = [];
        this.ReferenceFrame = [-1, -1];
        this.WidthInPixels = new int[3];
        this.HeightInPixels = new int[3];
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

    public Av1BlockModeInfo? AboveModeInfoForChroma { get; set; }

    public Av1BlockModeInfo? LeftModeInfoForChroma { get; set; }

    public int[] CdefStrength { get; set; }

    public int[] ReferenceFrame { get; set; }

    public int ModeBlockToLeftEdge { get; private set; }

    public int ModeBlockToRightEdge { get; private set; }

    public int ModeBlockToTopEdge { get; private set; }

    public int ModeBlockToBottomEdge { get; private set; }

    public int[] WidthInPixels { get; private set; }

    public int[] HeightInPixels { get; private set; }

    public Av1ChromaFromLumaContext? ChromaFromLumaContext { get; internal set; }

    public void ComputeBoundaryOffsets(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1TileInfo tileInfo)
    {
        Av1BlockSize blockSize = this.ModeInfo.BlockSize;
        int bw4 = blockSize.Get4x4WideCount();
        int bh4 = blockSize.Get4x4HighCount();
        int subX = sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
        this.AvailableAbove = this.RowIndex > tileInfo.ModeInfoRowStart;
        this.AvailableLeft = this.ColumnIndex > tileInfo.ModeInfoColumnStart;
        this.AvailableAboveForChroma = this.AvailableAbove;
        this.AvailableLeftForChroma = this.AvailableLeft;

        int shift = Av1Constants.ModeInfoSizeLog2 + 3;
        this.ModeBlockToLeftEdge = -this.ColumnIndex << shift;
        this.ModeBlockToRightEdge = (frameHeader.ModeInfoColumnCount - bw4 - this.ColumnIndex) << shift;
        this.ModeBlockToTopEdge = -this.RowIndex << shift;
        this.ModeBlockToBottomEdge = (frameHeader.ModeInfoRowCount - bh4 - this.RowIndex) << shift;

        // Block Size width & height in pixels.
        // For Luma bock
        const int modeInfoSize = 1 << Av1Constants.ModeInfoSizeLog2;
        this.WidthInPixels[0] = bw4 * modeInfoSize;
        this.HeightInPixels[0] = bh4 * modeInfoSize;

        // For U plane chroma bock
        this.WidthInPixels[1] = Math.Max(1, bw4 >> subX) * modeInfoSize;
        this.HeightInPixels[1] = Math.Max(1, bh4 >> subY) * modeInfoSize;

        // For V plane chroma bock
        this.WidthInPixels[2] = Math.Max(1, bw4 >> subX) * modeInfoSize;
        this.HeightInPixels[2] = Math.Max(1, bh4 >> subY) * modeInfoSize;

        this.ChromaFromLumaContext = new Av1ChromaFromLumaContext(configuration, sequenceHeader.ColorConfig);
    }

    public int GetMaxBlockWide(Av1BlockSize blockSize, bool subX)
    {
        int maxBlockWide = blockSize.GetWidth();
        if (this.ModeBlockToRightEdge < 0)
        {
            int shift = subX ? 4 : 3;
            maxBlockWide += this.ModeBlockToRightEdge >> shift;
        }

        return maxBlockWide >> 2;
    }

    public int GetMaxBlockHigh(Av1BlockSize blockSize, bool subY)
    {
        int maxBlockHigh = blockSize.GetHeight();
        if (this.ModeBlockToBottomEdge < 0)
        {
            int shift = subY ? 4 : 3;
            maxBlockHigh += this.ModeBlockToBottomEdge >> shift;
        }

        return maxBlockHigh >> 2;
    }
}
