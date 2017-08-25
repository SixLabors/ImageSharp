// <copyright file="GifDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ImageSharp.PixelFormats;

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
        /// Gets the encoding that should be used when reading comments.
        /// </summary>
        Encoding TextEncoding { get; }
    }
}
