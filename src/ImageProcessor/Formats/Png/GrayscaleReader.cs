// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GrayscaleReader.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Color reader for reading grayscale colors from a png file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Color reader for reading grayscale colors from a png file.
    /// </summary>
    public sealed class GrayscaleReader : IColorReader
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
        /// Initializes a new instance of the <see cref="GrayscaleReader"/> class.
        /// </summary>
        /// <param name="useAlpha">
        /// If set to <c>true</c> the color reader will also read the
        /// alpha channel from the scanline.
        /// </param>
        public GrayscaleReader(bool useAlpha)
        {
            this.useAlpha = useAlpha;
        }

        /// <summary>
        /// Reads the specified scanline.
        /// </summary>
        /// <param name="scanline">The scanline.</param>
        /// <param name="pixels">The pixels, where the colors should be stored in RGBA format.</param>
        /// <param name="header">
        /// The header, which contains information about the png file, like
        /// the width of the image and the height.
        /// </param>
        public void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header)
        {
            int offset;

            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);

            if (this.useAlpha)
            {
                for (int x = 0; x < header.Width / 2; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;

                    pixels[offset + 0] = newScanline[x * 2];
                    pixels[offset + 1] = newScanline[x * 2];
                    pixels[offset + 2] = newScanline[x * 2];
                    pixels[offset + 3] = newScanline[(x * 2) + 1];
                }
            }
            else
            {
                for (int x = 0; x < header.Width; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;

                    pixels[offset + 0] = newScanline[x];
                    pixels[offset + 1] = newScanline[x];
                    pixels[offset + 2] = newScanline[x];
                    pixels[offset + 3] = 255;
                }
            }

            this.row++;
        }
    }
}
