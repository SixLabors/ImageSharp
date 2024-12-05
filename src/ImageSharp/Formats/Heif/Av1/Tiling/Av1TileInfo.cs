// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1TileInfo
{
    public Av1TileInfo(int row, int column, ObuFrameHeader frameHeader)
    {
        this.SetTileRow(frameHeader.TilesInfo, frameHeader.ModeInfoRowCount, row);
        this.SetTileColumn(frameHeader.TilesInfo, frameHeader.ModeInfoColumnCount, column);
    }

    public int ModeInfoRowStart { get; private set; }

    public int ModeInfoRowEnd { get; private set; }

    public int ModeInfoColumnStart { get; private set; }

    public int ModeInfoColumnEnd { get; private set; }

    public Point TileIndex { get; private set; }

    public void SetTileRow(ObuTileGroupHeader tileGroupHeader, int modeInfoRowCount, int row)
    {
        this.ModeInfoRowStart = tileGroupHeader.TileRowStartModeInfo[row];
        this.ModeInfoRowEnd = Math.Min(tileGroupHeader.TileRowStartModeInfo[row + 1], modeInfoRowCount);
        Point loc = this.TileIndex;
        loc.Y = row;
        this.TileIndex = loc;
    }

    public void SetTileColumn(ObuTileGroupHeader tileGroupHeader, int modeInfoColumnCount, int column)
    {
        this.ModeInfoColumnStart = tileGroupHeader.TileColumnStartModeInfo[column];
        this.ModeInfoColumnEnd = Math.Min(tileGroupHeader.TileColumnStartModeInfo[column + 1], modeInfoColumnCount);
        Point loc = this.TileIndex;
        loc.X = column;
        this.TileIndex = loc;
    }
}
