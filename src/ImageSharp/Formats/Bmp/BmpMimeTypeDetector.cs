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
    /// Detects bmp file headers
    /// </summary>
    internal class BmpMimeTypeDetector : IMimeTypeDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 2;

        /// <inheritdoc/>
        public string DetectMimeType(Span<byte> header)
        {
            if (this.IsSupportedFileFormat(header))
            {
                return "image/bmp";
            }

            return null;
        }

        private bool IsSupportedFileFormat(Span<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x42 && // B
                   header[1] == 0x4D;   // M
        }
    }
}
