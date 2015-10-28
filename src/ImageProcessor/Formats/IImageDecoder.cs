// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IImageDecoder.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates properties and methods required for decoding an image from a stream.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System.IO;

    /// <summary>
    /// Encapsulates properties and methods required for decoding an image from a stream.
    /// </summary>
    public interface IImageDecoder
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        int HeaderSize { get; }

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="extension">The <see cref="string"/> containing the file extension.</param>
        /// <returns>
        /// True if the decoder supports the file extension; otherwise, false.
        /// </returns>
        bool IsSupportedFileExtension(string extension);

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="header">The <see cref="T:byte[]"/> containing the file header.</param>
        /// <returns>
        /// True if the decoder supports the file header; otherwise, false.
        /// </returns>
        bool IsSupportedFileFormat(byte[] header);

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        void Decode(Image image, Stream stream);
    }
}
