// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1MacroBlockD
{
    private Av1ModeInfo[] modeInfo = [];

    public required Span<Av1ModeInfo> ModeInfo
    {
        get => this.modeInfo;
        internal set
        {
            this.modeInfo = new Av1ModeInfo[value.Length];
            value.CopyTo(this.modeInfo);
        }
    }

    public required Av1TileInfo Tile { get; internal set; }

    public bool IsUpAvailable { get; internal set; }

    public bool IsLeftAvailable { get; internal set; }

    public Av1MacroBlockModeInfo? AboveMacroBlock { get; internal set; }

    public Av1MacroBlockModeInfo? LeftMacroBlock { get; internal set; }

    public int ModeInfoStride { get; internal set; }

    /// <summary>
    /// Gets or sets the number of macro blocks until the top edge.
    /// </summary>
    public int ToTopEdge { get; internal set; }

    /// <summary>
    /// Gets or sets the number of macro blocks until the bottom edge.
    /// </summary>
    public int ToBottomEdge { get; internal set; }

    /// <summary>
    /// Gets or sets the number of macro blocks until the left edge.
    /// </summary>
    public int ToLeftEdge { get; internal set; }

    /// <summary>
    /// Gets or sets the number of macro blocks until the right edge.
    /// </summary>
    public int ToRightEdge { get; internal set; }

    public Size N8Size { get; internal set; }

    public bool IsSecondRectangle { get; internal set; }
}
