// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngDecoder.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encoder for generating an image out of a png encoded stream.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encoder for generating an image out of a png encoded stream.
    /// </summary>
    /// <remarks>
    /// At the moment the following features are supported:
    /// <para>
    /// <b>Filters:</b> all filters are supported.
    /// </para>
    /// <para>
    /// <b>Pixel formats:</b>
    /// <list type="bullet">
    ///     <item>RGBA (True color) with alpha (8 bit).</item>
    ///     <item>RGB (True color) without alpha (8 bit).</item>
    ///     <item>Greyscale with alpha (8 bit).</item>
    ///     <item>Greyscale without alpha (8 bit).</item>
    ///     <item>Palette Index with alpha (8 bit).</item>
    ///     <item>Palette Index without alpha (8 bit).</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class PngDecoder : IImageDecoder
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        public int HeaderSize => 8;

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
            Guard.NotNullOrEmpty(extension, "extension");

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;

            return extension.Equals("PNG", StringComparison.OrdinalIgnoreCase);
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
            return header.Length >= 8 &&
                   header[0] == 0x89 &&
                   header[1] == 0x50 && // P
                   header[2] == 0x4E && // N
                   header[3] == 0x47 && // G
                   header[4] == 0x0D && // CR
                   header[5] == 0x0A && // LF
                   header[6] == 0x1A && // EOF
                   header[7] == 0x0A;   // LF
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public void Decode(Image image, Stream stream)
        {
            new PngDecoderCore().Decode(image, stream);
        }
    }
}
