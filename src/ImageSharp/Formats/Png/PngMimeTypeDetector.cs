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
    /// Detects png file headers
    /// </summary>
    public class PngMimeTypeDetector : IMimeTypeDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 8;

        /// <inheritdoc/>
        public string DetectMimeType(Span<byte> header)
        {
            if (this.IsSupportedFileFormat(header))
            {
                return "image/png";
            }

            return null;
        }

        private bool IsSupportedFileFormat(Span<byte> header)
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
