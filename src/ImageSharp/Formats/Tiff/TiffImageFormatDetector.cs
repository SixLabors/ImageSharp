// <copyright file="TiffImageFormatDetector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;

    /// <summary>
    /// Detects tiff file headers
    /// </summary>
    public sealed class TiffImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 4;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            if (this.IsSupportedFileFormat(header))
            {
                return ImageFormats.Tiff;
            }

            return null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) || // Little-endian
                    (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)); // Big-endian
        }
    }
}