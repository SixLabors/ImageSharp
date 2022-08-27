// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Encapsulates the options for the <see cref="TiffDecoder"/>.
    /// </summary>
    internal interface ITiffDecoderOptions
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
