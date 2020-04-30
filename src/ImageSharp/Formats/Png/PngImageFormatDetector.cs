// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Detects png file headers
    /// </summary>
    public sealed class PngImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 8;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return this.IsSupportedFileFormat(header) ? PngFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= this.HeaderSize && BinaryPrimitives.ReadUInt64BigEndian(header) == PngConstants.HeaderValue;
        }
    }
}