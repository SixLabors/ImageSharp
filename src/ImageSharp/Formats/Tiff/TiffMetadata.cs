// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the image.
    /// </summary>
    public class TiffMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
        /// </summary>
        public TiffMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private TiffMetadata(TiffMetadata other)
        {
            this.ByteOrder = other.ByteOrder;
            this.BitsPerPixel = other.BitsPerPixel;
            this.XmpProfile = other.XmpProfile != null ? new byte[other.XmpProfile.Length] : null;
            other.XmpProfile?.AsSpan().CopyTo(this.XmpProfile.AsSpan());
        }

        /// <summary>
        /// Gets or sets the byte order.
        /// </summary>
        public ByteOrder ByteOrder { get; set; }

        /// <summary>
        /// Gets or sets the number of bits per pixel for the root frame.
        /// </summary>
        public TiffBitsPerPixel? BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the XMP profile.
        /// For internal use only. ImageSharp not support XMP profile.
        /// </summary>
        internal byte[] XmpProfile { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffMetadata(this);
    }
}
