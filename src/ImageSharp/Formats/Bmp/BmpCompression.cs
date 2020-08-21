// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Defines the compression type of the image data
    /// in the bitmap file.
    /// </summary>
    internal enum BmpCompression : int
    {
        /// <summary>
        /// Each image row has a multiple of four elements. If the
        /// row has less elements, zeros will be added at the right side.
        /// The format depends on the number of bits, stored in the info header.
        /// If the number of bits are one, four or eight each pixel data is
        /// a index to the palette. If the number of bits are sixteen,
        /// twenty-four or thirty-two each pixel contains a color.
        /// </summary>
        RGB = 0,

        /// <summary>
        /// Two bytes are one data record. If the first byte is not zero, the
        /// next byte will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// </summary>
        RLE8 = 1,

        /// <summary>
        /// Two bytes are one data record. If the first byte is not zero, the
        /// next two half bytes will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// </summary>
        RLE4 = 2,

        /// <summary>
        /// Each image row has a multiple of four elements. If the
        /// row has less elements, zeros will be added at the right side.
        /// </summary>
        BitFields = 3,

        /// <summary>
        /// The bitmap contains a JPG image.
        /// Not supported at the moment.
        /// </summary>
        JPEG = 4,

        /// <summary>
        /// The bitmap contains a PNG image.
        /// Not supported at the moment.
        /// </summary>
        PNG = 5,

        /// <summary>
        /// Introduced with Windows CE.
        /// Specifies that the bitmap is not compressed and that the color table consists of four DWORD color
        /// masks that specify the red, green, blue, and alpha components of each pixel.
        /// </summary>
        BI_ALPHABITFIELDS = 6,

        /// <summary>
        /// OS/2 specific compression type.
        /// Similar to run length encoding of 4 and 8 bit.
        /// The only difference is that run values encoded are three bytes in size (one byte per RGB color component),
        /// rather than four or eight bits in size.
        ///
        /// Note: Because compression value of 4 is ambiguous for BI_RGB for windows and RLE24 for OS/2, the enum value is remapped
        /// to a different value, to be clearly separate from valid windows values.
        /// </summary>
        RLE24 = 100,
    }
}
