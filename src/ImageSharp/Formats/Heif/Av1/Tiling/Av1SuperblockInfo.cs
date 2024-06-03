// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SuperblockInfo
{
    public Av1SuperblockInfo(Av1BlockModeInfo superblockModeInfo, Av1TransformInfo superblockTransformInfo)
    {
        this.SuperblockModeInfo = superblockModeInfo;
        this.SuperblockTransformInfo = superblockTransformInfo;
    }

    public int SuperblockDeltaQ { get; internal set; }

    public Av1BlockModeInfo SuperblockModeInfo { get; set; }

    public int[] CoefficientsY { get; set; } = [];

    public int[] CoefficientsU { get; set; } = [];

    public int[] CoefficientsV { get; set; } = [];

    public Av1TransformInfo SuperblockTransformInfo { get; set; }

    public int CdefStrength { get; internal set; }

    public int SuperblockDeltaLoopFilter { get; set; }

    public Av1BlockModeInfo GetModeInfo(int rowIndex, int columnIndex) => throw new NotImplementedException();
}
