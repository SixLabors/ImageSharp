// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Gives the items that the still-image decoder reads for the primary image. <see cref="HeifItemGraphValidator"/> creates the plan after the decoder parses the
/// metadata boxes, and before the decoder allocates the image.
/// </summary>
internal sealed class HeifDecodePlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecodePlan"/> class.
    /// </summary>
    /// <param name="primary">The primary image item.</param>
    /// <param name="codedItem">The coded item that gives the codec description of the primary image.</param>
    /// <param name="tiles">The row-major tiles of a primary grid, or an empty array.</param>
    /// <param name="tilePremultiplied">For each tile, a value that is <see langword="true"/> if an alpha item of the tile premultiplies the tile color.</param>
    /// <param name="sourceRectangle">The clean-aperture region of the primary image.</param>
    /// <param name="alphaItem">The item whose payload gives the alpha plane or the alpha grid layout, or <see langword="null"/>.</param>
    /// <param name="alphaTiles">The row-major alpha tiles, or an empty array when the alpha plane is not a grid.</param>
    /// <param name="premultiplied"><see langword="true"/> if the alpha item premultiplies the primary item.</param>
    public HeifDecodePlan(
        HeifItem primary,
        HeifItem codedItem,
        HeifItem[] tiles,
        bool[] tilePremultiplied,
        Rectangle sourceRectangle,
        HeifItem? alphaItem,
        HeifItem[] alphaTiles,
        bool premultiplied)
    {
        this.Primary = primary;
        this.CodedItem = codedItem;
        this.Tiles = tiles;
        this.TilePremultiplied = tilePremultiplied;
        this.SourceRectangle = sourceRectangle;
        this.AlphaItem = alphaItem;
        this.AlphaTiles = alphaTiles;
        this.Premultiplied = premultiplied;
    }

    /// <summary>
    /// Gets the primary image item.
    /// </summary>
    public HeifItem Primary { get; }

    /// <summary>
    /// Gets the coded item that gives the codec description of the primary image. This item is the first tile of a grid, or the primary item.
    /// </summary>
    public HeifItem CodedItem { get; }

    /// <summary>
    /// Gets the row-major tiles of a primary grid, or an empty array when the primary item is a coded item.
    /// </summary>
    public HeifItem[] Tiles { get; }

    /// <summary>
    /// Gets, for each tile, a value indicating whether an alpha item of the tile premultiplies its color.
    /// </summary>
    public bool[] TilePremultiplied { get; }

    /// <summary>
    /// Gets the clean-aperture region of the primary image in its spatial extents.
    /// </summary>
    public Rectangle SourceRectangle { get; }

    /// <summary>
    /// Gets the item whose payload gives the alpha plane or the alpha grid layout, or <see langword="null"/> when the image has no alpha plane. For alpha tiles
    /// of a color grid, this item is the primary grid.
    /// </summary>
    public HeifItem? AlphaItem { get; }

    /// <summary>
    /// Gets the row-major alpha tiles, or an empty array when the alpha plane is not a grid.
    /// </summary>
    public HeifItem[] AlphaTiles { get; }

    /// <summary>
    /// Gets a value indicating whether the alpha item premultiplies the color of the primary item.
    /// </summary>
    public bool Premultiplied { get; }
}
