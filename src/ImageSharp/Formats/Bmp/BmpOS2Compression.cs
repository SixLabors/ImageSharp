// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// The compression method used on the IBM OS/2 BMP v2 (and later versions) image DIB or file.
    /// <para>Supported since IBM OS/2 2.0.</para>
    /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
    /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
    /// </summary>
    internal enum BmpOS2Compression : uint
    {
        // IBM OS/2 BMP v2

        /// <summary>
        /// Uncompressed format. The pixels are in plain RGB/RGBA.
        /// <para>Supported since Windows 2.0 and OS/2 1.0.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RGB = 0,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 8-bit/pixel. Only for 8 bpp bitmaps.
        /// <para>Supported since Windows 2.0 and OS/2 2.0.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RLE8 = 1,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 4-bit/pixel. Only for 4 bpp bitmaps.
        /// <para>Supported since Windows 2.0 and OS/2 2.0.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RLE4 = 2,

        /// <summary>
        /// Compressed format using Huffman 1D algorithm for monochrome images. Only for 1 bpp bitmaps.
        /// <para>Supported since OS/2 2.0.</para>
        /// <para>Implemented on IBM OS/2 BMP v2 format.</para>
        /// </summary>
        Huffman1D = 3,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 24-bit/pixel. Only for 24 bpp bitmaps.
        /// <para>Supported since OS/2 2.0.</para>
        /// <para>Implemented on IBM OS/2 BMP v2 format.</para>
        /// </summary>
        RLE24 = 4
    }
}