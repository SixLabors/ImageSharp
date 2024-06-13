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
    private readonly Dictionary<IImageFormat, IFormatFrameMetadata> formatMetadata = [];

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
    /// Gets or sets the iptc profile.
    /// </summary>
    public IptcProfile? IptcProfile { get; set; }

    /// <summary>
    /// Gets or sets the CICP profile
    /// </summary>
    public CicpProfile? CicpProfile { get; set; }

    /// <summary>
    /// Gets the original format, if any, the image was decode from.
    /// </summary>
    public IImageFormat? DecodedImageFormat { get; internal set; }

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
            return TFormatFrameMetadata.FromFormatConnectingFrameMetadata(decodedMetadata.ToFormatConnectingFrameMetadata());
        }

        TFormatFrameMetadata newMeta = key.CreateDefaultFormatFrameMetadata();
        this.formatMetadata[key] = newMeta;
        return newMeta;
    }

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
}
