// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Provides Webp specific metadata information for the image.
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
            this.FileFormat = other.FileFormat;
            this.AnimationLoopCount = other.AnimationLoopCount;
        }

        /// <summary>
        /// Gets or sets the webp file format used. Either lossless or lossy.
        /// </summary>
        public WebpFileFormatType? FileFormat { get; set; }

        /// <summary>
        /// Gets or sets the loop count. The number of times to loop the animation. 0 means infinitely.
        /// </summary>
        public ushort AnimationLoopCount { get; set; } = 1;

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new WebpMetadata(this);
    }
}
