// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Encapsulates the metadata of an image frame.
    /// </summary>
    public sealed class ImageFrameMetaData : IFrameMetaData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class.
        /// </summary>
        internal ImageFrameMetaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrameMetaData"/> class
        /// by making a copy from other metadata.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageFrameMetaData"/> to create this instance from.
        /// </param>
        internal ImageFrameMetaData(ImageFrameMetaData other)
        {
            DebugGuard.NotNull(other, nameof(other));

            this.FrameDelay = other.FrameDelay;
            this.DisposalMethod = other.DisposalMethod;
        }

        /// <inheritdoc/>
        public int FrameDelay { get; set; }

        /// <inheritdoc/>
        public DisposalMethod DisposalMethod { get; set; }

        /// <summary>
        /// Clones this ImageFrameMetaData.
        /// </summary>
        /// <returns>The cloned instance.</returns>
        public ImageFrameMetaData Clone()
        {
            return new ImageFrameMetaData(this);
        }
    }
}
