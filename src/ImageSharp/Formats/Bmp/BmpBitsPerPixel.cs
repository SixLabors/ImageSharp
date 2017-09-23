// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Enumerates the available bits per pixel for bitmap.
    /// The number of bits-per-pixel (bpp) used in the Microsoft Windows BMP and IBM OS/2 BMP image DIB or file.
    /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">See this MSDN link for more information.</seealso>
    /// </summary>
    public enum BmpBitsPerPixel
    {
        // ** Fields for Microsoft Windows BMP v2 and IBM OS/2 BMP v1

        /// <summary>
        /// The bitmap is monochrome (1 bpp), and the palette contains 2 entries (2 colors palette).
        /// <para>Each bit in the bitmap array represents a pixel.
        /// If the bit is clear, the pixel is displayed with the color of the first entry in the palette table;
        /// if the bit is set, the pixel has the color of the second entry in the table.</para>
        /// <para>Supported by Windows 2.0 and OS/2 1.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        MonoChrome = 1,

        /// <summary>
        /// The bitmap has a maximum of 4 colors, and the palette contains up to 4 entries (4 colors palette).
        /// <para>Each 2 bits in the bitmap array represents a pixel (the index for the palette).
        /// For example, if the first byte in the bitmap is 0x1F, the byte represents 4 pixels.
        /// The first pixel contains the color in the second table entry, the second pixel will be the color in
        /// the sixteenth table entry.</para>
        /// <para>Supported by Windows CE 1.0x and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v3.</para>
        /// </summary>
        Palette4 = 2,

        /// <summary>
        /// The bitmap has a maximum of 16 colors, and the palette contains up to 16 entries (16 colors palette).
        /// <para>Each 4 bits in the bitmap array represents a pixel (the index for the palette).
        /// For example, if the first byte in the bitmap is 0x1F, the byte represents two pixels.
        /// The first pixel contains the color in the second table entry, and the second pixel contains the
        /// color in the sixteenth table entry.</para>
        /// <para>Supported by Windows 2.0 and OS/2 1.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        Palette16 = 4,

        /// <summary>
        /// The bitmap has a maximum of 256 colors, and the palette contains up to 256 entries (256 colors palette).
        /// <para>Each 8 bits (1 byte) in the bitmap array represents a pixel (the index for the palette).</para>
        /// <para>Supported by Windows 2.0 and OS/2 1.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        Palette256 = 8,

        /// <summary>
        /// The bitmap has a maximum of 2^24 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each pixel is made by 3 bytes that specifies the relative intensities of blue, green, and red color components respectively.</para>
        /// <para>Supported by Windows 2.0 and OS/2 1.0 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v2 and IBM OS/2 BMP v1 format.</para>
        /// </summary>
        RGB24 = 24,

        // Microsoft Windows BMP v3 (Windows NT and Windows 98)

        /// <summary>
        /// The number of bits-per-pixel is specified or is implied by the JPEG or PNG format.
        /// <para>Supported by Windows 98/Windows 2000 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v4.</para>
        /// </summary>
        JPEG_PNG = 0,

        /// <summary>
        /// The bitmap has a maximum of 2^16 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each WORD (2 bytes) in the bitmap array represents a single pixel. The relative intensities of red, green, and blue are represented with 5 bits
        /// for each color component.
        /// The value for blue is in the least significant 5 bits, followed by 5 bits each for green and red. The most significant bit is not used.</para>
        /// <para>Supported by Windows 95/Windows NT 4 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v3.</para>
        /// </summary>
        RGB16 = 16,

        /// <summary>
        /// The bitmap has a maximum of 2^32 colors. An optional 256 colors palette can be given, for optimizing the display on palette-based devices.
        /// <para>Each DWORD (4 bytes) in the bitmap array represents a single pixel.
        /// The relative intensities of red, green, and blue are represented with 8 bits for each color component.
        /// The value for blue is in the least significant 8 bits, followed by 8 bits each for green and red. The most significant byte is not used.</para>
        /// <para>Supported by Windows 95/Windows NT 4 and later.</para>
        /// <para>Implemented on Microsoft Windows BMP v3.</para>
        /// </summary>
        RGB32 = 32
    }
}
