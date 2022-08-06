// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The options for decoding png images
    /// </summary>
    internal interface IPngDecoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        bool IgnoreMetadata { get; }
    }
}
