// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1BlockModeInfo
{
    private int[] paletteSize;

    public Av1BlockModeInfo(int numPlanes, Av1BlockSize blockSize, Point position)
    {
        this.BlockSize = blockSize;
        this.PositionInSuperblock = position;
        this.AngleDelta = new int[numPlanes];
        this.paletteSize = new int[numPlanes - 1];
        this.FilterIntraModeInfo = new();
    }

    public Av1BlockSize BlockSize { get; }

    public Av1PredictionMode YMode { get; set; }

    public bool Skip { get; set; }

    public Av1PartitionType PartitionType { get; }

    public bool SkipMode { get; set; }

    public int SegmentId { get; set; }

    public Av1PredictionMode UvMode { get; set; }

    public bool UseUltraBlockCopy { get; set; }

    public int ChromaFromLumaAlphaIndex { get; set; }

    public int ChromaFromLumaAlphaSign { get; set; }

    public int[] AngleDelta { get; set; }

    public Point PositionInSuperblock { get; set; }

    public Av1IntraFilterModeInfo FilterIntraModeInfo { get; internal set; }

    public int GetPaletteSize(Av1PlaneType planeType) => this.paletteSize[(int)planeType];

    public void SetPaletteSizes(int ySize, int uvSize) => this.paletteSize = [ySize, uvSize];
}
