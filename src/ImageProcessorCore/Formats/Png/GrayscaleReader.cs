// <copyright file="GrayscaleReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Color reader for reading grayscale colors from a png file.
    /// </summary>
    internal sealed class GrayscaleReader : IColorReader
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
        public void ReadScanline<T, TP>(byte[] scanline, T[] pixels, PngHeader header)
            where T : IPackedVector<TP>, new()
            where TP : struct
        {
            int offset;

            byte[] newScanline = scanline.ToArrayByBitsLength(header.BitDepth);

            // Stored in r-> g-> b-> a order.
            if (this.useAlpha)
            {
                for (int x = 0; x < header.Width / 2; x++)
                {
                    offset = (this.row * header.Width) + x;

                    byte rgb = newScanline[x * 2];
                    byte a = newScanline[(x * 2) + 1];

                    T color = default(T);
                    color.PackBytes(rgb, rgb, rgb, a);
                    pixels[offset] = color;
                }
            }
            else
            {
                for (int x = 0; x < header.Width; x++)
                {
                    offset = (this.row * header.Width) + x;
                    byte rgb = newScanline[x];

                    T color = default(T);
                    color.PackBytes(rgb, rgb, rgb, 255);

                    pixels[offset] = color;
                }
            }

            this.row++;
        }
    }
}
