// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public class JpegDecoder : IImageDecoder
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => JpegConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => JpegConstants.FileExtensions;

        /// <inheritdoc/>
        public int HeaderSize => 11;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(Span<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   (IsJfif(header) || IsExif(header) || IsJpeg(header));
        }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            using (JpegDecoderCore decoder = new JpegDecoderCore(configuration))
            {
                decoder.IgnoreMetadata = this.IgnoreMetadata;
                return decoder.Decode<TPixel>(stream);
            }
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify Jfif data.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsJfif(Span<byte> header)
        {
            bool isJfif =
                header[6] == 0x4A && // J
                header[7] == 0x46 && // F
                header[8] == 0x49 && // I
                header[9] == 0x46 && // F
                header[10] == 0x00;

            return isJfif;
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify EXIF data.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsExif(Span<byte> header)
        {
            bool isExif =
                header[6] == 0x45 && // E
                header[7] == 0x78 && // X
                header[8] == 0x69 && // I
                header[9] == 0x66 && // F
                header[10] == 0x00;

            return isExif;
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify Jpeg data.
        /// This is a last chance resort for jpegs that contain ICC information.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsJpeg(Span<byte> header)
        {
            bool isJpg =
                header[0] == 0xFF && // 255
                header[1] == 0xD8; // 216

            return isJpg;
        }
    }
}
