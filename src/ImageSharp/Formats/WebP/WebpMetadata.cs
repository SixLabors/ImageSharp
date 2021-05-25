// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Provides WebP specific metadata information for the image.
    /// </summary>
    public class WebpMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
        /// </summary>
        public WebpMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private WebpMetadata(WebpMetadata other)
        {
            this.Animated = other.Animated;
            this.Format = other.Format;
        }

        /// <summary>
        /// Gets or sets the webp format used. Either lossless or lossy.
        /// </summary>
        public WebpFormatType Format { get; set; }

        /// <summary>
        /// Gets or sets all found chunk types ordered by appearance.
        /// </summary>
        public Queue<WebpChunkType> ChunkTypes { get; set; } = new Queue<WebpChunkType>();

        /// <summary>
        /// Gets or sets a value indicating whether the webp file contains an animation.
        /// </summary>
        public bool Animated { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new WebpMetadata(this);
    }
}
