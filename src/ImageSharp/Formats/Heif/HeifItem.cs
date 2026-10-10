// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes a metadata or image item in a HEIF still-image container.
/// </summary>
internal sealed class HeifItem
{
    private IccProfile? iccProfile;

    private ReadOnlyMemory<byte> serializedIccProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifItem"/> class.
    /// </summary>
    /// <param name="type">The four-character item type.</param>
    /// <param name="id">The item identifier used by locations, properties, and references.</param>
    public HeifItem(Heif4CharCode type, uint id)
    {
        this.Type = type;
        this.Id = id;
    }

    /// <summary>
    /// Gets the item identifier.
    /// </summary>
    public uint Id { get; }

    /// <summary>
    /// Gets the four-character item type.
    /// </summary>
    public Heif4CharCode Type { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this item is excluded from primary-item discovery.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets or sets an earlier item whose identical property associations are reused by this item.
    /// </summary>
    public HeifItem? PropertySource { get; set; }

    /// <summary>
    /// Gets or sets the first one-based property index assigned while writing the property container.
    /// </summary>
    public ushort FirstPropertyIndex { get; set; }

    /// <summary>
    /// Gets or sets the name of this item.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the content type of a mime item.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content encoding of a mime item, or <see langword="null"/> when the item has none.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the extension type of the item information entry.
    /// </summary>
    public uint ExtensionType { get; set; }

    /// <summary>
    /// Gets or sets the URI type of a uri item.
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
    public IccProfile? IccProfile
    {
        get => this.iccProfile;
        set
        {
            this.iccProfile = value;
            this.serializedIccProfile = default;
        }
    }

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
    /// Gets or sets the content color volume associated with this image item, or <see langword="null"/> when the
    /// item has no content color-volume property.
    /// </summary>
    public HeifContentColorVolume? ContentColorVolume { get; set; }

    /// <summary>
    /// Gets or sets the nominal ambient viewing environment associated with this image item, or
    /// <see langword="null"/> when the item has no ambient viewing-environment property.
    /// </summary>
    public HeifAmbientViewingEnvironment? AmbientViewingEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the reference mastering environment associated with this image item, or
    /// <see langword="null"/> when the item has no reference viewing-environment property.
    /// </summary>
    public HeifReferenceViewingEnvironment? ReferenceViewingEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the nominal diffuse-white description associated with this image item, or
    /// <see langword="null"/> when the item has no nominal diffuse-white property.
    /// </summary>
    public HeifNominalDiffuseWhite? NominalDiffuseWhite { get; set; }

    /// <summary>
    /// Gets or sets the AV1 codec configuration associated with this coded image item, or <see langword="null"/>
    /// when the item has no AV1 codec-configuration property.
    /// </summary>
    public Av1CodecConfiguration? Av1CodecConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the operating-point selector associated with this AV1 image item, or <see langword="null"/> when
    /// the item uses the default operating-point index zero.
    /// </summary>
    public Av1OperatingPointSelector? Av1OperatingPointSelector { get; set; }

    /// <summary>
    /// Gets or sets the spatial-layer selector associated with this AV1 image item, or <see langword="null"/> when
    /// no explicit layer selection is present.
    /// </summary>
    public Av1LayerSelector? Av1LayerSelector { get; set; }

    /// <summary>
    /// Gets or sets the layered-image payload index associated with this AV1 image item, or <see langword="null"/>
    /// when the payload does not provide explicit layer boundaries.
    /// </summary>
    public Av1LayeredImageIndex? Av1LayeredImageIndex { get; set; }

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
    /// Gets or sets the image-mirror axis, or <see langword="null"/> when the item has no image-mirror property.
    /// Zero is the horizontal axis, and one is the vertical axis.
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
    /// Gets or sets the common encoded precision of every image channel, or <see langword="null"/> when channel
    /// precision is absent or represented individually by <see cref="ChannelBitDepths"/>.
    /// </summary>
    public byte? UniformChannelBitDepth { get; set; }

    /// <summary>
    /// Gets or sets the number of bits in a single pixel.
    /// </summary>
    public int BitsPerPixel { get; set; }

    /// <summary>
    /// Gets the spatial extent of this item.
    /// </summary>
    public Size Extent { get; private set; }

    /// <summary>
    /// Gets the coded extent of each grid cell of this item, or an empty size when the item has only one extent.
    /// </summary>
    public Size GridCellExtent { get; private set; }

    /// <summary>
    /// Gets the list of data locations for this item.
    /// </summary>
    public List<HeifLocation> DataLocations { get; } = [];

    /// <summary>
    /// Gets the serialized ICC payload used while sizing and writing an encoded item.
    /// </summary>
    /// <returns>The serialized profile data, or an empty memory when no profile is assigned.</returns>
    public ReadOnlyMemory<byte> GetIccProfileDataForWriting()
    {
        if (this.serializedIccProfile.IsEmpty && this.iccProfile is not null)
        {
            // The container writer asks for the payload length before it copies the payload. Keep the serialized
            // bytes on this item, so the profile is serialized once for both passes.
            this.serializedIccProfile = this.iccProfile.ToByteArray();
        }

        return this.serializedIccProfile;
    }

    /// <summary>
    /// Sets the image extent. The first call sets <see cref="Extent"/>. A later call sets <see cref="GridCellExtent"/>.
    /// </summary>
    /// <param name="extent">The extent to set.</param>
    /// <remarks>
    /// The decoder can call this method twice for a grid. The second call gives the cell extent.
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
