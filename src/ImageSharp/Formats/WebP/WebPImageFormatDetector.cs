// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Detects WebP file headers
    /// </summary>
    public sealed class WebPImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc />
        public int HeaderSize => 12;

        /// <inheritdoc />
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return this.IsSupportedFileFormat(header) ? WebPFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   this.IsRiffContainer(header) &&
                   this.IsWebPFile(header);
        }

        private bool IsRiffContainer(ReadOnlySpan<byte> header)
        {
            return header[0] == 0x52 && // R
                   header[1] == 0x49 && // I
                   header[2] == 0x46 && // F
                   header[3] == 0x46; // F
        }

        private bool IsWebPFile(ReadOnlySpan<byte> header)
        {
            return header[8] == 0x57 && // W
                   header[9] == 0x45 && // E
                   header[10] == 0x42 && // B
                   header[11] == 0x50; // P
        }
    }
}
