// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates a supported image format, providing means to encode and decode an image.
    /// Individual formats implements in this interface must be registered in the <see cref="Configuration"/>
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

        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        int HeaderSize { get; }

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="header">The <see cref="T:byte[]"/> containing the file header.</param>
        /// <returns>
        /// True if the decoder supports the file header; otherwise, false.
        /// </returns>
        bool IsSupportedFileFormat(byte[] header);
    }
}
