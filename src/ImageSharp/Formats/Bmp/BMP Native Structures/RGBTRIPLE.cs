// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This represents one Microsoft Windows BMP v2 and IBM OS/2 BMP v1 color table/palette element.
    /// <para>Supported since Windows 2.0, Windows CE 2.0 and OS/2 1.0.</para>
    /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(RGBTRIPLE)</c> returns the size of 3 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd162939(v=vs.85).aspx">this MSDN link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
    internal struct RGBTRIPLE
    {
        /// <summary>
        /// Specifies the intensity of blue in the color in the range 0 to 255.
        /// </summary>
        public byte Blue;

        /// <summary>
        /// Specifies the intensity of green in the color in the range 0 to 255.
        /// </summary>
        public byte Green;

        /// <summary>
        /// Specifies the intensity of red in the color in the range 0 to 255.
        /// </summary>
        public byte Red;
    }
}