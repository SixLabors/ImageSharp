// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This represents one Microsoft Windows BMP v3 and IBM OS/2 BMP v2 color table/palette element.
    /// <para>Supported since Windows 3.0, Windows CE 1.0 and OS/2 2.0.</para>
    /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(RGBQUAD)</c> returns the size of 4 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx">this MSDN link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    internal struct RGBQUAD
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

        /// <summary>
        /// Not used on Microsoft Windows BMP v3 and IBM OS/2 BMP v2 and older versions (reserved and must be 0).
        /// </summary>
        /// <remarks>Optional Alpha color channel on Microsoft Windows BMP v4. Default is set to 0.</remarks>
        public byte Alpha;
    }
}
