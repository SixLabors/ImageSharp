// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Information of a single Transform Block.
/// </summary>
internal class Av1TransformInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformInfo"/> class.
    /// </summary>
    public Av1TransformInfo()
        : this(Av1TransformSize.Size4x4, 0, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformInfo"/> class.
    /// </summary>
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
    /// Gets or sets the transform size to be used for this Transform Block.
    /// </summary>
    public Av1TransformSize Size { get; internal set; }

    /// <summary>
    /// Gets or sets the transform type to be used for this Transform Block.
    /// </summary>
    public Av1TransformType Type { get; internal set; }

    /// <summary>
    /// Gets or sets the X offset of this block in ModeInfo units.
    /// </summary>
    public int OffsetX { get; internal set; }

    /// <summary>
    /// Gets or sets the Y offset of this block in ModeInfo units.
    /// </summary>
    public int OffsetY { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Code block flag is set.
    /// <list type="table">
    /// <item>
    /// <term>false</term>
    /// <description>No residual for the block</description>
    /// </item>
    /// <item>
    /// <term>true</term>
    /// <description>Residual exists for the block</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool CodeBlockFlag { get; internal set; }
}
