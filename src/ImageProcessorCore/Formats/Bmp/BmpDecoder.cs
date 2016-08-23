// <copyright file="BmpDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image decoder for generating an image out of a Windows bitmap stream.
    /// </summary>
    /// <remarks>
    /// Does not support the following formats at the moment:
    /// <list type="bullet">
    ///    <item>JPG</item>
    ///    <item>PNG</item>
    ///    <item>RLE4</item>
    ///    <item>RLE8</item>
    ///    <item>BitFields</item>
    /// </list>
    /// Formats will be supported in a later releases. We advise always
    /// to use only 24 Bit Windows bitmaps.
    /// </remarks>
    public class BmpDecoder : IImageDecoder
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        public int HeaderSize => 2;

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

            return extension.Equals("BMP", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("DIP", StringComparison.OrdinalIgnoreCase);
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
            bool isBmp = false;
            if (header.Length >= 2)
            {
                isBmp = header[0] == 0x42 && // B
                        header[1] == 0x4D;   // M
            }

            return isBmp;
        }

        /// <inheritdoc/>
        public void Decode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");

            new BmpDecoderCore().Decode(image, stream);
        }
    }
}
