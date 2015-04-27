// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PaletteIndexReader.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A color reader for reading palette indices from the png file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// A color reader for reading palette indices from the png file.
    /// </summary>
    public sealed class PaletteIndexReader : IColorReader
    {
        /// <summary>
        /// The palette.
        /// </summary>
        private readonly byte[] palette;

        /// <summary>
        /// The alpha palette.
        /// </summary>
        private readonly byte[] paletteAlpha;

        /// <summary>
        /// The current row.
        /// </summary>
        private int row;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteIndexReader"/> class.
        /// </summary>
        /// <param name="palette">The palette as simple byte array. It will contains 3 values for each
        /// color, which represents the red-, the green- and the blue channel.</param>
        /// <param name="paletteAlpha">The alpha palette. Can be null, if the image does not have an
        /// alpha channel and can contain less entries than the number of colors in the palette.</param>
        public PaletteIndexReader(byte[] palette, byte[] paletteAlpha)
        {
            this.palette = palette;
            this.paletteAlpha = paletteAlpha;
        }

        /// <summary>
        /// Reads the specified scanline.
        /// </summary>
        /// <param name="scanline">The scanline.</param>
        /// <param name="pixels">The pixels, where the colors should be stored in RGBA format.</param>
        /// <param name="header">The header, which contains information about the png file, like
        /// the width of the image and the height.</param>
        public void ReadScanline(byte[] scanline, byte[] pixels, PngHeader header)
        {
            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);
            int offset, index;

            if (this.paletteAlpha != null && this.paletteAlpha.Length > 0)
            {
                // If the alpha palette is not null and does one or
                // more entries, this means, that the image contains and alpha
                // channel and we should try to read it.
                for (int i = 0; i < header.Width; i++)
                {
                    index = newScanline[i];

                    offset = ((this.row * header.Width) + i) * 4;

                    pixels[offset + 0] = this.palette[(index * 3) + 2];
                    pixels[offset + 1] = this.palette[(index * 3) + 1];
                    pixels[offset + 2] = this.palette[(index * 3) + 0];
                    pixels[offset + 3] = this.paletteAlpha.Length > index
                                                ? this.paletteAlpha[index]
                                                : (byte)255;
                }
            }
            else
            {
                for (int i = 0; i < header.Width; i++)
                {
                    index = newScanline[i];

                    offset = ((this.row * header.Width) + i) * 4;

                    pixels[offset + 0] = this.palette[(index * 3) + 2];
                    pixels[offset + 1] = this.palette[(index * 3) + 1];
                    pixels[offset + 2] = this.palette[(index * 3) + 0];
                    pixels[offset + 3] = 255;
                }
            }

            this.row++;
        }
    }
}
