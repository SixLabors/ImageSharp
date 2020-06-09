// Copyright (c) Six Labors.
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
            if (header.Length >= this.HeaderSize)
            {
                short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(header);
                return fileTypeMarker == BmpConstants.TypeMarkers.Bitmap || fileTypeMarker == BmpConstants.TypeMarkers.BitmapArray;
            }

            return false;
        }
    }
}
