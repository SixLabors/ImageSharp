// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The optioas for decoding png images
    /// </summary>
    internal interface IPngDecoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the encoding that should be used when reading text chunks.
        /// </summary>
        Encoding TextEncoding { get; }
    }
}