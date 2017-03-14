// <copyright file="PngFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates the means to encode and decode png images.
    /// </summary>
    public class PngFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string MimeType => "image/png";

        /// <inheritdoc/>
        public string Extension => "png";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new string[] { "png" };

        /// <inheritdoc/>
        public IImageDecoder Decoder => new PngDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new PngEncoder();

        /// <inheritdoc/>
        public int HeaderSize => 8;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x89 &&
                   header[1] == 0x50 && // P
                   header[2] == 0x4E && // N
                   header[3] == 0x47 && // G
                   header[4] == 0x0D && // CR
                   header[5] == 0x0A && // LF
                   header[6] == 0x1A && // EOF
                   header[7] == 0x0A;   // LF
        }
    }
}
