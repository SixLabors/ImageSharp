// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains the bounded row count, column count and output dimensions of one HEIF image-grid descriptor. This type also validates the grid layout against its
/// tiles.
/// </summary>
/// <remarks>
/// The color grid and the alpha grid use the same validation. The validation methods return the error message and do not throw. Thus the decoder can throw the
/// message for a color grid. The item graph validator can report an alpha grid error through the image data mode.
/// </remarks>
internal readonly struct HeifGridDescriptor
{
    /// <summary>
    /// The image-grid descriptor version that HEIF defines.
    /// </summary>
    private const byte GridDescriptorVersion = 0;

    /// <summary>
    /// The descriptor flag that selects 32-bit output dimensions instead of 16-bit dimensions.
    /// </summary>
    private const byte LargeDimensionsFlag = 1;

    /// <summary>
    /// The minimum width and height of the first cell in a MIAF image grid.
    /// </summary>
    private const int MinimumGridCellDimension = 64;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifGridDescriptor"/> struct.
    /// </summary>
    /// <param name="rows">The number of grid rows.</param>
    /// <param name="columns">The number of grid columns.</param>
    /// <param name="outputSize">The output canvas dimensions.</param>
    private HeifGridDescriptor(int rows, int columns, Size outputSize)
    {
        this.Rows = rows;
        this.Columns = columns;
        this.OutputSize = outputSize;
    }

    /// <summary>
    /// Gets the number of grid rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of grid columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the output canvas dimensions.
    /// </summary>
    public Size OutputSize { get; }

    /// <summary>
    /// Parses and validates the fixed HEIF image-grid descriptor fields.
    /// </summary>
    /// <param name="data">The complete image-grid descriptor payload.</param>
    /// <param name="descriptor">Receives the row, column, and output dimensions.</param>
    /// <returns>The error message, or <see langword="null"/> when the descriptor is valid.</returns>
    public static string? Parse(ReadOnlySpan<byte> data, out HeifGridDescriptor descriptor)
    {
        descriptor = default;

        // The version and the flags come before the row count minus one and the column count minus one. The flags select 16-bit or 32-bit output dimensions.
        HeifSpanReader reader = new(data, "image grid descriptor");
        byte version = reader.ReadUInt8();
        byte flags = reader.ReadUInt8();
        int rows = reader.ReadUInt8() + 1;
        int columns = reader.ReadUInt8() + 1;
        bool usesLargeDimensions = (flags & LargeDimensionsFlag) != 0;
        uint outputWidth = reader.ReadU16Or32(usesLargeDimensions);
        uint outputHeight = reader.ReadU16Or32(usesLargeDimensions);
        if (!reader.IsComplete)
        {
            // A read past the end leaves no remaining bytes. Bytes that remain after the last field are trailing data.
            return reader.Remaining > 0
                ? "The image grid descriptor box contains unexpected trailing data."
                : "The image grid descriptor box is truncated.";
        }

        if (version != GridDescriptorVersion)
        {
            return $"The HEIF image grid descriptor has unsupported version {version}.";
        }

        if (outputWidth is 0 or > int.MaxValue || outputHeight is 0 or > int.MaxValue)
        {
            return "The HEIF image grid descriptor has invalid output dimensions.";
        }

        descriptor = new HeifGridDescriptor(rows, columns, new Size((int)outputWidth, (int)outputHeight));
        return null;
    }

    /// <summary>
    /// Validates the grid layout against its row-major tiles: the tile count, the canvas coverage, the MIAF cell-size and chroma-alignment rules, and the
    /// extent of each tile.
    /// </summary>
    /// <param name="tiles">
    /// The row-major tiles. The item graph validator validated that each tile is an AV1 item with a codec configuration.
    /// </param>
    /// <returns>The error message, or <see langword="null"/> when the tiles fill the grid.</returns>
    public string? Validate(HeifItem[] tiles)
    {
        int tileCount = this.Rows * this.Columns;
        if (tiles.Length != tileCount)
        {
            return $"The HEIF image grid requires {tileCount} tiles, but its derived-image references contain {tiles.Length}.";
        }

        // The first cell sets the cell size. The cells must cover the canvas. The last row and the last column must overlap the canvas.
        Size tileSize = tiles[0].Extent;
        if (((long)tileSize.Width * this.Columns) < this.OutputSize.Width || ((long)tileSize.Height * this.Rows) < this.OutputSize.Height)
        {
            return "The HEIF image grid tiles do not cover the output canvas.";
        }

        if (((long)tileSize.Width * (this.Columns - 1)) >= this.OutputSize.Width || ((long)tileSize.Height * (this.Rows - 1)) >= this.OutputSize.Height)
        {
            return "The HEIF image grid edge tiles do not overlap the output canvas.";
        }

        if (tileSize.Width < MinimumGridCellDimension || tileSize.Height < MinimumGridCellDimension)
        {
            return $"HEIF image grid cells must be at least {MinimumGridCellDimension} samples wide and high.";
        }

        Av1CodecConfiguration configuration = tiles[0].Av1CodecConfiguration!;
        if (!configuration.IsMonochrome)
        {
            if (configuration.ChromaSubsamplingX && (((this.OutputSize.Width & 1) != 0) || ((tileSize.Width & 1) != 0)))
            {
                return "HEIF image grid widths must be even when AV1 chroma is horizontally subsampled.";
            }

            if (configuration.ChromaSubsamplingY && (((this.OutputSize.Height & 1) != 0) || ((tileSize.Height & 1) != 0)))
            {
                return "HEIF image grid heights must be even when AV1 chroma is vertically subsampled.";
            }
        }

        // Each cell must cover its output region. A cell must not be larger than the first cell. This validation occurs before the decoder allocates the canvas
        // or the alpha plane.
        for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
        {
            Size extent = tiles[tileIndex].Extent;
            Size copySize = this.GetTileCopySize(tileSize, tileIndex);
            if (extent.Width < copySize.Width || extent.Width > tileSize.Width || extent.Height < copySize.Height || extent.Height > tileSize.Height)
            {
                return $"HEIF image grid tile {tiles[tileIndex].Id} has dimensions {extent}, which cannot cover its {copySize} grid region.";
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the size of the part of one cell that overlaps the output canvas.
    /// </summary>
    /// <param name="tileSize">The size of the first cell.</param>
    /// <param name="tileIndex">The row-major index of the cell.</param>
    /// <returns>The size of the part of the cell that overlaps the output canvas.</returns>
    public Size GetTileCopySize(Size tileSize, int tileIndex)
    {
        int column = tileIndex % this.Columns;
        int row = tileIndex / this.Columns;
        int copyWidth = column == this.Columns - 1 ? this.OutputSize.Width - (tileSize.Width * (this.Columns - 1)) : tileSize.Width;
        int copyHeight = row == this.Rows - 1 ? this.OutputSize.Height - (tileSize.Height * (this.Rows - 1)) : tileSize.Height;
        return new Size(copyWidth, copyHeight);
    }

    /// <summary>
    /// Gets the region of the output canvas that one cell covers.
    /// </summary>
    /// <param name="tileSize">The size of the first cell.</param>
    /// <param name="tileIndex">The row-major index of the cell.</param>
    /// <returns>
    /// The canvas region of the cell. The location is the origin of the cell, and the size is the part of the cell that overlaps the output canvas.
    /// </returns>
    public Rectangle GetTileBounds(Size tileSize, int tileIndex)
    {
        Size copySize = this.GetTileCopySize(tileSize, tileIndex);
        return new Rectangle((tileIndex % this.Columns) * tileSize.Width, (tileIndex / this.Columns) * tileSize.Height, copySize.Width, copySize.Height);
    }
}
