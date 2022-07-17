// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Detects tiff file headers
    /// </summary>
    public sealed class TiffImageFormatDetector : IImageFormatDetector
    {
        /// <inheritdoc/>
        public int HeaderSize => 8;

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
            if (header.Length >= this.HeaderSize)
            {
                if (header[0] == 0x49 && header[1] == 0x49)
                {
                    // Little-endian
                    if (header[2] == 0x2A && header[3] == 0x00)
                    {
                        // tiff
                        return true;
                    }
                    else if (header[2] == 0x2B && header[3] == 0x00
                         && header[4] == 8 && header[5] == 0 && header[6] == 0 && header[7] == 0)
                    {
                        // big tiff
                        return true;
                    }
                }
                else if (header[0] == 0x4D && header[1] == 0x4D)
                {
                    // Big-endian
                    if (header[2] == 0 && header[3] == 0x2A)
                    {
                        // tiff
                        return true;
                    }
                    else
                    if (header[2] == 0 && header[3] == 0x2B
                        && header[4] == 0 && header[5] == 8 && header[6] == 0 && header[7] == 0)
                    {
                        // big tiff
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
