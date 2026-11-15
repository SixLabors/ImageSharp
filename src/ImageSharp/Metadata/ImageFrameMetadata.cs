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
/// Encapsulates the metadata of an image frame.
/// </summary>
public sealed class ImageFrameMetadata : IDeepCloneable<ImageFrameMetadata>
{
    private readonly Dictionary<IImageFormat, IFormatFrameMetadata> formatMetadata = [];
    private ExifProfile? exifProfile;
    private XmpProfile? xmpProfile;
    private CicpProfile? cicpProfile;
    private IccProfile? iccProfile;
    private IptcProfile? iptcProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrameMetadata"/> class.
    /// </summary>
    internal ImageFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFrameMetadata"/> class
    /// by making a copy from other metadata.
    /// </summary>
    /// <param name="other">
    /// The other <see cref="ImageFrameMetadata"/> to create this instance from.
    /// </param>
    internal ImageFrameMetadata(ImageFrameMetadata other)
    {
        DebugGuard.NotNull(other, nameof(other));

        foreach (KeyValuePair<IImageFormat, IFormatFrameMetadata> meta in other.formatMetadata)
        {
            this.formatMetadata.Add(meta.Key, (IFormatFrameMetadata)meta.Value.DeepClone());
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
    /// Gets the original format, if any, the image was decode from.
    /// </summary>
    public IImageFormat? DecodedImageFormat { get; internal set; }

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
    public ImageFrameMetadata DeepClone() => new(this);

    /// <summary>
    /// Gets the metadata value associated with the specified key.<br/>
    /// If none is found, an instance is created either by conversion from the decoded image format metadata
    /// or the requested format default constructor.
    /// This instance will be added to the metadata for future requests.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatFrameMetadata"/>.
    /// </returns>
    public TFormatFrameMetadata GetFormatMetadata<TFormatMetadata, TFormatFrameMetadata>(IImageFormat<TFormatMetadata, TFormatFrameMetadata> key)
        where TFormatMetadata : class
        where TFormatFrameMetadata : class, IFormatFrameMetadata<TFormatFrameMetadata>
    {
        if (this.formatMetadata.TryGetValue(key, out IFormatFrameMetadata? meta))
        {
            return (TFormatFrameMetadata)meta;
        }

        // None found. Check if we have a decoded format to convert from.
        if (this.DecodedImageFormat is not null
            && this.formatMetadata.TryGetValue(this.DecodedImageFormat, out IFormatFrameMetadata? decodedMetadata))
        {
            TFormatFrameMetadata derivedMeta = TFormatFrameMetadata.FromFormatConnectingFrameMetadata(decodedMetadata.ToFormatConnectingFrameMetadata());
            this.SetFormatMetadata(key, derivedMeta);
            return derivedMeta;
        }

        TFormatFrameMetadata newMeta = key.CreateDefaultFormatFrameMetadata();
        this.SetFormatMetadata(key, newMeta);
        return newMeta;
    }

    /// <summary>
    /// Sets the metadata value associated with the specified key.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    /// <param name="key">The key of the value to set.</param>
    /// <param name="value">The value to set.</param>
    public void SetFormatMetadata<TFormatMetadata, TFormatFrameMetadata>(IImageFormat<TFormatMetadata, TFormatFrameMetadata> key, TFormatFrameMetadata value)
        where TFormatMetadata : class
        where TFormatFrameMetadata : class, IFormatFrameMetadata<TFormatFrameMetadata>
        => this.formatMetadata[key] = value;

    /// <summary>
    /// Creates a new instance the metadata value associated with the specified key.
    /// The instance is created from a clone generated via <see cref="GetFormatMetadata{TFormatMetadata, TFormatFrameMetadata}(IImageFormat{TFormatMetadata, TFormatFrameMetadata})"/>.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatMetadata"/>.
    /// </returns>
    public TFormatFrameMetadata CloneFormatMetadata<TFormatMetadata, TFormatFrameMetadata>(IImageFormat<TFormatMetadata, TFormatFrameMetadata> key)
        where TFormatMetadata : class
        where TFormatFrameMetadata : class, IFormatFrameMetadata<TFormatFrameMetadata>
        => ((IDeepCloneable<TFormatFrameMetadata>)this.GetFormatMetadata(key)).DeepClone();

    /// <summary>
    /// Synchronizes the profiles with the current metadata.
    /// </summary>
    internal void SynchronizeProfiles() => this.ExifProfile?.Sync(this);

    /// <summary>
    /// This method is called after a process has been applied to the image frame.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="source">The source image frame.</param>
    /// <param name="destination">The destination image frame.</param>
    /// <param name="matrix">The transformation matrix applied to the frame.</param>
    internal void AfterFrameApply<TPixel>(
        ImageFrame<TPixel> source,
        ImageFrame<TPixel> destination,
        Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Always updated using the full frame dimensions.
        // Individual format frame metadata will update with sub region dimensions if appropriate.
        this.ExifProfile?.SyncDimensions(destination.Width, destination.Height);
        this.ExifProfile?.SyncSubject(destination.Width, destination.Height, matrix);

        foreach (KeyValuePair<IImageFormat, IFormatFrameMetadata> meta in this.formatMetadata)
        {
            meta.Value.AfterFrameApply(source, destination, matrix);
        }
    }
}
