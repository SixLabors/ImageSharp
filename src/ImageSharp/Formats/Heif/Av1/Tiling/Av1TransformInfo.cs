// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Describes the size, position, type, and residual state of one AV1 transform block.
/// </summary>
internal class Av1TransformInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformInfo"/> class with a 4x4 transform at the origin.
    /// </summary>
    public Av1TransformInfo()
        : this(Av1TransformSize.Size4x4, 0, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformInfo"/> class.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <param name="offsetX">The horizontal offset in mode-information units.</param>
    /// <param name="offsetY">The vertical offset in mode-information units.</param>
    public Av1TransformInfo(Av1TransformSize size, int offsetX, int offsetY)
    {
        this.Size = size;
        this.OffsetX = offsetX;
        this.OffsetY = offsetY;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformInfo"/> class.
    /// </summary>
    /// <param name="originalInfo">The <see cref="Av1TransformInfo"/> to copy the information from.</param>
    public Av1TransformInfo(Av1TransformInfo originalInfo)
    {
        this.Size = originalInfo.Size;
        this.OffsetX = originalInfo.OffsetX;
        this.OffsetY = originalInfo.OffsetY;
    }

    /// <summary>
    /// Gets or sets the transform size used for this transform block.
    /// </summary>
    public Av1TransformSize Size { get; internal set; }

    /// <summary>
    /// Gets or sets the transform type used for this transform block.
    /// </summary>
    public Av1TransformType Type { get; internal set; }

    /// <summary>
    /// Gets or sets the horizontal offset of this block in mode-information units.
    /// </summary>
    public int OffsetX { get; internal set; }

    /// <summary>
    /// Gets or sets the vertical offset of this block in mode-information units.
    /// </summary>
    public int OffsetY { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transform block contains a coded residual.
    /// <list type="table">
    /// <item>
    /// <term>false</term>
    /// <description>The block has no residual.</description>
    /// </item>
    /// <item>
    /// <term>true</term>
    /// <description>The block has a residual.</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool CodeBlockFlag { get; internal set; }
}
