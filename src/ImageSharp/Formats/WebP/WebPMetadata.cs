// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Provides WebP specific metadata information for the image.
    /// </summary>
    public class WebPMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebPMetadata"/> class.
        /// </summary>
        public WebPMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private WebPMetadata(WebPMetadata other)
        {
            this.Animated = other.Animated;
            this.Format = other.Format;
        }

        /// <summary>
        /// Gets or sets the webp format used. Either lossless or lossy.
        /// </summary>
        public WebPFormatType Format { get; set; }

        /// <summary>
        /// Gets or sets all found chunk types ordered by appearance.
        /// </summary>
        public Queue<WebPChunkType> ChunkTypes { get; set; } = new Queue<WebPChunkType>();

        /// <summary>
        /// Gets or sets a value indicating whether the webp file contains a animation.
        /// </summary>
        public bool Animated { get; set; } = false;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new WebPMetadata(this);
    }
}
