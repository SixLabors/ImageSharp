// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new WebPMetadata(this);

        /// <summary>
        /// The webp format used. Either lossless or lossy.
        /// </summary>
        public WebPFormatType Format { get; set; }

        /// <summary>
        /// Indicates, if the webp file contains a animation.
        /// </summary>
        public bool Animated { get; set; } = false;
    }
}
