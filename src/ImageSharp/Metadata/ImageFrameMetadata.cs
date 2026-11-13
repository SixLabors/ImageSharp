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
/// Encapsulates the metadata of an image frame.
/// </summary>
public sealed class ImageFrameMetadata : IDeepCloneable<ImageFrameMetadata>
{
    private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetadata = new();
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

        foreach (KeyValuePair<IImageFormat, IDeepCloneable> meta in other.formatMetadata)
        {
            this.formatMetadata.Add(meta.Key, meta.Value.DeepClone());
        }

        this.exifProfile = other.exifProfile?.Validate() == true ? other.exifProfile.DeepClone() : null;
        this.iccProfile = other.iccProfile?.CloneForMetadata();
        this.iptcProfile = other.iptcProfile?.Validate() == true ? other.iptcProfile.DeepClone() : null;
        XmpProfile? xmp = other.xmpProfile?.DeepClone();
        this.xmpProfile = xmp?.Validate() == true ? xmp : null;
        this.cicpProfile = other.cicpProfile?.Validate() == true ? other.cicpProfile.DeepClone() : null;
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
    /// Gets the metadata value associated with the specified key. This method will always return a result creating
    /// a new instance and binding it to the frame metadata if none is found.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// The <typeparamref name="TFormatFrameMetadata"/>.
    /// </returns>
    public TFormatFrameMetadata GetFormatMetadata<TFormatMetadata, TFormatFrameMetadata>(IImageFormat<TFormatMetadata, TFormatFrameMetadata> key)
        where TFormatMetadata : class
        where TFormatFrameMetadata : class, IDeepCloneable
    {
        if (this.formatMetadata.TryGetValue(key, out IDeepCloneable? meta))
        {
            return (TFormatFrameMetadata)meta;
        }

        TFormatFrameMetadata newMeta = key.CreateDefaultFormatFrameMetadata();
        this.formatMetadata[key] = newMeta;
        return newMeta;
    }

    /// <summary>
    /// Gets the metadata value associated with the specified key.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of format metadata.</typeparam>
    /// <typeparam name="TFormatFrameMetadata">The type of format frame metadata.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified key,
    /// if the key is found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the frame metadata exists for the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetFormatMetadata<TFormatMetadata, TFormatFrameMetadata>(IImageFormat<TFormatMetadata, TFormatFrameMetadata> key, out TFormatFrameMetadata? metadata)
        where TFormatMetadata : class
        where TFormatFrameMetadata : class, IDeepCloneable
    {
        if (this.formatMetadata.TryGetValue(key, out IDeepCloneable? meta))
        {
            metadata = (TFormatFrameMetadata)meta;
            return true;
        }

        metadata = default;
        return false;
    }
}
