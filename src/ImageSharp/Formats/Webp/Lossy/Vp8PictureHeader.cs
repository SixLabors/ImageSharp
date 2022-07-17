// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8PictureHeader
    {
        /// <summary>
        /// Gets or sets the width of the image.
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// Gets or sets the Height of the image.
        /// </summary>
        public uint Height { get; set; }

        /// <summary>
        /// Gets or sets the horizontal scale.
        /// </summary>
        public sbyte XScale { get; set; }

        /// <summary>
        /// Gets or sets the vertical scale.
        /// </summary>
        public sbyte YScale { get; set; }

        /// <summary>
        /// Gets or sets the colorspace.
        /// 0 - YUV color space similar to the YCrCb color space defined in.
        /// 1 - Reserved for future use.
        /// </summary>
        public sbyte ColorSpace { get; set; }

        /// <summary>
        /// Gets or sets the clamp type.
        /// 0 - Decoders are required to clamp the reconstructed pixel values to between 0 and 255 (inclusive).
        /// 1 - Reconstructed pixel values are guaranteed to be between 0 and 255; no clamping is necessary.
        /// </summary>
        public sbyte ClampType { get; set; }
    }
}
