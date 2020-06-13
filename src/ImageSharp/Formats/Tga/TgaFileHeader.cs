// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// This block of bytes tells the application detailed information about the targa image.
    /// <see href="https://www.fileformat.info/format/tga/egff.htm"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct TgaFileHeader
    {
        /// <summary>
        /// Defines the size of the data structure in the targa file.
        /// </summary>
        public const int Size = TgaConstants.FileHeaderLength;

        public TgaFileHeader(
            byte idLength,
            byte colorMapType,
            TgaImageType imageType,
            short cMapStart,
            short cMapLength,
            byte cMapDepth,
            short xOffset,
            short yOffset,
            short width,
            short height,
            byte pixelDepth,
            byte imageDescriptor)
        {
            this.IdLength = idLength;
            this.ColorMapType = colorMapType;
            this.ImageType = imageType;
            this.CMapStart = cMapStart;
            this.CMapLength = cMapLength;
            this.CMapDepth = cMapDepth;
            this.XOffset = xOffset;
            this.YOffset = yOffset;
            this.Width = width;
            this.Height = height;
            this.PixelDepth = pixelDepth;
            this.ImageDescriptor = imageDescriptor;
        }

        /// <summary>
        /// Gets the id length.
        /// This field identifies the number of bytes contained in Field 6, the Image ID Field. The maximum number
        /// of characters is 255. A value of zero indicates that no Image ID field is included with the image.
        /// </summary>
        public byte IdLength { get; }

        /// <summary>
        /// Gets the color map type.
        /// This field indicates the type of color map (if any) included with the image. There are currently 2 defined
        /// values for this field:
        /// 0 - indicates that no color-map data is included with this image.
        /// 1 - indicates that a color-map is included with this image.
        /// </summary>
        public byte ColorMapType { get; }

        /// <summary>
        /// Gets the image type.
        /// The TGA File Format can be used to store Pseudo-Color, True-Color and Direct-Color images of various
        /// pixel depths.
        /// </summary>
        public TgaImageType ImageType { get; }

        /// <summary>
        /// Gets the start of the color map.
        /// This field and its sub-fields describe the color map (if any) used for the image. If the Color Map Type field
        /// is set to zero, indicating that no color map exists, then these 5 bytes should be set to zero.
        /// </summary>
        public short CMapStart { get; }

        /// <summary>
        /// Gets the total number of color map entries included.
        /// </summary>
        public short CMapLength { get; }

        /// <summary>
        /// Gets the number of bits per entry. Typically 15, 16, 24 or 32-bit values are used.
        /// </summary>
        public byte CMapDepth { get; }

        /// <summary>
        /// Gets the XOffset.
        /// These bytes specify the absolute horizontal coordinate for the lower left
        /// corner of the image as it is positioned on a display device having an
        /// origin at the lower left of the screen.
        /// </summary>
        public short XOffset { get; }

        /// <summary>
        /// Gets the YOffset.
        /// These bytes specify the absolute vertical coordinate for the lower left
        /// corner of the image as it is positioned on a display device having an
        /// origin at the lower left of the screen.
        /// </summary>
        public short YOffset { get; }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public short Width { get; }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public short Height { get; }

        /// <summary>
        /// Gets the number of bits per pixel. This number includes
        /// the Attribute or Alpha channel bits. Common values are 8, 16, 24 and
        /// 32 but other pixel depths could be used.
        /// </summary>
        public byte PixelDepth { get; }

        /// <summary>
        /// Gets the ImageDescriptor.
        /// ImageDescriptor contains two pieces of information.
        /// Bits 0 through 3 contain the number of attribute bits per pixel.
        /// Attribute bits are found only in pixels for the 16- and 32-bit flavors of the TGA format and are called alpha channel,
        /// overlay, or interrupt bits. Bits 4 and 5 contain the image origin location (coordinate 0,0) of the image.
        /// This position may be any of the four corners of the display screen.
        /// When both of these bits are set to zero, the image origin is the lower-left corner of the screen.
        /// Bits 6 and 7 of the ImageDescriptor field are unused and should be set to 0.
        /// </summary>
        public byte ImageDescriptor { get; }

        public static TgaFileHeader Parse(Span<byte> data)
        {
            return MemoryMarshal.Cast<byte, TgaFileHeader>(data)[0];
        }

        public void WriteTo(Span<byte> buffer)
        {
            ref TgaFileHeader dest = ref Unsafe.As<byte, TgaFileHeader>(ref MemoryMarshal.GetReference(buffer));

            dest = this;
        }
    }
}
