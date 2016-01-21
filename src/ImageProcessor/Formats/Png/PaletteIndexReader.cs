// <copyright file="PaletteIndexReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// A color reader for reading palette indices from the png file.
    /// </summary>
    internal sealed class PaletteIndexReader : IColorReader
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

        /// <inheritdoc/>
        public void ReadScanline(byte[] scanline, float[] pixels, PngHeader header)
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
                    int pixelOffset = index * 3;
                    
                    float r = newScanline[pixelOffset] / 255f;
                    float g = newScanline[pixelOffset + 1] / 255f;
                    float b = newScanline[pixelOffset + 2] / 255f;
                    float a = this.paletteAlpha.Length > index
                                            ? this.paletteAlpha[index] / 255f
                                            : 1;

                    Color color = new Color(r, g, b, a);
                    if (color.A < 1)
                    {
                        // We want to convert to premultiplied alpha here.
                        color = Color.FromNonPremultiplied(color);
                    }
                    
                    pixels[offset] = color.R;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.B;
                    pixels[offset + 3] = color.A;
                }
            }
            else
            {
                for (int i = 0; i < header.Width; i++)
                {
                    index = newScanline[i];

                    offset = ((this.row * header.Width) + i) * 4;
                    int pixelOffset = index * 3;

                    pixels[offset] = this.palette[pixelOffset] / 255f;
                    pixels[offset + 1] = this.palette[pixelOffset + 1] / 255f;
                    pixels[offset + 2] = this.palette[pixelOffset + 2] / 255f;
                    pixels[offset + 3] = 1;
                }
            }

            this.row++;
        }
    }
}
