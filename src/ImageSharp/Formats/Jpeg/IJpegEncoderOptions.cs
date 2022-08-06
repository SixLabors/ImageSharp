// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    internal interface IJpegEncoderOptions
    {
        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// Defaults to <value>75</value>.
        /// </summary>
        public int? Quality { get; set; }

        /// <summary>
        /// Gets or sets the component encoding mode.
        /// </summary>
        /// <remarks>
        /// Interleaved encoding mode encodes all color components in a single scan.
        /// Non-interleaved encoding mode encodes each color component in a separate scan.
        /// </remarks>
        public bool? Interleaved { get; set; }

        /// <summary>
        /// Gets or sets jpeg color for encoding.
        /// </summary>
        public JpegEncodingColor? ColorType { get; set; }
    }
}
