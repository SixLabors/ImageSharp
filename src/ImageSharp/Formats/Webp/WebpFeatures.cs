// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image features of a VP8X image.
    /// </summary>
    internal class WebpFeatures
    {
        /// <summary>
        /// Gets or sets a value indicating whether this image has an ICC Profile.
        /// </summary>
        public bool IccProfile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this image has an alpha channel.
        /// </summary>
        public bool Alpha { get; set; }

        /// <summary>
        /// Gets or sets the alpha chunk header.
        /// </summary>
        public byte AlphaChunkHeader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this image has an EXIF Profile.
        /// </summary>
        public bool ExifProfile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this image has XMP Metadata.
        /// </summary>
        public bool XmpMetaData { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this image is an animation.
        /// </summary>
        public bool Animation { get; set; }

        /// <summary>
        /// Gets or sets the animation loop count. 0 means infinitely.
        /// </summary>
        public ushort AnimationLoopCount { get; set; }

        /// <summary>
        /// Gets or sets  default background color of the animation frame canvas.
        /// This color MAY be used to fill the unused space on the canvas around the frames, as well as the transparent pixels of the first frame..
        /// </summary>
        public Color? AnimationBackgroundColor { get; set; }
    }
}
