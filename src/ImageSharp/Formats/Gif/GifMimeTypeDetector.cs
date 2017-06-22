// <copyright file="PngMimeTypeDetector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Detects gif file headers
    /// </summary>
    public class GifMimeTypeDetector : IMimeTypeDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 6;

        /// <inheritdoc/>
        public string DetectMimeType(Span<byte> header)
        {
            if (this.IsSupportedFileFormat(header))
            {
                return "image/gif";
            }

            return null;
        }

        private bool IsSupportedFileFormat(Span<byte> header)
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
