// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Detects tiff file headers
    /// </summary>
    public sealed class TiffImageFormatDetector : IImageFormatDetector
    {
        private readonly bool isBigTiff;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffImageFormatDetector"/> class.
        /// </summary>
        /// <param name="isBigTiff">if set to <c>true</c> is BigTiff.</param>
        public TiffImageFormatDetector(bool isBigTiff) => this.isBigTiff = isBigTiff;

        /// <inheritdoc/>
        public int HeaderSize => this.isBigTiff ? 8 : 4;

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
                    if (this.isBigTiff is false)
                    {
                        if (header[2] == 0x2A && header[3] == 0x00)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (header[2] == 0x2B && header[3] == 0x00
                            && header[4] == 8 && header[5] == 0 && header[6] == 0 && header[7] == 0)
                        {
                            return true;
                        }
                    }
                }
                else if (header[0] == 0x4D && header[1] == 0x4D)
                {
                    // Big-endian
                    if (this.isBigTiff is false)
                    {
                        if (header[2] == 0 && header[3] == 0x2A)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (header[2] == 0 && header[3] == 0x2B
                            && header[4] == 0 && header[5] == 8 && header[6] == 0 && header[7] == 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
