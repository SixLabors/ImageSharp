// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;

/// <summary>
/// Describes the spatial, chroma, and transform layout of one AV1 block considered by mode-decision scanning.
/// </summary>
internal class Av1BlockGeometry
{
    /// <summary>
    /// The luma block size from which the cached luma dimensions are derived.
    /// </summary>
    private Av1BlockSize blockSize;

    /// <summary>
    /// The chroma block size from which the cached chroma dimensions are derived.
    /// </summary>
    private Av1BlockSize blockSizeUv;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockGeometry"/> class with storage for every supported transform depth.
    /// </summary>
    public Av1BlockGeometry()
    {
        this.RedunancyList = [];
        this.TransformOrigin = new Point[Av1Constants.MaxVarTransform + 1][];
        for (int i = 0; i < this.TransformOrigin.Length; i++)
        {
            this.TransformOrigin[i] = new Point[Av1Constants.MaxTransformBlockCount];
        }
    }

    /// <summary>
    /// Gets or sets the luma block size and updates <see cref="BlockWidth"/> and <see cref="BlockHeight"/> to match.
    /// </summary>
    public Av1BlockSize BlockSize
    {
        get => this.blockSize;
        internal set
        {
            this.blockSize = value;
            this.BlockWidth = value.GetWidth();
            this.BlockHeight = value.GetHeight();
        }
    }

    /// <summary>
    /// Gets or sets the chroma block size and updates <see cref="BlockWidthUv"/> and <see cref="BlockHeightUv"/> to match.
    /// </summary>
    public Av1BlockSize BlockSizeUv
    {
        get => this.blockSizeUv;
        internal set
        {
            this.blockSizeUv = value;
            this.BlockWidthUv = value.GetWidth();
            this.BlockHeightUv = value.GetHeight();
        }
    }

    /// <summary>
    /// Gets or sets the block origin in pixels relative to the top-left corner of its superblock.
    /// </summary>
    public Point Origin { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether this luma block owns chroma samples in the mode-decision layout.
    /// </summary>
    public bool HasUv { get; internal set; }

    /// <summary>
    /// Gets the luma block width in pixels.
    /// </summary>
    public int BlockWidth { get; private set; }

    /// <summary>
    /// Gets the luma block height in pixels.
    /// </summary>
    public int BlockHeight { get; private set; }

    /// <summary>
    /// Gets the number of luma transform blocks at each transform depth.
    /// </summary>
    public int[] TransformBlockCount { get; } = new int[Av1Constants.MaxVarTransform + 1];

    /// <summary>
    /// Gets the luma transform size selected at each transform depth.
    /// </summary>
    public Av1TransformSize[] TransformSize { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

    /// <summary>
    /// Gets the chroma transform size selected at each transform depth.
    /// </summary>
    public Av1TransformSize[] TransformSizeUv { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

    /// <summary>
    /// Gets the pixel origins of the transform blocks at each transform depth.
    /// </summary>
    public Point[][] TransformOrigin { get; private set; }

    /// <summary>
    /// Gets or sets the block index in mode-decision scan order.
    /// </summary>
    public int ModeDecisionIndex { get; set; }

    /// <summary>
    /// Gets or sets the scan offset from this square block to the next block at the same depth.
    /// </summary>
    public int NextDepthOffset { get; set; }

    /// <summary>
    /// Gets or sets the scan offset from this square block to its first child at the next depth.
    /// </summary>
    public int Depth1Offset { get; set; }

    /// <summary>
    /// Gets a value indicating whether this block is redundant to another.
    /// </summary>
    public bool IsRedundant => this.RedunancyList.Count > 0;

    /// <summary>
    /// Gets or sets the mode-decision indices of blocks with the same size and origin as this block.
    /// </summary>
    public List<int> RedunancyList { get; internal set; }

    /// <summary>
    /// Gets or sets the zero-based component index of this block within a non-square partition.
    /// </summary>
    public int NonSquareIndex { get; internal set; }

    /// <summary>
    /// Gets or sets the number of component blocks produced by this partition shape.
    /// </summary>
    public int TotalNonSuareCount { get; internal set; }

    /// <summary>
    /// Gets the chroma block width in pixels.
    /// </summary>
    public int BlockWidthUv { get; private set; }

    /// <summary>
    /// Gets the chroma block height in pixels.
    /// </summary>
    public int BlockHeightUv { get; private set; }

    /// <summary>
    /// Gets or sets the quadtree depth of this block within its superblock.
    /// </summary>
    public int Depth { get; internal set; }

    /// <summary>
    /// Gets or sets the width and height, in pixels, of the square sequence region that produced this block.
    /// </summary>
    public int SequenceSize { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether this block belongs to the last quadrant of its parent.
    /// </summary>
    public bool IsLastQuadrant { get; internal set; }
}
