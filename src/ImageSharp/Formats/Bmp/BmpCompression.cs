// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// The compression method used on the Microsoft Windows BMP v2 and IBM OS/2 BMP v1 (and later versions) image DIB or file.
    /// <para>
    /// Defines how the compression type of the image data
    /// in the bitmap file.
    /// </para>
    /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
    /// </summary>
    internal enum BmpCompression
    {
        // Microsoft Windows BMP v3 and IBM OS/2 BMP v2

        /// <summary>
        /// Uncompressed format. The pixels are in plain RGB/RGBA.
        /// <para>
        /// Each image row has a multiple of four elements. If the
        /// row has less elements, zeros will be added at the right side.
        /// The format depends on the number of bits, stored in the info header.
        /// If the number of bits are one, four or eight each pixel data is
        /// a index to the palette. If the number of bits are sixteen,
        /// twenty-four or thirty-two each pixel contains a color.
        /// </para>
        /// <para>Supported by Windows 2.0 and OS/2 1.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RGB = 0,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 8-bit/pixel. Only for 8 bpp bitmaps.
        /// <para>
        /// Two bytes are one data record. If the first byte is not zero, the
        /// next byte will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// Not supported at the moment.
        /// </para>
        /// <para>Supported by Windows 2.0 and OS/2 2.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RLE8 = 1,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 4-bit/pixel. Only for 4 bpp bitmaps.
        /// <para>
        /// Two bytes are one data record. If the first byte is not zero, the
        /// next byte will be repeated as much as the value of the first byte.
        /// If the first byte is zero, the record has different meanings, depending
        /// on the second byte. If the second byte is zero, it is the end of the row,
        /// if it is one, it is the end of the image.
        /// Not supported at the moment.
        /// </para>
        /// <para>Supported by Windows 2.0 and OS/2 2.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RLE4 = 2,

        /// <summary>
        /// Uncompressed format. For Microsoft Windows BMP v3 only, the color table consists of three DWORD color bitfield masks
        /// that specify the red, green, and blue components, respectively, of each pixel.
        /// <para>
        /// Each image row has a multiple of four elements. If the
        /// row has less elements, zeros will be added at the right side.
        /// Not supported at the moment.
        /// </para>
        /// <para>This is valid when used with 16 and 32 bpp bitmaps.</para>
        /// <para>Supported by Windows 3.0/Windows NT 3.1/Windows CE 2.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v3.</para>
        /// </summary>
        BitFields = 3,

        /// <summary>
        /// The bitmap contains a JPG compressed image.
        /// <para>
        /// The bitmap contains a JPG image.
        /// Not supported at the moment.
        /// </para>
        /// <para>Supported by Windows 98/Windows 2000 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v4.</para>
        /// </summary>
        JPEG = 4,

        /// <summary>
        /// The bitmap contains a PNG compressed image.
        /// <para>
        /// The bitmap contains a PNG image.
        /// Not supported at the moment.
        /// </para>
        /// <para>Supported by Windows 98/Windows 2000 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v4.</para>
        /// </summary>
        PNG = 5,

        // Windows CE Specific

        /// <summary>
        /// Uncompressed format. For Microsoft Windows BMP v3 only, the color table consists of four DWORD color bitfield masks
        /// that specify the red, green, blue, and alpha components, respectively, of each pixel.
        /// <para>This is valid when used with 16 and 32 bpp bitmaps on Windows CE only.</para>
        /// <para>Supported by Windows CE .NET 4.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v3.</para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/aa452885.aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        AlphaBitFields = 6,

        // Windows Metafile Specific

        /// <summary>
        /// Uncompressed format using CMYK color scheme instead of RGBA.
        /// <para>Windows Metafile CMYK only.</para>
        /// </summary>
        CMYK = 11,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 8-bit/pixel. Only for 8 bpp bitmaps using CMYK color scheme instead of RGBA.
        /// <para>Windows Metafile CMYK only.</para>
        /// </summary>
        CMYK_RLE8 = 12,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 4-bit/pixel. Only for 4 bpp bitmaps using CMYK color scheme instead of RGBA.
        /// <para>Windows Metafile CMYK only.</para>
        /// </summary>
        CMYK_RLE4 = 13
    }
}
