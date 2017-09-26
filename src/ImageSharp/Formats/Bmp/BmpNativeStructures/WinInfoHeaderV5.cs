// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This is the Windows 2000 and Windows 98 BMP v5 DIB (Device Independent Bitmap) information header: BITMAPV5HEADER.
    /// <para>Supported since Windows 2000 and Windows 98.</para>
    /// <para>Implemented on Microsoft Windows BMP v5 format.</para>
    /// <seealso href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">See this MSDN link for more information.</seealso>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(WinInfoHeaderV5)</c> returns the size of 124 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>The colors (<see cref="WinRgbQuadruple"/>) in the palette table should appear in order of importance and must follow this structure.</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 124)]
    internal struct WinInfoHeaderV5
    {
        // ** Fields for Microsoft Windows BMP v2 and IBM OS/2 BMP v1 DIB header

        /// <summary>
        /// The size in bytes required to store this structure: Always 124.
        /// </summary>
        public uint Size;

        /// <summary>
        /// Specifies the width of the bitmap in pixels.
        /// <remarks>
        /// On Windows 98, Windows 2000 and above: If <c>WinInfoHeaderV5.Compression</c> is <c>BmpCompression.JPEG</c> or <c>BmpCompression.PNG</c>,
        /// the <c>WinInfoHeaderV5.Width</c> member specifies the width of the decompressed JPEG or PNG image file, respectively.
        /// </remarks>
        /// </summary>
        public int Width;

        /// <summary>
        /// Specifies the height of the bitmap in pixels.
        /// <para>If <c>WinInfoHeaderV5.Height</c> is positive, the bitmap is a bottom-up DIB and its origin is the lower-left corner.</para>
        /// <para>If <c>WinInfoHeaderV5.Height</c> is negative, the bitmap is a top-down DIB and its origin is the upper-left corner.</para>
        /// <para>Top-down DIBs cannot be compressed: <c>WinInfoHeaderV5.Compression</c> must be either <c>BmpCompression.RGB</c>,
        /// <c>BmpCompression.BitFields</c> or <c>BmpCompression.AlphaBitFields</c>.</para>
        /// <remarks>
        /// On Windows 98, Windows 2000 and above: If <c>WinInfoHeaderV5.Compression</c> is <c>BmpCompression.JPEG</c> or <c>BmpCompression.PNG</c>,
        /// the <c>WinInfoHeaderV5.Height</c> member specifies the height of the decompressed JPEG or PNG image file, respectively.
        /// </remarks>
        /// </summary>
        public int Height;

        /// <summary>
        /// The number of planes for the target device: Always 1.
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// The number of bits-per-pixel (bpp). This value must be one of: 1, 2, 4, 8, 24 or 32.
        /// <para>If <c>WinInfoHeaderV5.BitsPerPixel</c> is 0, <c>WinInfoHeaderV5.Compression</c> must be either <c>BmpCompression.JPEG</c> or
        /// <c>BmpCompression.PNG</c>.</para>
        /// <para>If <c>WinInfoHeaderV5.BitsPerPixel</c> is 2, the bitmap is Windows CE 1.0 and above specific.</para>
        /// <see cref="Compression"/>
        /// </summary>
        /// <remarks>
        /// The color table (if present) must follow the <c>WinInfoHeaderV5</c> structure, and consist of
        /// <see cref="RGBQUAD"/> structure vector (most important colors at top), up to the maximum palette size dictated by the bpp.
        /// </remarks>
        public ushort BitsPerPixel;

        // ** Fields added for Microsoft Windows BMP v3 DIB header

        /// <summary>
        /// Specifies the type of compression scheme used for compressing a bottom-up bitmap (top-down DIBs cannot be compressed).
        /// <para>
        /// This value must be one of:
        /// <list type="table">
        /// <listheader>
        /// <term>Value</term>
        /// <description>Compression Scheme</description>
        /// </listheader>
        /// <item>
        /// <term>0 - <see cref="BmpCompression.RGB"/></term>
        /// <description>Uncompressed, uses RGB or RGBA</description>
        /// </item>
        /// <item>
        /// <term>1 - <see cref="BmpCompression.RLE8"/></term>
        /// <description>8-bit RLE (only valid for 8 bpp)</description>
        /// </item>
        /// <item>
        /// <term>2 - <see cref="BmpCompression.RLE4"/></term>
        /// <description>4-bit RLE (only valid for 4 bpp)</description>
        /// </item>
        /// <item>
        /// <term>3 - <see cref="BmpCompression.BitFields"/></term>
        /// <description>RGB (and optionaly RGBA) components size are given on bitfields mask (only valid for 16 and 32 bpp)</description>
        /// </item>
        /// <item>
        /// <term>4 - <see cref="BmpCompression.JPEG"/></term>
        /// <description>Contains a JPEG file</description>
        /// </item>
        /// <item>
        /// <term>5 - <see cref="BmpCompression.PNG"/></term>
        /// <description>Contains a PNG file</description>
        /// </item>
        /// <item>
        /// <term>6 - <see cref="BmpCompression.AlphaBitFields"/></term>
        /// <description>RGBA components size are given on bitfields mask (only valid for 16 and 32 bpp on Windows CE .NET 4.0 and later)</description>
        /// </item>
        /// <item>
        /// <term>11 - <see cref="BmpCompression.CMYK"/></term>
        /// <description>Uncompressed, uses CMYK (only valid on Windows Metafile)</description>
        /// </item>
        /// <item>
        /// <term>12 - <see cref="BmpCompression.CMYK_RLE8"/></term>
        /// <description>8-bit RLE (only valid for 8 bpp on Windows Metafile)</description>
        /// </item>
        /// <item>
        /// <term>13 - <see cref="BmpCompression.CMYK_RLE4"/></term>
        /// <description>4-bit RLE (only valid for 4 bpp on Windows Metafile)</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <remarks>
        /// For Windows CE Mobile 5.0 and later, this value can be <c>OR</c> with <c>BI_SRCPREROTATE</c> (8000h in hexadecimal)
        /// to specify that the source DIB section has the same rotation angle as the destination.
        /// </remarks>
        /// <seealso cref="BitsPerPixel"/>
        public uint Compression;

        /// <summary>
        /// Specifies the size, in bytes, of the image. This may be set to 0 for <seealso cref="BmpCompression.RGB"/> bitmaps.
        /// </summary>
        public uint ImageSize;

        /// <summary>
        /// Specifies the horizontal resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// </summary>
        public int PixelsPerMeterX;

        /// <summary>
        /// Specifies the vertical resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// </summary>
        public int PixelsPerMeterY;

        /// <summary>
        /// Specifies the number of color indexes in the color palette used by the bitmap. Most important colors first.
        /// <para>
        /// If this value is 0, the bitmap uses the maximum number of colors corresponding to the value of the <c>WinInfoHeaderV5.BitsPerPixel</c>
        /// member for the compression mode specified by <c>WinInfoHeaderV5.Compression</c>.
        /// </para>
        /// <para>
        /// If is nonzero and the <c>WinInfoHeaderV5.BitsPerPixel</c> member is less than 16, the <c>WinInfoHeaderV5.PaletteSize</c> member
        /// specifies the actual number of colors the graphics engine or device driver accesses.
        /// </para>
        /// <para>
        /// If <c>WinInfoHeaderV5.BitsPerPixel</c> is 16 or greater, the <c>WinInfoHeaderV5.PaletteSize</c> member specifies the size of the
        /// color table used to optimize performance of the system color palettes.
        /// </para>
        /// <para>
        /// When the bitmap array immediately follows the <c>WinInfoHeaderV5</c> structure, it is a packed bitmap.
        /// Packed bitmaps are referenced by a single pointer.
        /// Packed bitmaps require that the <c>WinInfoHeaderV5.PaletteSize</c> member must be either 0 or the actual size of the color table.
        /// </para>
        /// <see cref="PaletteImportant"/>
        /// </summary>
        public uint PaletteSize;

        /// <summary>
        /// Specifies the number of important color indexes from the color palette for displaying the bitmap.
        /// <para>If this value is 0, all colors are required to display the bitmap correctly.</para>
        /// <see cref="PaletteSize"/>
        /// </summary>
        public uint PaletteImportant;

        // ** Fields added for Microsoft Windows BMP v4 DIB header

        /// <summary>
        /// Color mask that specifies the red component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or  <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint RedMask;

        /// <summary>
        /// Color mask that specifies the green component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint GreenMask;

        /// <summary>
        /// Color mask that specifies the blue component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint BlueMask;

        /// <summary>
        /// Color mask that specifies the alpha (transparency) component of each pixel, valid only if <c>WinInfoHeaderV5.Compression</c> is set to
        /// <see cref="BmpCompression.BitFields"/> or <see cref="BmpCompression.AlphaBitFields"/>.
        /// </summary>
        public uint AlphaMask;

        /// <summary>
        /// Specifies the color space of the DIB.
        /// <see cref="BmpColorSpace"/>
        /// </summary>
        public uint ColorSpaceType;

        /// <summary>
        /// A structure that specifies the x, y and z coordinates of the three colors that correspond to the
        /// red, green and blue endpoints for the logical color space associated with the bitmap. <see cref="CIEXYZTRIPLE"/>.
        /// This member is ignored unless the <c>WinInfoHeaderV5.ColorSpaceType</c> member specifies <seealso cref="BmpColorSpace.Calibrated_RGB"/>.
        /// <para>
        /// <b>Note:</b> A color space is a model for representing color numerically in terms of three or more coordinates.
        /// For example, the RGB color space represents colors in terms of the red, green and blue coordinates.
        /// </para>
        /// </summary>
        public WinCieXyzTriple Endpoints;

        /// <summary>
        /// Toned response curve for red.
        /// This member is ignored unless color values are calibrated RGB values and <c>WinInfoHeaderV5.ColorSpaceType</c> is set to
        /// <see cref="BmpColorSpace.Calibrated_RGB"/>.
        /// Specified in 16^16 format.
        /// </summary>
        public uint GammaRed;

        /// <summary>
        /// Toned response curve for green.
        /// This member is ignored unless color values are calibrated RGB values and <c>WinInfoHeaderV5.ColorSpaceType</c> is set to
        /// <see cref="BmpColorSpace.Calibrated_RGB"/>.
        /// Specified in 16^16 format.
        /// </summary>
        public uint GammaGreen;

        /// <summary>
        /// Toned response curve for blue.
        /// This member is ignored unless color values are calibrated RGB values and <c>WinInfoHeaderV5.ColorSpaceType</c> is set to
        /// <see cref="BmpColorSpace.Calibrated_RGB"/>.
        /// Specified in 16^16 format.
        /// </summary>
        public uint GammaBlue;

        // ** Fields added for Microsoft Windows BMP v5 DIB header

        /// <summary>
        /// Rendering intent for bitmap.
        /// <see cref="BmpColorSpaceIntent"/>
        /// <para>The Independent Color Management interface (ICM) 2.0 allows International Color Consortium (ICC) color profiles
        /// to be linked or embedded in DIBs.</para>
        /// </summary>
        public uint Intent;

        /// <summary>
        /// The offset, in bytes, from the beginning of the <c>WinInfoHeaderV5</c> structure to the start of the profile data.
        /// This member is ignored unless <c>WinInfoHeaderV5.ColorSpaceType</c> is set to <see cref="BmpColorSpace.ProfileLinked"/> or
        /// <see cref="BmpColorSpace.ProfileEmbedded"/>.
        /// <para>If the profile is embedded, profile data is the actual ICM 2.0 profile.</para>
        /// <para>If the profile is linked,  profile data is the null-terminated file name of the ICM 2.0 profile or
        /// the fully qualified path (including a network path) of the profile used by the DIB.
        /// It must be composed exclusively of characters from the Windows character set (code page 1252). This cannot be a Unicode string.</para>
        /// </summary>
        public uint ProfileData;

        /// <summary>
        /// Size, in bytes, of embedded profile data.
        /// This member is ignored unless <c>WinInfoHeaderV5.ColorSpaceType</c> is set to <see cref="BmpColorSpace.ProfileLinked"/> or
        /// <see cref="BmpColorSpace.ProfileEmbedded"/>.
        /// <para>The profile data (if present) should follow the color table.</para>
        /// <para>For packed DIBs, the profile data should follow the bitmap bits similar to the file format.</para>
        /// </summary>
        public uint ProfileSize;

        /// <summary>
        /// This member has been reserved for future use. Its value must be set to 0.
        /// </summary>
        public uint Reserved;
    }
}
