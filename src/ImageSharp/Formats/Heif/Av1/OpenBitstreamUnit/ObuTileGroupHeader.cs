// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the tile layout derived from an AV1 frame header.
/// </summary>
internal class ObuTileGroupHeader
{
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
    /// Gets or sets the starting superblock column for each tile column.
    /// </summary>
    public int[] TileColumnStartModeInfo { get; set; } = new int[Av1Constants.MaxTileRowCount + 1];

    /// <summary>
    /// Gets or sets the minimum base-2 logarithm of the tile-row count.
    /// </summary>
    public int MinLog2TileRowCount { get; set; }

    /// <summary>
    /// Gets or sets the base-2 logarithm of the tile-row count.
    /// </summary>
    public int TileRowCountLog2 { get; set; }

    /// <summary>
    /// Gets or sets the starting superblock row for each tile row.
    /// </summary>
    public int[] TileRowStartModeInfo { get; set; } = new int[Av1Constants.MaxTileColumnCount + 1];

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
}
