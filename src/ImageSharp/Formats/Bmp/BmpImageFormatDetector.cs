// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Bmp
{
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
            return this.IsSupportedFileFormat(header) ? BmpFormat.Instance : null;
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