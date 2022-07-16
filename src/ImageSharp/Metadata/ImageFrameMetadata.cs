// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Metadata
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetadata : IDeepCloneable<ImageFrameMetadata>
    {
        private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetadata = new();

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

            this.ExifProfile = other.ExifProfile?.DeepClone();
            this.IccProfile = other.IccProfile?.DeepClone();
            this.IptcProfile = other.IptcProfile?.DeepClone();
            this.XmpProfile = other.XmpProfile?.DeepClone();
        }

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets the XMP profile.
        /// </summary>
        public XmpProfile XmpProfile { get; set; }

        /// <summary>
        /// Gets or sets the list of ICC profiles.
        /// </summary>
        public IccProfile IccProfile { get; set; }

        /// <summary>
        /// Gets or sets the iptc profile.
        /// </summary>
        public IptcProfile IptcProfile { get; set; }

        /// <inheritdoc/>
        public ImageFrameMetadata DeepClone() => new(this);

        /// <summary>
        /// Gets the metadata value associated with the specified key.
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
            if (this.formatMetadata.TryGetValue(key, out IDeepCloneable meta))
            {
                return (TFormatFrameMetadata)meta;
            }

            TFormatFrameMetadata newMeta = key.CreateDefaultFormatFrameMetadata();
            this.formatMetadata[key] = newMeta;
            return newMeta;
        }
    }
}
