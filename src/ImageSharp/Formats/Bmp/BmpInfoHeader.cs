// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This block of bytes tells the application detailed information
    /// about the image, which will be used to display the image on
    /// the screen.
    /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
    /// </summary>
    internal sealed class BmpInfoHeader
    {
        /// <summary>
        /// If this field is diferent of <see cref="BmpOS2Compression.RGB"/>, an IBM OS/2 <c>OS2InfoHeaderV2</c> DIB header
        /// is being used insted of the Microsoft Windows <see cref="WinInfoHeaderV5"/> DIB header.
        /// See <see cref="OS2InfoHeaderV2.Compression"/>.
        /// </summary>
        public BmpOS2Compression OS2Compression = BmpOS2Compression.RGB;

        /// <summary>
        /// Store the private fields as a Microsoft Windows <see cref="WinInfoHeaderV5"/> DIB header.
        /// </summary>
        private WinInfoHeaderV5 infoHeader;

        /// <summary>
        /// Gets or sets a value indicating whether this DIB header is stored in top-down (<c>true</c>)
        /// or bottom-down (<c>false</c>).
        /// <see cref="Height"/>
        /// </summary>
        public bool IsTopDown { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this DIB header stored is pre-rotated 90º referent to the
        /// GUI (<c>true</c>). If the picture has taken with the GUI and the camera having the same orientantion
        /// (Landscape/Portrait), this value is <c>false</c>;
        /// or bottom-down (<c>false</c>).
        /// <see cref="BmpDecoderCore.SourcePreRotateMask"/>
        /// </summary>
        public bool IsSourcePreRotate { get; set; } = false;

        /// <summary>
        /// Gets or sets the size of this header
        /// </summary>
        public uint HeaderSize {
            get { return this.infoHeader.Size; }

            set { this.infoHeader.Size = value; }
        }

        /// <summary>
        /// Gets or sets the bitmap width in pixels (signed integer).
        /// </summary>
        public int Width
        {
            get { return this.infoHeader.Width; }

            set { this.infoHeader.Width = value; }
        }

        /// <summary>
        /// Gets or sets the bitmap height in pixels (signed integer).
        /// </summary>
        public int Height
        {
            get { return this.infoHeader.Height; }

            set { this.infoHeader.Height = value; }
        }

        /// <summary>
        /// Gets or sets the number of color planes being used. Must be set to 1.
        /// </summary>
        public ushort Planes
        {
            get { return this.infoHeader.Planes; }

            set { this.infoHeader.Planes = value; }
        }

        /// <summary>
        /// Gets or sets the number of bits per pixel, which is the color depth of the image.
        /// Typical values are 1, 4, 8, 16, 24 and 32.
        /// </summary>
        public ushort BitsPerPixel
        {
            get { return this.infoHeader.BitsPerPixel; }

            set { this.infoHeader.BitsPerPixel = value; }
        }

        /// <summary>
        /// Gets or sets the compression method being used.
        /// See the next table for a list of possible values.
        /// </summary>
        public BmpCompression Compression
        {
            get { return (BmpCompression)this.infoHeader.Compression; }

            set { this.infoHeader.Compression = (uint)value; }
        }

        /// <summary>
        /// Gets or sets the image size. This is the size of the raw bitmap data (see below),
        /// and should not be confused with the file size.
        /// </summary>
        public uint ImageSize
        {
            get { return this.infoHeader.ImageSize; }

            set { this.infoHeader.ImageSize = value; }
        }

        /// <summary>
        /// Gets or sets the horizontal resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int XPelsPerMeter
        {
            get { return this.infoHeader.PixelsPerMeterX; }

            set { this.infoHeader.PixelsPerMeterX = value; }
        }

        /// <summary>
        /// Gets or sets the vertical resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int YPelsPerMeter
        {
            get { return this.infoHeader.PixelsPerMeterY; }

            set { this.infoHeader.PixelsPerMeterY = value; }
        }

        /// <summary>
        /// Gets or sets the number of colors in the color palette,
        /// or 0 to default to 2^n.
        /// </summary>
        public uint ClrUsed
        {
            get { return this.infoHeader.PaletteSize; }

            set { this.infoHeader.PaletteSize = value; }
        }

        /// <summary>
        /// Gets or sets the number of important colors used,
        /// or 0 when every color is important{ get; set; } generally ignored.
        /// </summary>
        public uint ClrImportant
        {
            get { return this.infoHeader.PaletteImportant; }

            set { this.infoHeader.PaletteImportant = value; }
        }
    }
}
