// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    internal interface IGifDecoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the text encoding that should be used when reading comments.
        /// </summary>
        Encoding TextEncoding { get; }

        /// <summary>
        /// Gets the decoding mode for multi-frame images
        /// </summary>
        FrameDecodingMode DecodingMode { get; }
    }
}
