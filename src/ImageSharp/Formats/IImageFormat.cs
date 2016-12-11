// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates a supported image format, providing means to encode and decode an image.
    /// </summary>
    public interface IImageFormat
    {
        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains.
        /// </summary>
        string MimeType { get; }

        /// <summary>
        /// Gets the default file extension for this format.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Gets the supported file extensions for this format.
        /// </summary>
        /// <returns>
        /// The supported file extension.
        /// </returns>
        IEnumerable<string> SupportedExtensions { get; }

        /// <summary>
        /// Gets the image encoder for encoding an image from a stream.
        /// </summary>
        IImageEncoder Encoder { get; }

        /// <summary>
        /// Gets the image decoder for decoding an image from a stream.
        /// </summary>
        IImageDecoder Decoder { get; }
    }
}
