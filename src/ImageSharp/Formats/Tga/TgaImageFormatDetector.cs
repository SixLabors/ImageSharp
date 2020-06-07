// Copyright (c) Six Labors.
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
        public int HeaderSize => 16;

        /// <inheritdoc/>
        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return this.IsSupportedFileFormat(header) ? TgaFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            if (header.Length >= this.HeaderSize)
            {
                // There are no magic bytes in the first few bytes of a tga file,
                // so we try to figure out if its a valid tga by checking for valid tga header bytes.

                // The color map type should be either 0 or 1, other values are not valid.
                if (header[1] != 0 && header[1] != 1)
                {
                    return false;
                }

                // The third byte is the image type.
                var imageType = (TgaImageType)header[2];
                if (!imageType.IsValid())
                {
                    return false;
                }

                // If the color map typ is zero, all bytes of the color map specification should also be zeros.
                if (header[1] == 0)
                {
                    if (header[3] != 0 || header[4] != 0 || header[5] != 0 || header[6] != 0 || header[7] != 0)
                    {
                        return false;
                    }
                }

                // The height or the width of the image should not be zero.
                if ((header[12] == 0 && header[13] == 0) || (header[14] == 0 && header[15] == 0))
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
