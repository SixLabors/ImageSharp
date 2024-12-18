// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1BlockModeInfo
{
    private int[] paletteSize;

    public Av1BlockModeInfo(int numPlanes, Av1BlockSize blockSize, Point positionInSuperblock)
    {
        this.BlockSize = blockSize;
        this.PositionInSuperblock = positionInSuperblock;
        this.AngleDelta = new int[numPlanes - 1];
        this.paletteSize = new int[numPlanes - 1];
        this.FilterIntraModeInfo = new();
        this.FirstTransformLocation = new int[numPlanes - 1];
        this.TransformUnitsCount = new int[numPlanes - 1];
    }

    public Av1BlockSize BlockSize { get; }

    /// <summary>
    /// Gets or sets the <see cref="Av1PredictionMode"/> for the luminance channel.
    /// </summary>
    public Av1PredictionMode YMode { get; set; }

    public bool Skip { get; set; }

    public Av1PartitionType PartitionType { get; set; }

    public bool SkipMode { get; set; }

    public int SegmentId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Av1PredictionMode"/> for the chroma channels.
    /// </summary>
    public Av1PredictionMode UvMode { get; set; }

    public bool UseUltraBlockCopy { get; set; }

    public int ChromaFromLumaAlphaIndex { get; set; }

    public int ChromaFromLumaAlphaSign { get; set; }

    public int[] AngleDelta { get; set; }

    /// <summary>
    /// Gets the position relative to the Superblock, counted in mode info (4x4 pixels).
    /// </summary>
    public Point PositionInSuperblock { get; }

    public Av1IntraFilterModeInfo FilterIntraModeInfo { get; internal set; }

    /// <summary>
    /// Gets the index of the first <see cref="Av1TransformInfo"/> of this Mode Info in the <see cref="Av1FrameInfo"/>.
    /// </summary>
    public int[] FirstTransformLocation { get; }

    public int[] TransformUnitsCount { get; internal set; }

    public int GetPaletteSize(Av1Plane plane) => this.paletteSize[Math.Min(1, (int)plane)];

    public int GetPaletteSize(Av1PlaneType planeType) => this.paletteSize[(int)planeType];

    public void SetPaletteSizes(int ySize, int uvSize) => this.paletteSize = [ySize, uvSize];
}
