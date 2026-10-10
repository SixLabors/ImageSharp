// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder-side macroblock edges and neighboring mode information.
/// </summary>
internal class Av1MacroBlockD
{
    /// <summary>
    /// Stores the current block's linear position in the frame's mode-information grid.
    /// </summary>
    private int modeInfoIndex;

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
    /// Selects the current entry in the frame-owned mode-information grid.
    /// </summary>
    /// <param name="index">The current block's linear grid index.</param>
    public void SetModeInfoIndex(int index) => this.modeInfoIndex = index;

    /// <summary>
    /// Gets a mode-information entry relative to the current block from the frame grid and allocation, which the
    /// caller read once.
    /// </summary>
    /// <param name="modeInfoGrid">The frame-owned mode-information allocation-index grid.</param>
    /// <param name="modeInfoAllocation">The frame-owned contiguous mode-information values.</param>
    /// <param name="offset">The signed linear offset from the current block.</param>
    /// <returns>A reference to the mapped neighboring or current entry.</returns>
    public ref Av1MacroBlockModeInfo GetRelativeModeInfo(ReadOnlySpan<int> modeInfoGrid, Span<Av1MacroBlockModeInfo> modeInfoAllocation, int offset)
        => ref modeInfoAllocation[modeInfoGrid[this.modeInfoIndex + offset]];
}
