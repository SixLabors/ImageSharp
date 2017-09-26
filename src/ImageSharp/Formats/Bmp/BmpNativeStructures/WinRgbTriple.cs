// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This represents one Microsoft Windows BMP v2 and IBM OS/2 BMP v1 color table/palette element: RGBTRIPLE.
    /// <para>Supported since Windows 2.0, Windows CE 2.0 and OS/2 1.0.</para>
    /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
    /// <seealso href="https://msdn.microsoft.com/en-us/library/dd162939(v=vs.85).aspx">See this MSDN link for more information.</seealso>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(WinRgbTriple)</c> returns the size of 3 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
    internal struct WinRgbTriple
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