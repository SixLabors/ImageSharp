// <copyright file="BmpBitsPerPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumerates the available bits per pixel for bitmap.
    /// </summary>
    public enum BmpBitsPerPixel_Original
    {
        /// <summary>
        /// 24 bits per pixel. Each pixel consists of 3 bytes.
        /// </summary>
        Pixel24 = 3,

        /// <summary>
        /// 32 bits per pixel. Each pixel consists of 4 bytes.
        /// </summary>
        Pixel32 = 4,
    }

    /// <summary>
    /// The number of bits-per-pixel (bpp) used in the Microsoft Windows BMP and IBM OS/2 BMP image DIB or file.
    /// </summary>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">this link</a> for more information.
    public enum BmpBitsPerPixel
    {
        // Microsoft Windows BMP v2 and IBM OS/2 BMP v1

        /// <summary>
        /// The bitmap is monochrome (1 bpp), and the palette contains 2 entries (2 colors palette).
        /// <para>Each bit in the bitmap array represents a pixel.
        /// If the bit is clear, the pixel is displayed with the color of the first entry in the palette table;
        /// if the bit is set, the pixel has the color of the second entry in the table.</para>
        /// <para>Supported by Windows 2.0x and OS/2 1.0x or later.</para>
        /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
        /// </summary>
        MonoChrome = 1,

        /// <summary>
        /// The bitmap has a maximum of 4 colors, and the palette contains up to 4 entries (4 colors palette).
        /// <para>Each 2 bits in the bitmap array represents a pixel (the index for the palette).
        /// For example, if the first byte in the bitmap is 0x1F, the byte represents 4 pixels.
        /// The first pixel contains the color in the second table entry, the second pixel will be the color in
        /// the sixteenth table entry.</para>
        /// <para>Supported by Windows CE 1.0x or later.</para>
        /// <para>From Windows BMP v3.</para>
        /// </summary>
        Palette4 = 2,

        /// <summary>
        /// The bitmap has a maximum of 16 colors, and the palette contains up to 16 entries (16 colors palette).
        /// <para>Each 4 bits in the bitmap array represents a pixel (the index for the palette).
        /// For example, if the first byte in the bitmap is 0x1F, the byte represents two pixels.
        /// The first pixel contains the color in the second table entry, and the second pixel contains the
        /// color in the sixteenth table entry.</para>
        /// <para>Supported by Windows 2.0x and OS/2 1.0x or later.</para>
        /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
        /// </summary>
        Palette16 = 4,

        /// <summary>
        /// The bitmap has a maximum of 256 colors, and the palette contains up to 256 entries (256 colors palette).
        /// <para>Each 8 bits (1 byte) in the bitmap array represents a pixel (the index for the palette).</para>
        /// <para>Supported by Windows 2.0x and OS/2 1.0x or later.</para>
        /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
        /// </summary>
        Palette256 = 8,

        /// <summary>
        /// The bitmap has a maximum of 2^24 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each pixel is made by 3 bytes that specifies the relative intensities of blue, green, and red color components respectively.</para>
        /// <para>Supported by Windows 2.0x and OS/2 1.0x or later.</para>
        /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
        /// </summary>
        RGB24 = 24,

        // Microsoft Windows BMP v3

        /// <summary>
        /// The number of bits-per-pixel is specified or is implied by the JPEG or PNG format.
        /// <para>Supported by Windows 98/Windows 2000 or later.</para>
        /// <para>From Windows BMP v4.</para>
        /// </summary>
        JPEG_PNG = 0,

        /// <summary>
        /// The bitmap has a maximum of 2^16 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each WORD (2 bytes) in the bitmap array represents a single pixel. The relative intensities of red, green, and blue are represented with 5 bits
        /// for each color component.
        /// The value for blue is in the least significant 5 bits, followed by 5 bits each for green and red. The most significant bit is not used.</para>
        /// <para>Supported by Windows 95/Windows NT 4 or later.</para>
        /// <para>From Windows BMP v3.</para>
        /// </summary>
        RGB16 = 16,

        /// <summary>
        /// The bitmap has a maximum of 2^32 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each DWORD (4 bytes) in the bitmap array represents a single pixel.
        /// The relative intensities of red, green, and blue are represented with 8 bits for each color component.
        /// The value for blue is in the least significant 8 bits, followed by 8 bits each for green and red. The most significant byte is not used.</para>
        /// <para>Supported by Windows 95/Windows NT 4 or later.</para>
        /// <para>From Windows BMP v3.</para>
        /// </summary>
        RGB32 = 32
    }
}
