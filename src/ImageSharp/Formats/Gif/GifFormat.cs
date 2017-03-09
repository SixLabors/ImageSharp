// <copyright file="GifFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates the means to encode and decode gif images.
    /// </summary>
    public class GifFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Extension => "gif";

        /// <inheritdoc/>
        public string MimeType => "image/gif";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new string[] { "gif" };

        /// <inheritdoc/>
        public IImageDecoder Decoder => new GifDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new GifEncoder();

        /// <inheritdoc/>
        public int HeaderSize => 6;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x47 && // G
                   header[1] == 0x49 && // I
                   header[2] == 0x46 && // F
                   header[3] == 0x38 && // 8
                  (header[4] == 0x39 || header[4] == 0x37) && // 9 or 7
                   header[5] == 0x61;   // a
        }
    }
}
