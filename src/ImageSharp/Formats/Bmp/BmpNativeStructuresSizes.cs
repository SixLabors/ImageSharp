// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This enumeration contains the sizes of the several native structures used by the BMP file format.
    /// </summary>
    internal enum BmpNativeStructuresSizes
    {
        // IBM OS/2 BMP palette element

        /// <summary>
        /// The size in bytes of a color palette element for Microsoft Windows BMP v2 and IBM OS/2 BMP v1 file format.
        /// <para>
        /// Supported since OS/2 1.0 (same format as Windows 2.0).
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// <seealso cref="BmpNativeStructuresSizes.RGBTRIPLE"/>
        /// </summary>
        OS21XPALETTEELEMENT = 3,

        /// <summary>
        /// The size in bytes of a color palette element for Microsoft Windows BMP v3 and IBM OS/2 BMP v2 file format.
        /// <para>
        /// Supported since OS/2 2.0 (same format as Windows 3.1x).
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// <seealso cref="BmpNativeStructuresSizes.RGBQUAD"/>
        /// </summary>
        OS22XPALETTEELEMENT = 4,

        // IBM OS/2 BMP DIB header

        /// <summary>
        /// The size in bytes for the bitmap information header of Microsoft Windows BMP v2 and IBM OS/2 BMP v1 file format.
        /// <para>
        /// Supported since OS/2 1.0 (same format as Windows 2.0).
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// <seealso cref="BmpNativeStructuresSizes.BITMAPCOREHEADER"/>
        /// </summary>
        OS21XBITMAPHEADER = 12,

        /// <summary>
        /// The minimum size in bytes for the bitmap information header of IBM OS/2 BMP v2 file format.
        /// <para>
        /// Supported since OS/2 2.0.
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// </summary>
        OS22XBITMAPHEADER_MIN = 16,

        /// <summary>
        /// The maximum size in bytes for the bitmap information header of IBM OS/2 BMP v2 file format.
        /// <para>
        /// Supported since OS/2 2.0.
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// </summary>
        OS22XBITMAPHEADER = 64,

        /// <summary>
        /// The maximum size in bytes for the bitmap information header of IBM OS/2 BMP v2 file format.
        /// <para>
        /// Supported since OS/2 2.0.
        /// </para>
        /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
        /// </summary>
        OS22XBITMAPHEADER_MAX = OS22XBITMAPHEADER,

        // Microsoft Windows BMP palette element

        /// <summary>
        /// The size in bytes of a color palette element for Microsoft Windows BMP v2 and IBM OS/2 BMP v1 file format.
        /// <para>
        /// Supported since Windows 2.0 (same format as OS/2 1.0).
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd162939(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        RGBTRIPLE = 3,

        /// <summary>
        /// The size in bytes of a color palette element for Microsoft Windows BMP v3 and IBM OS/2 BMP v2 file format.
        /// <para>
        /// Supported since Windows 3.0 (same format as OS/2 2.0).
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        RGBQUAD = 4,

        // Microsoft Windows BMP and IBM OS/2 file header

        /// <summary>
        /// The size in bytes for the file header of Microsoft Windows BMP v2 and IBM OS/2 BMP v1 file format.
        /// <para>
        /// Supported since Windows 2.0 and OS/2 1.0.
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        BITMAPFILEHEADER = 14,

        // Microsoft Windows BMP DIB header

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v2 and IBM OS/2 BMP v1 file format.
        /// <para>
        /// Supported since Window 2.0x (same format as OS/2 1.0).
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183372(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        BITMAPCOREHEADER = 12,

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v3 file format.
        /// <para>
        /// Supported since Window 3.0x, Windows NT 3.1.
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183376(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        BITMAPINFOHEADER = 40,

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v3 file format width Windows NT 4 RGB bitfields extension.
        /// <para>
        /// Supported since Windows NT 3.1.
        /// </para>
        /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
        /// </summary>
        BITMAPINFOHEADER_NT = 52,

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v3 file format width Windows CE RGBA bitfields extension.
        /// <para>
        /// Supported since Windows CE 5.0 with .NET 4.0.
        /// </para>
        /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
        /// </summary>
        BITMAPINFOHEADER_CE = 56,

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v4 file format.
        /// <para>
        /// Supported since Window 95, Windows NT 4.
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183380(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        BITMAPV4HEADER = 108,

        /// <summary>
        /// The size in bytes for the bitmap header of Microsoft Windows BMP v5 file format.
        /// <para>
        /// Supported since Window 98, Windows 2000.
        /// </para>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        BITMAPV5HEADER = 124
    }
}
