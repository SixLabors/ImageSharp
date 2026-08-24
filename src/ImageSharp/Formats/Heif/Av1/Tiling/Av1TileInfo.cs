// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Describes one AV1 tile's superblock and mode-information boundaries.
/// </summary>
internal class Av1TileInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileInfo"/> class for the specified tile coordinates.
    /// </summary>
    /// <param name="row">The tile row index.</param>
    /// <param name="column">The tile column index.</param>
    /// <param name="frameHeader">The frame header that defines the tile layout.</param>
    public Av1TileInfo(int row, int column, ObuFrameHeader frameHeader)
    {
        this.SetTileRow(frameHeader.TilesInfo, frameHeader.ModeInfoRowCount, row);
        this.SetTileColumn(frameHeader.TilesInfo, frameHeader.ModeInfoColumnCount, column);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TileInfo"/> class by copying another tile description.
    /// </summary>
    /// <param name="tileInfo">The tile description to copy.</param>
    public Av1TileInfo(Av1TileInfo tileInfo)
    {
        this.ModeInfoColumnStart = tileInfo.ModeInfoColumnStart;
        this.ModeInfoColumnEnd = tileInfo.ModeInfoColumnEnd;
        this.ModeInfoRowStart = tileInfo.ModeInfoRowStart;
        this.ModeInfoRowEnd = tileInfo.ModeInfoRowEnd;
        this.TileIndex = tileInfo.TileIndex;
    }

    /// <summary>
    /// Gets the first mode-information row in the tile.
    /// </summary>
    public int ModeInfoRowStart { get; private set; }

    /// <summary>
    /// Gets the exclusive end mode-information row in the tile.
    /// </summary>
    public int ModeInfoRowEnd { get; private set; }

    /// <summary>
    /// Gets the first mode-information column in the tile.
    /// </summary>
    public int ModeInfoColumnStart { get; private set; }

    /// <summary>
    /// Gets the exclusive end mode-information column in the tile.
    /// </summary>
    public int ModeInfoColumnEnd { get; private set; }

    /// <summary>
    /// Gets the tile column and row indices.
    /// </summary>
    public Point TileIndex { get; private set; }

    /// <summary>
    /// Selects the tile row and updates its mode-information boundaries.
    /// </summary>
    /// <param name="tileGroupHeader">The tile layout.</param>
    /// <param name="modeInfoRowCount">The coded frame height in mode-information rows.</param>
    /// <param name="row">The tile row index.</param>
    public void SetTileRow(ObuTileGroupHeader tileGroupHeader, int modeInfoRowCount, int row)
    {
        this.ModeInfoRowStart = tileGroupHeader.TileRowStartModeInfo[row];
        this.ModeInfoRowEnd = Math.Min(tileGroupHeader.TileRowStartModeInfo[row + 1], modeInfoRowCount);
        Point loc = this.TileIndex;
        loc.Y = row;
        this.TileIndex = loc;
    }

    /// <summary>
    /// Selects the tile column and updates its mode-information boundaries.
    /// </summary>
    /// <param name="tileGroupHeader">The tile layout.</param>
    /// <param name="modeInfoColumnCount">The coded frame width in mode-information columns.</param>
    /// <param name="column">The tile column index.</param>
    public void SetTileColumn(ObuTileGroupHeader tileGroupHeader, int modeInfoColumnCount, int column)
    {
        this.ModeInfoColumnStart = tileGroupHeader.TileColumnStartModeInfo[column];
        this.ModeInfoColumnEnd = Math.Min(tileGroupHeader.TileColumnStartModeInfo[column + 1], modeInfoColumnCount);
        Point loc = this.TileIndex;
        loc.X = column;
        this.TileIndex = loc;
    }
}
