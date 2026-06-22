// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Provides definition for a HEIF Item.
/// </summary>
internal class HeifItem(Heif4CharCode type, uint id)
{
    /// <summary>
    /// Gets the ID of this Item.
    /// </summary>
    public uint Id { get; } = id;

    /// <summary>
    /// Gets the type of this Item.
    /// </summary>
    public Heif4CharCode Type { get; } = type;

    /// <summary>
    /// Gets or sets the name of this item.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the Content Type of this item.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the Content Encoding of this item.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the type of extension of this item.
    /// </summary>
    public uint ExtensionType { get; set; }

    /// <summary>
    /// Gets or sets the URI of this item.
    /// </summary>
    public string? UriType { get; set; }

    /// <summary>
    /// Gets or sets the aspect ratio of the pixels.
    /// </summary>
    public Size PixelAspectRatio { get; set; }

    /// <summary>
    /// Gets or sets the number of color channels in each pixel.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the number of bits in a single pixel.
    /// </summary>
    public int BitsPerPixel { get; set; }

    /// <summary>
    /// Gets the spatial extent of this item.
    /// </summary>
    public Size Extent { get; private set; }

    /// <summary>
    /// Gets the spatial extent of this grid cells in this item.
    /// </summary>
    public Size GridCellExtent { get; private set; }

    /// <summary>
    /// Gets the list of data locations for this item.
    /// </summary>
    public List<HeifLocation> DataLocations { get; } = [];

    /// <summary>
    /// Set the image extent.
    /// </summary>
    /// <param name="extent">The size to set the extent to.</param>
    /// <remarks>
    /// Might be called twice for a grid, in which case the second call is the cell extent.
    /// </remarks>
    public void SetExtent(Size extent)
    {
        if (this.Extent == default)
        {
            this.Extent = extent;
        }
        else
        {
            this.GridCellExtent = extent;
        }
    }

    public override string ToString() => $"{this.Type}:{this.Id}";
}
