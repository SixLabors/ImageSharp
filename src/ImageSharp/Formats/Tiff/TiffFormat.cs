// <copyright file="TiffFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates the means to encode and decode Tiff images.
    /// </summary>
    public class TiffFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string MimeType => "image/tiff";

        /// <inheritdoc/>
        public string Extension => "tif";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new string[] { "tif", "tiff" };

        /// <inheritdoc/>
        public IImageDecoder Decoder => new TiffDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new TiffEncoder();

        /// <inheritdoc/>
        public int HeaderSize => 4;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(byte[] header)
        {
            return header.Length >= this.HeaderSize &&
                   ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||  // Little-endian
                    (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A));   // Big-endian
        }
    }
}
