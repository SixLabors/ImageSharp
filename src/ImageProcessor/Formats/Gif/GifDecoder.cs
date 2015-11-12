// <copyright file="GifDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    public class GifDecoder : IImageDecoder
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        public int HeaderSize => 6;

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="extension">The <see cref="string"/> containing the file extension.</param>
        /// <returns>
        /// True if the decoder supports the file extension; otherwise, false.
        /// </returns>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;
            return extension.Equals("GIF", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="header">The <see cref="T:byte[]"/> containing the file header.</param>
        /// <returns>
        /// True if the decoder supports the file header; otherwise, false.
        /// </returns>
        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= 6 &&
                   header[0] == 0x47 && // G
                   header[1] == 0x49 && // I
                   header[2] == 0x46 && // F
                   header[3] == 0x38 && // 8
                  (header[4] == 0x39 || header[4] == 0x37) && // 9 or 7
                   header[5] == 0x61;   // a
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public void Decode(Image image, Stream stream)
        {
            new GifDecoderCore().Decode(image, stream);
        }
    }
}
