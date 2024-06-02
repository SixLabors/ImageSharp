// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1PartitionInfo
{
    public Av1PartitionInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo, bool isChroma, Av1PartitionType partitionType)
    {
        this.ModeInfo = modeInfo;
        this.SuperblockInfo = superblockInfo;
        this.IsChroma = isChroma;
        this.PartitionType = partitionType;
        this.CdefStrength = [];
        this.ReferenceFrame = [-1, -1];
    }

    public Av1BlockModeInfo ModeInfo { get; }

    public Av1SuperblockInfo SuperblockInfo { get; }

    public bool IsChroma { get; }

    public Av1PartitionType PartitionType { get; }

    public bool AvailableUp { get; set; }

    public bool AvailableLeft { get; set; }

    public bool AvailableUpForChroma { get; set; }

    public bool AvailableLeftForChroma { get; set; }

    public int ColumnIndex { get; set; }

    public int RowIndex { get; set; }

    public Av1BlockModeInfo? AboveModeInfo { get; set; }

    public Av1BlockModeInfo? LeftModeInfo { get; set; }

    public int[][] CdefStrength { get; set; }

    public int[] ReferenceFrame { get; set; }
}
