// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Maps HEVC coding-tree blocks between picture raster order and tile-scan order.
/// </summary>
internal readonly struct HevcTileLayout
{
    /// <summary>
    /// The tile widths in coding-tree blocks.
    /// </summary>
    private readonly IReadOnlyList<int> columnWidths;

    /// <summary>
    /// The tile heights in coding-tree blocks.
    /// </summary>
    private readonly IReadOnlyList<int> rowHeights;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcTileLayout"/> struct.
    /// </summary>
    /// <param name="pictureParameterSet">The picture tile geometry.</param>
    public HevcTileLayout(HevcPictureParameterSet pictureParameterSet)
        : this(pictureParameterSet.TileColumnWidths, pictureParameterSet.TileRowHeights)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcTileLayout"/> struct from validated tile dimensions.
    /// </summary>
    /// <param name="columnWidths">The tile-column widths in coding-tree blocks.</param>
    /// <param name="rowHeights">The tile-row heights in coding-tree blocks.</param>
    public HevcTileLayout(IReadOnlyList<int> columnWidths, IReadOnlyList<int> rowHeights)
    {
        this.columnWidths = columnWidths;
        this.rowHeights = rowHeights;
        this.ColumnCount = this.columnWidths.Count;
        this.RowCount = this.rowHeights.Count;
        this.Width = Sum(this.columnWidths);
        this.Height = Sum(this.rowHeights);
    }

    /// <summary>
    /// Gets the number of tile columns.
    /// </summary>
    public int ColumnCount { get; }

    /// <summary>
    /// Gets the number of tile rows.
    /// </summary>
    public int RowCount { get; }

    /// <summary>
    /// Gets the picture width in coding-tree blocks.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the picture height in coding-tree blocks.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of tiles in the picture.
    /// </summary>
    public int TileCount => this.ColumnCount * this.RowCount;

    /// <summary>
    /// Converts a picture raster-scan address to tile-scan order.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan coding-tree-block address.</param>
    /// <returns>The corresponding tile-scan address.</returns>
    public int GetTileScanAddress(int rasterAddress)
    {
        int x = rasterAddress % this.Width;
        int y = rasterAddress / this.Width;
        this.FindTile(x, y, out int tileColumn, out int tileRow, out int tileStartX, out int tileStartY);
        int address = 0;
        for (int row = 0; row < tileRow; row++)
        {
            address += this.rowHeights[row] * this.Width;
        }

        for (int column = 0; column < tileColumn; column++)
        {
            address += this.columnWidths[column] * this.rowHeights[tileRow];
        }

        return address + ((y - tileStartY) * this.columnWidths[tileColumn]) + x - tileStartX;
    }

    /// <summary>
    /// Converts a tile-scan coding-tree-block address to picture raster order.
    /// </summary>
    /// <param name="tileScanAddress">The tile-scan address.</param>
    /// <returns>The corresponding raster-scan address.</returns>
    public int GetRasterAddress(int tileScanAddress)
    {
        int remaining = tileScanAddress;
        int tileStartY = 0;
        for (int tileRow = 0; tileRow < this.RowCount; tileRow++)
        {
            int tileStartX = 0;
            for (int tileColumn = 0; tileColumn < this.ColumnCount; tileColumn++)
            {
                int tileWidth = this.columnWidths[tileColumn];
                int tileHeight = this.rowHeights[tileRow];
                int tileArea = tileWidth * tileHeight;
                if (remaining < tileArea)
                {
                    int x = tileStartX + (remaining % tileWidth);
                    int y = tileStartY + (remaining / tileWidth);
                    return (y * this.Width) + x;
                }

                remaining -= tileArea;
                tileStartX += tileWidth;
            }

            tileStartY += this.rowHeights[tileRow];
        }

        return this.Width * this.Height;
    }

    /// <summary>
    /// Gets the tile and tile-local position of one raster-scan coding-tree block.
    /// </summary>
    /// <param name="rasterAddress">The raster-scan address.</param>
    /// <param name="tileIndex">The zero-based tile index.</param>
    /// <param name="columnInTile">The horizontal coding-tree-block offset within the tile.</param>
    /// <param name="rowInTile">The vertical coding-tree-block offset within the tile.</param>
    /// <param name="tileWidth">The tile width in coding-tree blocks.</param>
    /// <param name="tileHeight">The tile height in coding-tree blocks.</param>
    public void GetTilePosition(
        int rasterAddress,
        out int tileIndex,
        out int columnInTile,
        out int rowInTile,
        out int tileWidth,
        out int tileHeight)
    {
        int x = rasterAddress % this.Width;
        int y = rasterAddress / this.Width;
        this.FindTile(x, y, out int tileColumn, out int tileRow, out int tileStartX, out int tileStartY);
        tileIndex = (tileRow * this.ColumnCount) + tileColumn;
        columnInTile = x - tileStartX;
        rowInTile = y - tileStartY;
        tileWidth = this.columnWidths[tileColumn];
        tileHeight = this.rowHeights[tileRow];
    }

    /// <summary>
    /// Locates the tile containing one coding-tree-block coordinate.
    /// </summary>
    /// <param name="x">The raster coding-tree-block X coordinate.</param>
    /// <param name="y">The raster coding-tree-block Y coordinate.</param>
    /// <param name="tileColumn">The containing tile column.</param>
    /// <param name="tileRow">The containing tile row.</param>
    /// <param name="tileStartX">The containing tile's left coding-tree-block coordinate.</param>
    /// <param name="tileStartY">The containing tile's top coding-tree-block coordinate.</param>
    private void FindTile(int x, int y, out int tileColumn, out int tileRow, out int tileStartX, out int tileStartY)
    {
        tileStartX = 0;
        tileColumn = 0;
        while (x >= tileStartX + this.columnWidths[tileColumn])
        {
            tileStartX += this.columnWidths[tileColumn++];
        }

        tileStartY = 0;
        tileRow = 0;
        while (y >= tileStartY + this.rowHeights[tileRow])
        {
            tileStartY += this.rowHeights[tileRow++];
        }
    }

    /// <summary>
    /// Sums one complete tile dimension.
    /// </summary>
    /// <param name="values">The tile widths or heights.</param>
    /// <returns>The complete picture dimension in coding-tree blocks.</returns>
    private static int Sum(IReadOnlyList<int> values)
    {
        int sum = 0;
        foreach (int value in values)
        {
            sum += value;
        }

        return sum;
    }
}
