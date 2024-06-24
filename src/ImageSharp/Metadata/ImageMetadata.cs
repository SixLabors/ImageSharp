// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Metadata;

/// <summary>
/// Encapsulates the metadata of an image.
/// </summary>
public sealed class ImageMetadata : IDeepCloneable<ImageMetadata>
{
    /// <summary>
    /// The default horizontal resolution value (dots per inch) in x direction.
    /// <remarks>The default value is 96 <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
    /// </summary>
    public const double DefaultHorizontalResolution = 96;

    /// <summary>
    /// The default vertical resolution value (dots per inch) in y direction.
    /// <remarks>The default value is 96 <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
    /// </summary>
    public const double DefaultVerticalResolution = 96;

    /// <summary>
    /// The default pixel resolution units.
    /// <remarks>The default value is <see cref="PixelResolutionUnit.PixelsPerInch"/>.</remarks>
    /// </summary>
    public const PixelResolutionUnit DefaultPixelResolutionUnits = PixelResolutionUnit.PixelsPerInch;

    private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetadata = new();
    private double horizontalResolution;
    private double verticalResolution;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageMetadata"/> class.
    /// </summary>
    public ImageMetadata()
    {
        this.horizontalResolution = DefaultHorizontalResolution;
        this.verticalResolution = DefaultVerticalResolution;
        this.ResolutionUnits = DefaultPixelResolutionUnits;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageMetadata"/> class
    /// by making a copy from other metadata.
    /// </summary>
    /// <param name="other">
    /// The other <see cref="ImageMetadata"/> to create this instance from.
    /// </param>
    private ImageMetadata(ImageMetadata other)
    {
        this.HorizontalResolution = other.HorizontalResolution;
        this.VerticalResolution = other.VerticalResolution;
        this.ResolutionUnits = other.ResolutionUnits;

        foreach (KeyValuePair<IImageFormat, IDeepCloneable> meta in other.formatMetadata)
        {
            this.formatMetadata.Add(meta.Key, meta.Value.DeepClone());
        }

        this.ExifProfile = other.ExifProfile?.DeepClone();
        this.IccProfile = other.IccProfile?.DeepClone();
        this.IptcProfile = other.IptcProfile?.DeepClone();
        this.XmpProfile = other.XmpProfile?.DeepClone();
        this.CicpProfile = other.CicpProfile?.DeepClone();

        // NOTE: This clone is actually shallow but we share the same format
        // instances for all images in the configuration.
        this.DecodedImageFormat = other.DecodedImageFormat;
    }

    /// <summary>
    /// Gets or sets the resolution of the image in x- direction.
    /// It is defined as the number of dots per <see cref="ResolutionUnits"/> and should be an positive value.
    /// </summary>
    /// <value>The density of the image in x- direction.</value>
    public double HorizontalResolution
    {
        get => this.horizontalResolution;

        set
        {
            if (value > 0)
            {
                this.horizontalResolution = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the resolution of the image in y- direction.
    /// It is defined as the number of dots per <see cref="ResolutionUnits"/> and should be an positive value.
    /// </summary>
    /// <value>The density of the image in y- direction.</value>
    public double VerticalResolution
    {
        get => this.verticalResolution;

        set
        {
            if (value > 0)
            {
                this.verticalResolution = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets unit of measure used when reporting resolution.
    /// <list type="table">
    ///   <listheader>
    ///     <term>Value</term>
    ///     <description>Unit</description>
    ///   </listheader>
    ///   <item>
    ///     <term>AspectRatio (00)</term>
    ///     <description>No units; width:height pixel aspect ratio = Ydensity:Xdensity</description>
    ///   </item>
    ///   <item>
    ///     <term>PixelsPerInch (01)</term>
    ///     <description>Pixels per inch (2.54 cm)</description>
    ///   </item>
    ///   <item>
    ///     <term>PixelsPerCentimeter (02)</term>
    ///     <description>Pixels per centimeter</description>
    ///   </item>
    ///   <item>
    ///     <term>PixelsPerMeter (03)</term>
    ///     <description>Pixels per meter (100 cm)</description>
    ///   </item>
    /// </list>
    /// </summary>
    public PixelResolutionUnit ResolutionUnits { get; set; }

    /// <summary>
    /// Gets or sets the Exif profile.
    /// </summary>
    public ExifProfile? ExifProfile { get; set; }

    /// <summary>
    /// Gets or sets the XMP profile.
    /// </summary>
    public XmpProfile? XmpProfile { get; set; }

    /// <summary>
    /// Gets or sets the ICC profile.
    /// </summary>
    public IccProfile? IccProfile { get; set; }

    /// <summary>
    /// Gets or sets the IPTC profile.
    /// </summary>
    public IptcProfile? IptcProfile { get; set; }

    /// <summary>
    /// Gets or sets the CICP profile.
    /// </summary>
    public CicpProfile? CicpProfile { get; set; }

    /// <summary>
    /// Gets the original format, if any, the image was decode from.
    /// </summary>
    public IImageFormat? DecodedImageFormat { get; internal set; }

    /// <summary>
    /// Gets the metadata value associated with the specified key.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatMetadata"/>.
    /// </returns>
    public TFormatMetadata GetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key)
         where TFormatMetadata : class, IDeepCloneable
    {
        if (this.formatMetadata.TryGetValue(key, out IDeepCloneable? meta))
        {
            return (TFormatMetadata)meta;
        }

        TFormatMetadata newMeta = key.CreateDefaultFormatMetadata();
        this.formatMetadata[key] = newMeta;
        return newMeta;
    }

    /// <summary>
    /// Gets the metadata value associated with the specified key.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified key,
    /// if the key is found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the frame metadata exists for the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key, out TFormatMetadata? metadata)
        where TFormatMetadata : class, IDeepCloneable
    {
        if (this.formatMetadata.TryGetValue(key, out IDeepCloneable? meta))
        {
            metadata = (TFormatMetadata)meta;
            return true;
        }

        metadata = default;
        return false;
    }

    internal void SetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key, TFormatMetadata value)
        where TFormatMetadata : class, IDeepCloneable
        => this.formatMetadata[key] = value;

    /// <inheritdoc/>
    public ImageMetadata DeepClone() => new(this);

    /// <summary>
    /// Synchronizes the profiles with the current metadata.
    /// </summary>
    internal void SyncProfiles() => this.ExifProfile?.Sync(this);
}
