// <copyright file="JpegFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates the means to encode and decode jpeg images.
    /// </summary>
    public class JpegFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string MimeType => "image/jpeg";

        /// <inheritdoc/>
        public string Extension => "jpg";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new string[] { "jpg", "jpeg", "jfif" };

        /// <inheritdoc/>
        public IImageDecoder Decoder => new JpegDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new JpegEncoder();

        /// <inheritdoc/>
        public int HeaderSize => 11;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= this.HeaderSize &&
                   (IsJfif(header) || IsExif(header) || IsJpeg(header));
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify Jfif data.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsJfif(byte[] header)
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
        private static bool IsExif(byte[] header)
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
        private static bool IsJpeg(byte[] header)
        {
            bool isJpg =
                header[0] == 0xFF && // 255
                header[1] == 0xD8; // 216

            return isJpg;
        }
    }
}
