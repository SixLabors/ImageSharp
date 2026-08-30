// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds decoder-side macroblock edges, plane state, and neighboring mode information.
/// </summary>
internal class Av1MacroBlockD
{
    /// <summary>
    /// Stores the mode-information entries exposed through <see cref="ModeInfo"/>.
    /// </summary>
    private Av1ModeInfo[] modeInfo = [];

    /// <summary>
    /// Gets or sets the mode-information entries for the current block and its mapped neighbors.
    /// </summary>
    public required ReadOnlySpan<Av1ModeInfo> ModeInfo
    {
        get => this.modeInfo;
        set
        {
            // A span cannot be retained by the class, so preserve the selected map entries in owned storage.
            this.modeInfo = new Av1ModeInfo[value.Length];
            value.CopyTo(this.modeInfo);
        }
    }

    /// <summary>
    /// Gets or sets the tile containing the current block.
    /// </summary>
    public required Av1TileInfo Tile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an above block is available within the tile.
    /// </summary>
    public bool IsUpAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a left block is available within the tile.
    /// </summary>
    public bool IsLeftAvailable { get; set; }

    /// <summary>
    /// Gets or sets the above macroblock mode information, when available.
    /// </summary>
    public Av1MacroBlockModeInfo? AboveMacroBlock { get; set; }

    /// <summary>
    /// Gets or sets the left macroblock mode information, when available.
    /// </summary>
    public Av1MacroBlockModeInfo? LeftMacroBlock { get; set; }

    /// <summary>
    /// Gets or sets the row stride of the frame mode-information map.
    /// </summary>
    public int ModeInfoStride { get; set; }

    /// <summary>
    /// Gets or sets the signed distance from the block to the top frame edge in one-eighth-sample units.
    /// </summary>
    public int ToTopEdge { get; set; }

    /// <summary>
    /// Gets or sets the signed distance from the block to the bottom frame edge in one-eighth-sample units.
    /// </summary>
    public int ToBottomEdge { get; set; }

    /// <summary>
    /// Gets or sets the signed distance from the block to the left frame edge in one-eighth-sample units.
    /// </summary>
    public int ToLeftEdge { get; set; }

    /// <summary>
    /// Gets or sets the signed distance from the block to the right frame edge in one-eighth-sample units.
    /// </summary>
    public int ToRightEdge { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions in samples for rectangular-partition context selection.
    /// </summary>
    public Size N8Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this block is the second half of a rectangular partition.
    /// </summary>
    public bool IsSecondRectangle { get; set; }
}
