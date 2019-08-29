// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides Tiff specific metadata information for the frame.
    /// </summary>
    public class TiffFrameMetaData : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetaData"/> class.
        /// </summary>
        public TiffFrameMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffFrameMetaData"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private TiffFrameMetaData(TiffFrameMetaData other)
        {
            for (int i = 0; i < other.TextTags.Count; i++)
            {
                this.TextTags.Add(other.TextTags[i]);
            }
        }

        public double HorizontalResolution { get; set; }

        public double VerticalResolution { get; set; }

        /// <summary>
        /// Gets the list of png text properties for storing meta information about this frame.
        /// </summary>
        public IList<TiffMetadataTag> TextTags { get; } = new List<TiffMetadataTag>();

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new TiffFrameMetaData(this);
    }
}
