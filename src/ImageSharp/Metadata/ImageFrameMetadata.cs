// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Metadata
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetadata : IDeepCloneable<ImageFrameMetadata>
    {
        private readonly Dictionary<IImageFormat, IDeepCloneable> formatMetadata = new Dictionary<IImageFormat, IDeepCloneable>();

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
            this.XmpProfile = other.XmpProfile != null ? new byte[other.XmpProfile.Length] : null;
            other.XmpProfile?.AsSpan().CopyTo(this.XmpProfile.AsSpan());
        }

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets the XMP profile.
        /// </summary>
        internal byte[] XmpProfile { get; set; }

        /// <inheritdoc/>
        public ImageFrameMetadata DeepClone() => new ImageFrameMetadata(this);

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
