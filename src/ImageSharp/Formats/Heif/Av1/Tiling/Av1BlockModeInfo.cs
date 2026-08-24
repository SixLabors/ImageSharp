// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores block-size, prediction-mode, transform, and palette decisions shared by AV1 block processing.
/// </summary>
internal class Av1BlockModeInfo
{
    /// <summary>
    /// Stores the palette size for luma and for the shared chroma mode.
    /// </summary>
    private int[] paletteSize;

    /// <summary>
    /// Stores the decoded palette colors for the Y, U, and V planes.
    /// </summary>
    private readonly ushort[][] paletteColors = [[], [], []];

    /// <summary>
    /// Stores the luma and shared chroma palette color-index maps.
    /// </summary>
    private readonly byte[][] paletteColorIndexMaps = [[], []];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockModeInfo"/> class.
    /// </summary>
    /// <param name="numPlanes">The number of color planes in the decoded frame.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="positionInSuperblock">The block origin relative to its superblock in 4x4 mode-information units.</param>
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

    /// <summary>
    /// Gets the decoded block size.
    /// </summary>
    public Av1BlockSize BlockSize { get; }

    /// <summary>
    /// Gets or sets the <see cref="Av1PredictionMode"/> for the luminance channel.
    /// </summary>
    public Av1PredictionMode YMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether residual coefficients are omitted for the block.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType PartitionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compound skip mode is selected.
    /// </summary>
    public bool SkipMode { get; set; }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Av1PredictionMode"/> for the chroma channels.
    /// </summary>
    public Av1PredictionMode UvMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether intra block copy is selected.
    /// </summary>
    public bool UseUltraBlockCopy { get; set; }

    /// <summary>
    /// Gets or sets the packed chroma-from-luma alpha magnitude indices.
    /// </summary>
    public int ChromaFromLumaAlphaIndex { get; set; }

    /// <summary>
    /// Gets or sets the joint chroma-from-luma alpha sign value.
    /// </summary>
    public int ChromaFromLumaAlphaSign { get; set; }

    /// <summary>
    /// Gets or sets the directional prediction angle adjustments for the chroma planes.
    /// </summary>
    public int[] AngleDelta { get; set; }

    /// <summary>
    /// Gets the position relative to the superblock in 4x4 mode-information units.
    /// </summary>
    public Point PositionInSuperblock { get; }

    /// <summary>
    /// Gets or sets the filter-intra syntax for the block.
    /// </summary>
    public Av1IntraFilterModeInfo FilterIntraModeInfo { get; internal set; }

    /// <summary>
    /// Gets the plane-relative index of the first <see cref="Av1TransformInfo"/> for this block.
    /// </summary>
    public int[] FirstTransformLocation { get; }

    /// <summary>
    /// Gets or sets the number of transform units for luma and for each chroma plane.
    /// </summary>
    public int[] TransformUnitsCount { get; internal set; }

    /// <summary>
    /// Gets the palette size for the specified color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The palette size for the plane.</returns>
    public int GetPaletteSize(Av1Plane plane) => this.paletteSize[Math.Min(1, (int)plane)];

    /// <summary>
    /// Gets the palette size for the specified plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The palette size for the plane class.</returns>
    public int GetPaletteSize(Av1PlaneType planeType) => this.paletteSize[(int)planeType];

    /// <summary>
    /// Sets the luma and shared chroma palette sizes.
    /// </summary>
    /// <param name="ySize">The luma palette size.</param>
    /// <param name="uvSize">The palette size shared by the chroma planes.</param>
    public void SetPaletteSizes(int ySize, int uvSize) => this.paletteSize = [ySize, uvSize];

    /// <summary>
    /// Gets the decoded palette colors for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The palette colors in prediction-index order.</returns>
    public ReadOnlySpan<ushort> GetPaletteColors(Av1Plane plane) => this.paletteColors[(int)plane];

    /// <summary>
    /// Stores the decoded palette colors for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <param name="colors">The palette colors in prediction-index order.</param>
    public void SetPaletteColors(Av1Plane plane, ReadOnlySpan<ushort> colors)
        => this.paletteColors[(int)plane] = colors.ToArray();

    /// <summary>
    /// Gets the palette color-index map for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The luma map for <see cref="Av1Plane.Y"/> or the shared chroma map for either chroma plane.</returns>
    public ReadOnlySpan<byte> GetPaletteColorIndexMap(Av1Plane plane)
        => this.paletteColorIndexMaps[Math.Min(1, (int)plane)];

    /// <summary>
    /// Stores the palette color-index map for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="colorIndexMap">The row-major color-index map including coded-block edge padding.</param>
    public void SetPaletteColorIndexMap(Av1PlaneType planeType, byte[] colorIndexMap)
        => this.paletteColorIndexMaps[(int)planeType] = colorIndexMap;
}
