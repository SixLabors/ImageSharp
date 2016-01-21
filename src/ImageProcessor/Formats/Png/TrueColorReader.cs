// <copyright file="TrueColorReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Color reader for reading true colors from a png file. Only colors
    /// with 24 or 32 bit (3 or 4 bytes) per pixel are supported at the moment.
    /// </summary>
    internal sealed class TrueColorReader : IColorReader
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

                    // We want to convert to premultiplied alpha here.
                    float r = newScanline[x] / 255f;
                    float g = newScanline[x + 1] / 255f;
                    float b = newScanline[x + 2] / 255f;
                    float a = newScanline[x + 3] / 255f;

                    Color premultiplied = Color.FromNonPremultiplied(new Color(r, g, b, a));

                    pixels[offset] = premultiplied.R;
                    pixels[offset + 1] = premultiplied.G;
                    pixels[offset + 2] = premultiplied.B;
                    pixels[offset + 3] = premultiplied.A;
                }
            }
            else
            {
                for (int x = 0; x < newScanline.Length / 3; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;
                    int pixelOffset = x * 3;

                    pixels[offset] = newScanline[pixelOffset] / 255f;
                    pixels[offset + 1] = newScanline[pixelOffset + 1] / 255f;
                    pixels[offset + 2] = newScanline[pixelOffset + 2] / 255f;
                    pixels[offset + 3] = 1;
                }
            }

            this.row++;
        }
    }
}
