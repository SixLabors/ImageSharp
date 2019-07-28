// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Detects bmp file headers.
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
            short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(header);
            return header.Length >= this.HeaderSize &&
                   (fileTypeMarker == BmpConstants.TypeMarkers.Bitmap || fileTypeMarker == BmpConstants.TypeMarkers.BitmapArray);
        }
    }
}
