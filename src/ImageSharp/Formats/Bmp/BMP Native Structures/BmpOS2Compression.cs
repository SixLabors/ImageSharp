// <copyright file="BmpOS2Compression.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// The compression method used on the IBM OS/2 BMP v2 (and later versions) image DIB or file.
    /// <para>See: https://en.wikipedia.org/wiki/BMP_file_format </para>
    /// </summary>
    internal enum BmpOS2Compression
    {
        // IBM OS/2 BMP v2

        /// <summary>
        /// Uncompressed format. The pixels are in plain None/RGBA.
        /// <para>Supported since Windows 2.0 and OS/2 1.0.</para>
        /// <para>Implemented on Windows BMP v2 and OS/2 BMP v1 format.</para>
        /// </summary>
        None = 0,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 8-bit/pixel. Only for 8 bpp bitmaps.
        /// <para>Supported since Windows 2.0 and OS/2 2.0.</para>
        /// <para>Implemented on Windows BMP v2 and OS/2 BMP v1 format.</para>
        /// </summary>
        RLE8 = 1,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 4-bit/pixel. Only for 4 bpp bitmaps.
        /// <para>Supported since Windows 2.0 and OS/2 2.0.</para>
        /// <para>Implemented on Windows BMP v2 and OS/2 BMP v1 format.</para>
        /// </summary>
        RLE4 = 2,

        /// <summary>
        /// Compressed format using Huffman 1D algorithm for monochrome images. Only for 1 bpp bitmaps.
        /// <para>Supported since OS/2 2.0.</para>
        /// <para>Implemented on OS/2 BMP v2 format.</para>
        /// </summary>
        Huffman1D = 3,

        /// <summary>
        /// Compressed format using Run-Length Encoded (RLE) 24-bit/pixel. Only for 24 bpp bitmaps.
        /// <para>Supported since OS/2 2.0.</para>
        /// <para>Implemented on OS/2 BMP v2 format.</para>
        /// </summary>
        RLE24 = 4
    }
}