// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Detects Pbm file headers.
    /// </summary>
    public sealed class PbmImageFormatDetector : IImageFormatDetector
    {
        private const byte P = (byte)'P';
        private const byte Zero = (byte)'0';
        private const byte Seven = (byte)'7';

        /// <inheritdoc/>
        public int HeaderSize => 2;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header) => this.IsSupportedFileFormat(header) ? PbmFormat.Instance : null;

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            if (header.Length >= this.HeaderSize)
            {
                return header[0] == P && header[1] > Zero && header[1] < Seven;
            }

            return false;
        }
    }
}
