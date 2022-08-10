// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image decoder options for generating an image out of a webp stream.
    /// </summary>
    internal interface IWebpDecoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the decoding mode for multi-frame images.
        /// </summary>
        FrameDecodingMode DecodingMode { get; }
    }
}
