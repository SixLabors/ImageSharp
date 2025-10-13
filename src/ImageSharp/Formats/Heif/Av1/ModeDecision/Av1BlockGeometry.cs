// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;

internal class Av1BlockGeometry
{
    private Av1BlockSize blockSize;
    private Av1BlockSize blockSizeUv;

    public Av1BlockGeometry()
    {
        this.RedunancyList = [];
        this.TransformOrigin = new Point[Av1Constants.MaxVarTransform + 1][];
        for (int i = 0; i < this.TransformOrigin.Length; i++)
        {
            this.TransformOrigin[i] = new Point[Av1Constants.MaxTransformBlockCount];
        }
    }

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
    /// Gets or sets the Origin point from lop left of the superblock.
    /// </summary>
    public Point Origin { get; internal set; }

    public bool HasUv { get; internal set; }

    /// <summary>
    /// Gets the blocks width.
    /// </summary>
    public int BlockWidth { get; private set; }

    /// <summary>
    /// Gets the blocks height.
    /// </summary>
    public int BlockHeight { get; private set; }

    public int[] TransformBlockCount { get; } = new int[Av1Constants.MaxVarTransform + 1];

    public Av1TransformSize[] TransformSize { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

    public Av1TransformSize[] TransformSizeUv { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

    public Point[][] TransformOrigin { get; private set; }

    /// <summary>
    /// Gets or sets the blocks index in the Mode Decision scan.
    /// </summary>
    public int ModeDecisionIndex { get; set; }

    /// <summary>
    /// Gets or sets the offset to the next nsq block (skip remaining d2 blocks).
    /// </summary>
    public int NextDepthOffset { get; set; }

    /// <summary>
    /// Gets or sets the offset to the next d1 sq block
    /// </summary>
    public int Depth1Offset { get; set; }

    /// <summary>
    /// Gets a value indicating whether this block is redundant to another.
    /// </summary>
    public bool IsRedundant => this.RedunancyList.Count > 0;

    /// <summary>
    /// Gets or sets the list where the block is redundant.
    /// </summary>
    public List<int> RedunancyList { get; internal set; }

    /// <summary>
    /// Gets or sets the non square index within a partition  0..totns-1
    /// </summary>
    public int NonSquareIndex { get; internal set; }

    public int TotalNonSuareCount { get; internal set; }

    public int BlockWidthUv { get; private set; }

    public int BlockHeightUv { get; private set; }

    public int Depth { get; internal set; }

    public int SequenceSize { get; internal set; }

    public bool IsLastQuadrant { get; internal set; }
}
