// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

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

    private readonly Dictionary<IImageFormat, IFormatMetadata> formatMetadata = [];
    private ExifProfile? exifProfile;
    private XmpProfile? xmpProfile;
    private CicpProfile? cicpProfile;
    private IccProfile? iccProfile;
    private IptcProfile? iptcProfile;
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

        foreach (KeyValuePair<IImageFormat, IFormatMetadata> meta in other.formatMetadata)
        {
            this.formatMetadata.Add(meta.Key, (IFormatMetadata)meta.Value.DeepClone());
        }

        this.exifProfile = other.exifProfile?.Validate() == true ? other.exifProfile.DeepClone() : null;
        this.iccProfile = other.iccProfile?.CloneForMetadata();
        this.iptcProfile = other.iptcProfile?.Validate() == true ? other.iptcProfile.DeepClone() : null;
        XmpProfile? xmp = other.xmpProfile?.DeepClone();
        this.xmpProfile = xmp?.Validate() == true ? xmp : null;
        this.cicpProfile = other.cicpProfile?.Validate() == true ? other.cicpProfile.DeepClone() : null;

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
    /// Gets or sets the EXIF profile. Assign null to clear it.
    /// </summary>
    /// <exception cref="ArgumentException">The supplied profile is invalid or empty.</exception>
    public ExifProfile? ExifProfile
    {
        get => this.exifProfile;
        set
        {
            if (!this.TrySetExifProfile(value))
            {
                throw new ArgumentException("The EXIF profile is invalid or empty.", nameof(value));
            }
        }
    }

    /// <summary>
    /// Gets or sets the XMP profile. Assign null to clear it.
    /// </summary>
    /// <exception cref="ArgumentException">The supplied profile is invalid or empty.</exception>
    public XmpProfile? XmpProfile
    {
        get => this.xmpProfile;
        set
        {
            if (value != null && !value.Validate())
            {
                throw new ArgumentException("The XMP profile is invalid or empty.", nameof(value));
            }

            this.xmpProfile = value;
        }
    }

    /// <summary>
    /// Gets or sets the ICC profile. Assign null to clear it.
    /// </summary>
    /// <exception cref="ArgumentException">The supplied profile is invalid or empty.</exception>
    public IccProfile? IccProfile
    {
        get => this.iccProfile;
        set
        {
            if (value != null && !value.Validate())
            {
                throw new ArgumentException("The ICC profile is invalid or empty.", nameof(value));
            }

            this.iccProfile = value;
        }
    }

    /// <summary>
    /// Gets or sets the IPTC profile. Assign null to clear it.
    /// </summary>
    /// <exception cref="ArgumentException">The supplied profile is invalid or empty.</exception>
    public IptcProfile? IptcProfile
    {
        get => this.iptcProfile;
        set
        {
            if (!this.TrySetIptcProfile(value))
            {
                throw new ArgumentException("The IPTC profile is invalid or empty.", nameof(value));
            }
        }
    }

    /// <summary>
    /// Gets or sets the CICP profile. Assign null to clear it.
    /// </summary>
    /// <exception cref="ArgumentException">The supplied profile is invalid or empty.</exception>
    public CicpProfile? CicpProfile
    {
        get => this.cicpProfile;
        set
        {
            if (value != null && !value.Validate())
            {
                throw new ArgumentException("The CICP profile is invalid or empty.", nameof(value));
            }

            this.cicpProfile = value;
        }
    }

    /// <summary>
    /// Gets the original format, if any, from which the image was decoded.
    /// </summary>
    public IImageFormat? DecodedImageFormat { get; internal set; }

    /// <summary>
    /// Gets the metadata value associated with the specified key.<br/>
    /// If none is found, an instance is created either by conversion from the decoded image format metadata
    /// or the requested format default constructor.
    /// This instance will be added to the metadata for future requests.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatMetadata"/>.
    /// </returns>
    public TFormatMetadata GetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key)
         where TFormatMetadata : class, IFormatMetadata<TFormatMetadata>
    {
        // Check for existing metadata.
        if (this.formatMetadata.TryGetValue(key, out IFormatMetadata? meta))
        {
            return (TFormatMetadata)meta;
        }

        // None found. Check if we have a decoded format to convert from.
        if (this.DecodedImageFormat is not null
            && this.formatMetadata.TryGetValue(this.DecodedImageFormat, out IFormatMetadata? decodedMetadata))
        {
            TFormatMetadata derivedMeta = TFormatMetadata.FromFormatConnectingMetadata(decodedMetadata.ToFormatConnectingMetadata());
            this.formatMetadata[key] = derivedMeta;
            return derivedMeta;
        }

        // Fall back to a default instance.
        TFormatMetadata newMeta = key.CreateDefaultFormatMetadata();
        this.formatMetadata[key] = newMeta;
        return newMeta;
    }

    /// <summary>
    /// Creates a new instance the metadata value associated with the specified key.
    /// The instance is created from a clone generated via <see cref="GetFormatMetadata{TFormatMetadata}(IImageFormat{TFormatMetadata})"/>.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatMetadata"/>.
    /// </returns>
    public TFormatMetadata CloneFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key)
        where TFormatMetadata : class, IFormatMetadata<TFormatMetadata>
        => ((IDeepCloneable<TFormatMetadata>)this.GetFormatMetadata(key)).DeepClone();

    internal void SetFormatMetadata<TFormatMetadata>(IImageFormat<TFormatMetadata> key, TFormatMetadata value)
        where TFormatMetadata : class, IFormatMetadata<TFormatMetadata>
        => this.formatMetadata[key] = value;

    /// <summary>
    /// Stores decoded EXIF metadata when the profile is valid.
    /// </summary>
    /// <param name="profile">The profile to store, or null to clear it.</param>
    /// <returns>Whether the profile was stored.</returns>
    internal bool TrySetExifProfile(ExifProfile? profile)
    {
        if (profile != null && !profile.Validate())
        {
            return false;
        }

        this.exifProfile = profile;
        return true;
    }

    /// <summary>
    /// Stores decoded IPTC metadata when the profile is valid.
    /// </summary>
    /// <param name="profile">The profile to store, or null to clear it.</param>
    /// <returns>Whether the profile was stored.</returns>
    internal bool TrySetIptcProfile(IptcProfile? profile)
    {
        if (profile != null && !profile.Validate())
        {
            return false;
        }

        this.iptcProfile = profile;
        return true;
    }

    /// <inheritdoc/>
    public ImageMetadata DeepClone() => new(this);

    /// <summary>
    /// Synchronizes the profiles with the current metadata.
    /// </summary>
    internal void SynchronizeProfiles() => this.ExifProfile?.Sync(this);

    /// <summary>
    /// This method is called after a process has been applied to the image.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="destination">The destination image.</param>
    /// <param name="matrix">The transformation matrix applied to the image.</param>
    internal void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.ExifProfile?.SyncDimensions(destination.Width, destination.Height);
        this.ExifProfile?.SyncSubject(destination.Width, destination.Height, matrix);

        foreach (KeyValuePair<IImageFormat, IFormatMetadata> meta in this.formatMetadata)
        {
            meta.Value.AfterImageApply(destination, matrix);
        }
    }

    internal PixelTypeInfo GetDecodedPixelTypeInfo()
    {
        // None found. Check if we have a decoded format to convert from.
        if (this.DecodedImageFormat is not null
            && this.formatMetadata.TryGetValue(this.DecodedImageFormat, out IFormatMetadata? decodedMetadata))
        {
            return decodedMetadata.GetPixelTypeInfo();
        }

        // This should never happen.
        return default;
    }
}
