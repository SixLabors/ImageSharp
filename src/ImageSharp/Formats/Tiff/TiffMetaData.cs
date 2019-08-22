// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the image.
    /// </summary>
    public class TiffMetaData : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffMetaData"/> class.
        /// </summary>
        public TiffMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffMetaData"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private TiffMetaData(TiffMetaData other)
        {
            for (int i = 0; i < other.TextTags.Count; i++)
            {
                this.TextTags.Add(other.TextTags[i]);
            }
        }

        /// <summary>
        /// Gets the list of png text properties for storing meta information about this image.
        /// </summary>
        public IList<TiffMetadataTag> TextTags { get; } = new List<TiffMetadataTag>();

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffMetaData(this);
    }
}
