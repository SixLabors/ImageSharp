// <copyright file="GrayscaleReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

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

        /// <inheritdoc/>
        public void ReadScanline(byte[] scanline, float[] pixels, PngHeader header)
        {
            int offset;

            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);

            // We divide by 255 as we will store the colors in our floating point format.
            // Stored in r-> g-> b-> a order.
            if (this.useAlpha)
            {
                for (int x = 0; x < header.Width / 2; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;

                    // We want to convert to premultiplied alpha here.
                    float r = newScanline[x * 2] / 255f;
                    float g = newScanline[x * 2] / 255f;
                    float b = newScanline[x * 2] / 255f;
                    float a = newScanline[(x * 2) + 1] / 255f;

                    Color premultiplied = Color.FromNonPremultiplied(new Color(r, g, b, a));

                    pixels[offset] = premultiplied.R;
                    pixels[offset + 1] = premultiplied.G;
                    pixels[offset + 2] = premultiplied.B;
                    pixels[offset + 3] = premultiplied.A;
                }
            }
            else
            {
                for (int x = 0; x < header.Width; x++)
                {
                    offset = ((this.row * header.Width) + x) * 4;

                    pixels[offset] = newScanline[x] / 255f;
                    pixels[offset + 1] = newScanline[x] / 255f;
                    pixels[offset + 2] = newScanline[x] / 255f;
                    pixels[offset + 3] = 1;
                }
            }

            this.row++;
        }
    }
}
