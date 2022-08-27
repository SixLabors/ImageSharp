// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
#pragma warning disable SA1131 // Use readable conditions
            if (1 < (uint)header.Length)
#pragma warning restore SA1131 // Use readable conditions
            {
                // Signature should be between P1 and P6.
                return header[0] == P && (uint)(header[1] - Zero - 1) < (Seven - Zero - 1);
            }

            return false;
        }
    }
}
