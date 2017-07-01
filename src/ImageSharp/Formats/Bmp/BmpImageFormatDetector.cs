// <copyright file="BmpImageFormatDetector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;

    /// <summary>
    /// Detects bmp file headers
    /// </summary>
    public sealed class BmpImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 2;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            if (this.IsSupportedFileFormat(header))
            {
                return ImageFormats.Bitmap;
            }

            return null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            // TODO: This should be in constants
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x42 && // B
                   header[1] == 0x4D;   // M
        }
    }
}