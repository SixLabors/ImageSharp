// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BmpInfoHeader.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   This block of bytes tells the application detailed information
//   about the image, which will be used to display the image on
//   the screen.
//   <see href="https://en.wikipedia.org/wiki/BMP_file_format" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// This block of bytes tells the application detailed information
    /// about the image, which will be used to display the image on
    /// the screen.
    /// <see href="https://en.wikipedia.org/wiki/BMP_file_format"/>
    /// </summary>
    internal class BmpInfoHeader
    {
        /// <summary>
        /// Defines of the data structure in the bitmap file.
        /// </summary>
        public const int Size = 40;

        /// <summary>
        /// Gets or sets the size of this header (40 bytes)
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// Gets or sets the bitmap width in pixels (signed integer).
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the bitmap height in pixels (signed integer).
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the number of color planes being used. Must be set to 1.
        /// </summary>
        public short Planes { get; set; }

        /// <summary>
        /// Gets or sets the number of bits per pixel, which is the color depth of the image.
        /// Typical values are 1, 4, 8, 16, 24 and 32.
        /// </summary>
        public short BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the compression method being used.
        /// See the next table for a list of possible values.
        /// </summary>
        public BmpCompression Compression { get; set; }

        /// <summary>
        /// Gets or sets the image size. This is the size of the raw bitmap data (see below),
        /// and should not be confused with the file size.
        /// </summary>
        public int ImageSize { get; set; }

        /// <summary>
        /// Gets or sets the horizontal resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int XPelsPerMeter { get; set; }

        /// <summary>
        /// Gets or sets the vertical resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int YPelsPerMeter { get; set; }

        /// <summary>
        /// Gets or sets the number of colors in the color palette,
        /// or 0 to default to 2^n.
        /// </summary>
        public int ClrUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of important colors used,
        /// or 0 when every color is important{ get; set; } generally ignored.
        /// </summary>
        public int ClrImportant { get; set; }
    }
}
