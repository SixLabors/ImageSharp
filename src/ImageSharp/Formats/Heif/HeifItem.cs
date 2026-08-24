// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes a metadata or image item in a HEIF still-image container.
/// </summary>
/// <param name="type">The four-character item type.</param>
/// <param name="id">The item identifier used by locations, properties, and references.</param>
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
    /// Gets or sets the registered auxiliary type associated with this image item.
    /// </summary>
    public string? AuxiliaryType { get; set; }

    /// <summary>
    /// Gets or sets the ICC profile associated with this color image item, or <see langword="null"/> when the item
    /// has no restricted or unrestricted ICC color-information property.
    /// </summary>
    public IccProfile? IccProfile { get; set; }

    /// <summary>
    /// Gets or sets the CICP color description associated with this color image item, or <see langword="null"/>
    /// when the item has no <c>nclx</c> color-information property.
    /// </summary>
    public CicpProfile? CicpProfile { get; set; }

    /// <summary>
    /// Gets or sets the content light-level information associated with this image item, or <see langword="null"/>
    /// when the item has no content light-level property.
    /// </summary>
    public HeifContentLightLevel? ContentLightLevel { get; set; }

    /// <summary>
    /// Gets or sets the mastering-display color volume associated with this image item, or <see langword="null"/>
    /// when the item has no mastering-display property.
    /// </summary>
    public HeifMasteringDisplayColorVolume? MasteringDisplayColorVolume { get; set; }

    /// <summary>
    /// Gets or sets the AV1 codec configuration associated with this coded image item, or <see langword="null"/>
    /// when the item has no AV1 codec-configuration property.
    /// </summary>
    public Av1CodecConfiguration? Av1CodecConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the relative pixel spacing associated with this image item, or <see langword="null"/> when the
    /// item has no pixel-aspect-ratio property.
    /// </summary>
    public HeifPixelAspectRatio? PixelAspectRatio { get; set; }

    /// <summary>
    /// Gets or sets the clean-aperture crop applied before image rotation and mirroring, or
    /// <see langword="null"/> when no clean-aperture property is associated with the item.
    /// </summary>
    public HeifCleanAperture? CleanAperture { get; set; }

    /// <summary>
    /// Gets or sets the number of 90-degree counter-clockwise rotations applied to the image, or
    /// <see langword="null"/> when no image-rotation property is associated with the item.
    /// </summary>
    public byte? RotationAngle { get; set; }

    /// <summary>
    /// Gets or sets the image-mirror axis, where zero is the horizontal axis and one is the vertical axis, or
    /// <see langword="null"/> when no image-mirror property is associated with the item.
    /// </summary>
    public byte? MirrorAxis { get; set; }

    /// <summary>
    /// Gets or sets the number of color channels in each pixel.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the encoded precision of each image channel, or <see langword="null"/> when the item has no
    /// pixel-information property.
    /// </summary>
    public byte[]? ChannelBitDepths { get; set; }

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

    /// <summary>
    /// Returns the item type and identifier.
    /// </summary>
    /// <returns>The item type and identifier separated by a colon.</returns>
    public override string ToString() => $"{this.Type}:{this.Id}";
}
