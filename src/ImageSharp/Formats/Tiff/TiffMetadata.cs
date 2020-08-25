// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.Collections.Generic;

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
            this.XmpProfile = other.XmpProfile;
        }

        /// <summary>
        /// Gets or sets the byte order.
        /// </summary>
        public TiffByteOrder ByteOrder { get; set; }

        /// <summary>
        /// Gets or sets the XMP profile.
        /// </summary>
        public byte[] XmpProfile { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffMetadata(this);
    }
}
