// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Detects tga file headers.
    /// </summary>
    public sealed class TgaImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 18;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return this.IsSupportedFileFormat(header) ? TgaFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= this.HeaderSize;
        }
    }
}
