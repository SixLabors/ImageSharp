// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the tile layout derived from an AV1 frame header.
/// </summary>
internal sealed class ObuTileGroupHeader
{
    private InlineTileColumnBoundaryArray tileColumnStartModeInfo;
    private InlineTileRowBoundaryArray tileRowStartModeInfo;

    /// <summary>
    /// Gets or sets the maximum tile width, in superblocks.
    /// </summary>
    public int MaxTileWidthSuperblock { get; set; }

    /// <summary>
    /// Gets or sets the maximum tile height, in superblocks.
    /// </summary>
    public int MaxTileHeightSuperblock { get; set; }

    /// <summary>
    /// Gets or sets the minimum base-2 logarithm of the tile-column count.
    /// </summary>
    public int MinLog2TileColumnCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum base-2 logarithm of the tile-column count.
    /// </summary>
    public int MaxLog2TileColumnCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum base-2 logarithm of the tile-row count.
    /// </summary>
    public int MaxLog2TileRowCount { get; set; }

    /// <summary>
    /// Gets or sets the minimum base-2 logarithm of the total tile count.
    /// </summary>
    public int MinLog2TileCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tile columns and rows use uniform spacing.
    /// </summary>
    public bool HasUniformTileSpacing { get; set; }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the tile-column count.
    /// </summary>
    public int TileColumnCountLog2 { get; set; }

    /// <summary>
    /// Gets or sets the number of tile columns.
    /// </summary>
    public int TileColumnCount { get; set; }

    /// <summary>
    /// Gets the fixed-capacity starting superblock column storage for each tile column.
    /// </summary>
    public Span<int> TileColumnStartModeInfo => this.tileColumnStartModeInfo;

    /// <summary>
    /// Gets or sets the minimum base-2 logarithm of the tile-row count.
    /// </summary>
    public int MinLog2TileRowCount { get; set; }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the tile-row count.
    /// </summary>
    public int TileRowCountLog2 { get; set; }

    /// <summary>
    /// Gets the fixed-capacity starting superblock row storage for each tile row.
    /// </summary>
    public Span<int> TileRowStartModeInfo => this.tileRowStartModeInfo;

    /// <summary>
    /// Gets or sets the number of tile rows.
    /// </summary>
    public int TileRowCount { get; set; }

    /// <summary>
    /// Gets or sets the tile whose entropy context is retained after frame decoding.
    /// </summary>
    public uint ContextUpdateTileId { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes used to signal each tile size.
    /// </summary>
    public int TileSizeBytes { get; set; }

    [InlineArray(Av1Constants.MaxTileColumnCount + 1)]
    private struct InlineTileColumnBoundaryArray
    {
        private int element;
    }

    [InlineArray(Av1Constants.MaxTileRowCount + 1)]
    private struct InlineTileRowBoundaryArray
    {
        private int element;
    }
}
