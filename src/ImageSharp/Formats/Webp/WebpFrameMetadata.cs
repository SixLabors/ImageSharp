// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Provides webp specific metadata information for the image frame.
    /// </summary>
    public class WebpFrameMetadata : IDeepCloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebpFrameMetadata"/> class.
        /// </summary>
        public WebpFrameMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebpFrameMetadata"/> class.
        /// </summary>
        /// <param name="other">The metadata to create an instance from.</param>
        private WebpFrameMetadata(WebpFrameMetadata other) => this.FrameDuration = other.FrameDuration;

        /// <summary>
        /// Gets or sets the frame duration. The time to wait before displaying the next frame,
        /// in 1 millisecond units. Note the interpretation of frame duration of 0 (and often smaller and equal to  10) is implementation defined.
        /// </summary>
        public uint FrameDuration { get; set; }

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new WebpFrameMetadata(this);
    }
}
