// <copyright file="TrueColorReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
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
        public void ReadScanline<T, TP>(byte[] scanline, T[] pixels, PngHeader header)
            where T : IPackedVector<TP>
            where TP : struct
        {
            int offset;

            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);

            if (this.useAlpha)
            {
                for (int x = 0; x < newScanline.Length; x += 4)
                {
                    offset = (this.row * header.Width) + (x >> 2);

                    // We want to convert to premultiplied alpha here.
                    byte r = newScanline[x];
                    byte g = newScanline[x + 1];
                    byte b = newScanline[x + 2];
                    byte a = newScanline[x + 3];

                    T color = default(T);
                    color.PackFromBytes(r, g, b, a);

                    pixels[offset] = color;
                }
            }
            else
            {
                for (int x = 0; x < newScanline.Length / 3; x++)
                {
                    offset = (this.row * header.Width) + x;
                    int pixelOffset = x * 3;

                    byte r = newScanline[pixelOffset];
                    byte g = newScanline[pixelOffset + 1];
                    byte b = newScanline[pixelOffset + 2];

                    T color = default(T);
                    color.PackFromBytes(r, g, b, 255);
                    pixels[offset] = color;
                }
            }

            this.row++;
        }
    }
}
