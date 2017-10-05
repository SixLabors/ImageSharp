// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
using System;

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
        /// Store the private fields as a Microsoft Windows <see cref="WinInfoHeaderV5"/> DIB header.
        /// </summary>
        private WinInfoHeaderV5 winInfoHeader;

        /// <summary>
        /// Store the private fields as an IBM OS/2 <see cref="OS2InfoHeaderV2"/> DIB header.
        /// </summary>
        private OS2InfoHeaderV2 os2InfoHeader;

        /// <summary>
        /// Gets or sets a value indicating whether if this field is <c>true</c>, an IBM OS/2 <c>OS2InfoHeaderV2</c> DIB header
        /// is being used insted of the Microsoft Windows <see cref="WinInfoHeaderV5"/> DIB header.
        /// <c>false</c>, otherwise.
        /// See <see cref="OS2InfoHeaderV2.Compression"/>.
        /// </summary>
        public bool IsOS2InfoHeaderV2 { get; set; } = false;

        /// <summary>
        /// Gets or sets if this field is diferent of <see cref="BmpOS2Compression.RGB"/>, an IBM OS/2 <c>OS2InfoHeaderV2</c> DIB header
        /// is being used insted of the Microsoft Windows <see cref="WinInfoHeaderV5"/> DIB header.
        /// See <see cref="OS2InfoHeaderV2.Compression"/>.
        /// </summary>
        public BmpOS2Compression Os2Compression
        {
            get { return (BmpOS2Compression)this.os2InfoHeader.Compression; }

            set { this.os2InfoHeader.Compression = (uint)((BmpOS2Compression)value); }
        }

        /// <summary>
        /// Gets or sets indicates the type of units used to interpret the values of the <c>OS2InfoHeaderV2.PixelsPerUnitX</c> and
        /// <c>OS2InfoHeaderV2.PixelsPerUnitY</c> fields.
        /// <para>The only valid value is 0, indicating pixels-per-meter.</para>
        /// See <see cref="OS2InfoHeaderV2.Units"/>.
        /// </summary>
        public ushort Os2Units
        {
            get
            {
                return this.os2InfoHeader.Units;
            }

            set
            {
                if (value != 0)
                {
                    this.os2InfoHeader.Units = 0;
                }
                else
                {
                    this.os2InfoHeader.Units = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets reserved for future use. Must be set to 0.
        /// See <see cref="OS2InfoHeaderV2.Reserved"/>.
        /// </summary>
        public ushort Os2Reserved
        {
            get
            {
                return this.os2InfoHeader.Reserved;
            }

            set
            {
                if (value != 0)
                {
                    this.os2InfoHeader.Reserved = 0;
                }
                else
                {
                    this.os2InfoHeader.Reserved = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets specifies how the bitmap scan lines are stored.
        /// <para>The only valid value for this field is 0,
        /// indicating that the bitmap is stored from left to right and from the bottom up,
        /// with the origin being in the lower-left corner of the display.</para>
        /// See <see cref="OS2InfoHeaderV2.Recording"/>.
        /// </summary>
        public ushort Os2Recording
        {
            get
            {
                return this.os2InfoHeader.Recording;
            }

            set
            {
                if (value != 0)
                {
                    this.os2InfoHeader.Recording = 0;
                }
                else
                {
                    this.os2InfoHeader.Recording = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets specifies the halftoning algorithm used when compressing the bitmap data.
        /// See <see cref="OS2InfoHeaderV2.Rendering"/>.
        /// </summary>
        public BmpOS2CompressionHalftoning Os2Rendering
        {
            get { return (BmpOS2CompressionHalftoning)this.os2InfoHeader.Rendering; }

            set { this.os2InfoHeader.Rendering = (ushort)((BmpOS2CompressionHalftoning)value); }
        }

        /// <summary>
        /// Gets or sets reserved field used only by the halftoning algorithm.
        /// See <see cref="OS2InfoHeaderV2.Size1"/>.
        /// </summary>
        public uint Os2Size1
        {
            get { return this.os2InfoHeader.Size1; }

            set { this.os2InfoHeader.Size1 = value; }
        }

        /// <summary>
        /// Gets or sets reserved field used only by the halftoning algorithm.
        /// See <see cref="OS2InfoHeaderV2.Size2"/>.
        /// </summary>
        public uint Os2Size2
        {
            get { return this.os2InfoHeader.Size2; }

            set { this.os2InfoHeader.Size2 = value; }
        }

        /// <summary>
        /// Gets or sets color model used to describe the bitmap data.
        /// <para>The only valid value is 0, indicating the RGB encoding scheme.</para>
        /// <see cref="OS2InfoHeaderV2.ColorEncoding"/>
        /// </summary>
        public uint Os2ColorEncoding
        {
            get
            {
                return this.os2InfoHeader.ColorEncoding;
            }

            set
            {
                if (value != 0)
                {
                    this.os2InfoHeader.ColorEncoding = 0;
                }
                else
                {
                    this.os2InfoHeader.ColorEncoding = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets reserved for application use and may contain an application-specific value.
        /// <para>Normally is set to 0.</para>
        /// <see cref="OS2InfoHeaderV2.Identifier"/>
        /// </summary>
        public uint Os2Identifier
        {
            get { return this.os2InfoHeader.Identifier; }

            set { this.os2InfoHeader.Identifier = value; }
        }

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
        /// Gets or sets the size of this header (aka version).
        /// <see cref="WinInfoHeaderV5.Size"/>
        /// </summary>
        public uint HeaderSize
        {
            get { return this.winInfoHeader.Size; }

            set { this.winInfoHeader.Size = value; }
        }

        /// <summary>
        /// Gets or sets the bitmap width in pixels.
        /// <see cref="WinInfoHeaderV5.Width"/>
        /// </summary>
        public int Width
        {
            get { return this.winInfoHeader.Width; }

            set { this.winInfoHeader.Width = value; }
        }

        /// <summary>
        /// Gets or sets the bitmap height in pixels.
        /// <see cref="WinInfoHeaderV5.Height"/>
        /// </summary>
        public int Height
        {
            get
            {
                return this.winInfoHeader.Height;
            }

            set
            {
                if (value < 0)
                {
                    this.winInfoHeader.Height = -value;
                    this.IsTopDown = true;
                }
                else
                {
                    this.winInfoHeader.Height = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of color planes being used. Must be set to 1.
        /// <see cref="WinInfoHeaderV5.Planes"/>
        /// </summary>
        public ushort Planes
        {
            get
            {
                return this.winInfoHeader.Planes;
            }

            set
            {
                if (value != 1)
                {
                    this.winInfoHeader.Planes = 1;
                }
                else
                {
                    this.winInfoHeader.Planes = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of bpp (bits per pixel), which is the color depth of the image.
        /// <see cref="WinInfoHeaderV5.BitsPerPixel"/>
        /// </summary>
        public ushort BitsPerPixel
        {
            get { return this.winInfoHeader.BitsPerPixel; }

            set { this.winInfoHeader.BitsPerPixel = value; }
        }

        /// <summary>
        /// Gets or sets the compression method being used.
        /// <see cref="WinInfoHeaderV5.Compression"/>
        /// </summary>
        public BmpCompression Compression
        {
            get
            {
                return (BmpCompression)this.winInfoHeader.Compression;
            }

            set
            {
                this.winInfoHeader.Compression = (uint)value;
                if ((BmpDecoderCore.SourcePreRotateMask & (uint)this.winInfoHeader.Compression) == BmpDecoderCore.SourcePreRotateMask)
                {
                    this.IsSourcePreRotate = true;
                    this.winInfoHeader.Compression = this.winInfoHeader.Compression & (~BmpDecoderCore.SourcePreRotateMask);
                }
            }
        }

        /// <summary>
        /// Gets or sets the image size. This is the size in bytes of the raw bitmap data.
        /// <see cref="WinInfoHeaderV5.ImageDataSize"/>
        /// </summary>
        public uint ImageSize
        {
            get { return this.winInfoHeader.ImageDataSize; }

            set { this.winInfoHeader.ImageDataSize = value; }
        }

        /// <summary>
        /// Gets or sets the horizontal resolution of the image in ppm (pixels per meter).
        /// <see cref="WinInfoHeaderV5.PixelsPerMeterX"/>
        /// </summary>
        public int XPelsPerMeter
        {
            get { return this.winInfoHeader.PixelsPerMeterX; }

            set { this.winInfoHeader.PixelsPerMeterX = value; }
        }

        /// <summary>
        /// Gets or sets the vertical resolution of the image in ppm (pixels per meter).
        /// <see cref="WinInfoHeaderV5.PixelsPerMeterY"/>
        /// </summary>
        public int YPelsPerMeter
        {
            get { return this.winInfoHeader.PixelsPerMeterY; }

            set { this.winInfoHeader.PixelsPerMeterY = value; }
        }

        /// <summary>
        /// Gets or sets the number of colors in the color palette,
        /// or 0 to default to 2^bpp.
        /// <see cref="WinInfoHeaderV5.PaletteSize"/>
        /// </summary>
        public uint ClrUsed
        {
            get
            {
                if (this.winInfoHeader.PaletteSize == 0)
                {
                    return (uint)this.ComputePaletteMaxNumEntries();
                }

                return this.winInfoHeader.PaletteSize;
            }

            set
            {
                this.winInfoHeader.PaletteSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of important colors used,
        /// or 0 when every color is important.
        /// <see cref="WinInfoHeaderV5.PaletteImportant"/>
        /// </summary>
        public uint ClrImportant
        {
            get
            {
                if ((this.winInfoHeader.PaletteImportant == 0) || (this.winInfoHeader.PaletteImportant > this.ClrUsed))
                {
                    return this.ClrUsed;
                }

                return this.winInfoHeader.PaletteImportant;
            }

            set
            {
                this.winInfoHeader.PaletteImportant = value;
            }
        }

        /// <summary>
        /// Gets or sets the red color mask that specifies the red component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint RedMask
        {
            get { return this.winInfoHeader.RedMask; }

            set { this.winInfoHeader.RedMask = value; }
        }

        /// <summary>
        /// Gets or sets the green color mask that specifies the green component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint GreenMask
        {
            get { return this.winInfoHeader.GreenMask; }

            set { this.winInfoHeader.GreenMask = value; }
        }

        /// <summary>
        /// Gets or sets the blue color mask that specifies the blue component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint BlueMask
        {
            get { return this.winInfoHeader.BlueMask; }

            set { this.winInfoHeader.BlueMask = value; }
        }

        /// <summary>
        /// Gets or sets the alpha (transparency) mask that specifies the transparency component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint AlphaMask
        {
            get { return this.winInfoHeader.AlphaMask; }

            set { this.winInfoHeader.AlphaMask = value; }
        }

        /// <summary>
        /// Returns the computed size of the palette entries stored on the file.
        /// </summary>
        public int ComputePaletteMaxNumEntries()
        {
            int maxPaletteEntries = 0;
            if (this.BitsPerPixel <= (ushort)BmpBitsPerPixel.Palette256)
            {
                maxPaletteEntries = (int)(1 << this.BitsPerPixel);
            }

            return maxPaletteEntries;
        }

        /// <summary>
        /// Returns the computed size of the palette entries stored on the file.
        /// </summary>
        public int ComputePaletteStorageSize(long pixelsOffset)
        {
            int presentPaletteEntries = 0;
            int paletteElementSize = (this.HeaderSize == (uint)BmpNativeStructuresSizes.BITMAPCOREHEADER) ? (int)BmpNativeStructuresSizes.RGBTRIPLE : (int)BmpNativeStructuresSizes.RGBQUAD;
            presentPaletteEntries = (int)(pixelsOffset - (int)BmpNativeStructuresSizes.BITMAPFILEHEADER - (int)this.HeaderSize) / paletteElementSize;
            return presentPaletteEntries;
        }

        /// <summary>
        /// Returns the computed size of the palette entries stored on the file.
        /// </summary>
        public int ComputeRowStorageSize()
        {
            int rowSize = 0;
            rowSize = (int)(((this.BitsPerPixel * this.Width) + 31) / 32) * 4;
            return rowSize;
        }
    }
}
