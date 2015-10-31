// <copyright file="TrueColorReader.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Color reader for reading true colors from a png file. Only colors
    /// with 24 or 32 bit (3 or 4 bytes) per pixel are supported at the moment.
    /// </summary>
    public sealed class TrueColorReader : IColorReader
    {
        /// <summary>
        /// Whether t also read the alpha channel.
        /// </summary>
        private readonly bool useAlpha;

        /// <summary>
        /// The current row.
        /// </summary>
        private int row;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrueColorReader"/> class.
        /// </summary>
        /// <param name="useAlpha">if set to <c>true</c> the color reader will also read the
        /// alpha channel from the scanline.</param>
        public TrueColorReader(bool useAlpha)
        {
            this.useAlpha = useAlpha;
        }

        /// <inheritdoc/>
        public void ReadScanline(byte[] scanline, float[] pixels, PngHeader header)
        {
            int offset;

            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);

            if (this.useAlpha)
            {
                for (int x = 0; x < newScanline.Length; x += 4)
                {
                    offset = ((this.row * header.Width) + (x >> 2)) * 4;

                    pixels[offset + 0] = newScanline[x] / 255f;
                    pixels[offset + 1] = newScanline[x + 1] / 255f;
                    pixels[offset + 2] = newScanline[x + 2] / 255f;
                    pixels[offset + 3] = newScanline[x + 3] / 255f;
                }
            }
            else
            {
                for (int x = 0; x < newScanline.Length / 3; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;
                    int pixelOffset = x * 3;

                    pixels[offset + 0] = newScanline[pixelOffset] / 255f;
                    pixels[offset + 1] = newScanline[pixelOffset + 1] / 255f;
                    pixels[offset + 2] = newScanline[pixelOffset + 2] / 255f;
                    pixels[offset + 3] = 1;
                }
            }

            this.row++;
        }
    }
}
