// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff
{
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
                return TiffFormat.Instance;
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