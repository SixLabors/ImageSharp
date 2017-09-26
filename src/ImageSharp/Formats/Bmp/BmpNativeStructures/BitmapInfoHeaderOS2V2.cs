// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This is the OS/2 2.x BMP v2 DIB (Device Independent Bitmap) information header: OS22XBITMAPHEADER.
    /// <para>Supported since OS/2 2.0.</para>
    /// <para>Implemented on IBM OS/2 BMP v2 format.</para>
    /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(BitmapInfoHeaderOS2V2)</c> returns the size of 64 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>The colors (<seealso cref="RGBQUAD"/>) in the palette table should appear in order
    /// of importance and must follow this structure.</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct BitmapInfoHeaderOS2V2
    {
        // ** Fields for Microsoft Windows BMP v2 and IBM OS/2 BMP v1 DIB header

        /// <summary>
        /// The size in bytes required to store this structure: Must be between 16 and 64 bytes.
        /// <para>
        /// Applications should use this member to determine which bitmap information header structure fields are being used.
        /// If the field is not present, 0 is assumed as it's value.
        /// </para>
        /// </summary>
        public uint Size;

        /// <summary>
        /// The width of the bitmap, in pixels.
        /// </summary>
        public uint Width;

        /// <summary>
        /// The height of the bitmap, in pixels.
        /// </summary>
        public uint Height;

        /// <summary>
        /// The number of planes for the target device: Always 1.
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// The number of bits-per-pixel (bpp). This value must be one of: 1, 4, 8, or 24.
        /// <see cref="Compression"/>
        /// </summary>
        /// <remarks>
        /// The color table (if present) must follow the <c>BitmapInfoHeaderOS2V2</c> structure, and consist of
        /// <see cref="RGBQUAD"/> structure vector (most important colors at top), up to the maximum palette size dictated by the bpp.
        /// </remarks>
        public ushort BitsPerPixel;

        // ** Fields added for IBM OS/2 BMP v2 DIB header

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
        /// <term>0 - <see cref="BmpOS2Compression.RGB"/></term>
        /// <description>Uncompressed</description>
        /// </item>
        /// <item>
        /// <term>1 - <see cref="BmpOS2Compression.RLE8"/></term>
        /// <description>8-bit RLE (only valid for 8 bpp)</description>
        /// </item>
        /// <item>
        /// <term>2 - <see cref="BmpOS2Compression.RLE4"/></term>
        /// <description>4-bit RLE (only valid for 4 bpp)</description>
        /// </item>
        /// <item>
        /// <term>3 - <see cref="BmpOS2Compression.Huffman1D"/></term>
        /// <description>1-bit Huffman 1D (only valid for 1 bpp)</description>
        /// </item>
        /// <item>
        /// <term>4 - <see cref="BmpOS2Compression.RLE24"/></term>
        /// <description>24-bit RLE (only valid for 24 bpp)</description>
        /// </item>
        /// </list>
        /// </para>
        /// <see cref="BitsPerPixel"/>
        /// </summary>
        public uint Compression;

        /// <summary>
        /// Specifies the size, in bytes, of the image. This may be set to 0 for <see cref="BmpOS2Compression.RGB"/> bitmaps.
        /// </summary>
        public uint ImageSize;

        /// <summary>
        /// Specifies the horizontal resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// <see cref="Units"/>
        /// </summary>
        public uint PixelsPerUnitX;

        /// <summary>
        /// Specifies the vertical resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// <see cref="Units"/>
        /// </summary>
        public uint PixelsPerUnitY;

        /// <summary>
        /// Specifies the number of color indexes in the color palette used by the bitmap. Most important colors first.
        /// <para>
        /// If this value is 0, the bitmap uses the maximum number of colors corresponding to the value of the <c>BitmapInfoHeaderOS2V2.BitsPerPixel</c>
        /// member for the compression mode specified by <c>BitmapInfoHeaderOS2V2.Compression</c>.
        /// </para>
        /// <para>
        /// If is nonzero and the <c>BitmapInfoHeaderOS2V2.BitsPerPixel</c> member is less than 16, the <c>BitmapInfoHeaderOS2V2.PaletteSize</c> member
        /// specifies the actual number of colors the graphics engine or device driver accesses.
        /// </para>
        /// <para>
        /// If <c>BitmapInfoHeaderOS2V2.BitsPerPixel</c> is 16 or greater, the <c>BitmapInfoHeaderOS2V2.PaletteSize</c> member specifies the size of the
        /// color table used to optimize performance of the system color palettes.
        /// </para>
        /// <para>
        /// When the bitmap array immediately follows the <c>BitmapInfoHeaderOS2V2</c> structure, it is a packed bitmap.
        /// Packed bitmaps are referenced by a single pointer.
        /// Packed bitmaps require that the <c>BitmapInfoHeaderOS2V2.PaletteSize</c> member must be either 0 or the actual size of the color table.
        /// </para>
        /// <see cref="PaletteImportant"/>
        /// </summary>
        public uint PaletteSize;

        /// <summary>
        /// Specifies the number of important color indexes from the color palette for displaying the bitmap.
        /// <para>If this value is 0, all colors are required.</para>
        /// <see cref="PaletteSize"/>
        /// </summary>
        public uint PaletteImportant;

        /// <summary>
        /// Indicates the type of units used to interpret the values of the <c>BitmapInfoHeaderOS2V2.PixelsPerUnitX</c> and
        /// <c>BitmapInfoHeaderOS2V2.PixelsPerUnitY</c> fields.
        /// <para>The only valid value is 0, indicating pixels-per-meter.</para>
        /// <see cref="PixelsPerUnitX"/>
        /// <see cref="PixelsPerUnitY"/>
        /// </summary>
        public ushort Units;

        /// <summary>
        /// Reserved for future use. Must be set to 0.
        /// <para>Pad structure to 4-byte boundary.</para>
        /// </summary>
        public ushort Reserved;

        /// <summary>
        /// Specifies how the bitmap scan lines are stored.
        /// <para>The only valid value for this field is 0,
        /// indicating that the bitmap is stored from left to right and from the bottom up,
        /// with the origin being in the lower-left corner of the display.</para>
        /// </summary>
        public ushort Recording;

        /// <summary>
        /// Specifies the halftoning algorithm used when compressing the bitmap data.
        /// <para>
        /// This value must be one of:
        /// <list type="table">
        /// <listheader>
        /// <term>Value</term>
        /// <description>Halftoning Algorithm</description>
        /// </listheader>
        /// <item>
        /// <term>0 - <see cref="BmpOS2CompressionHalftoning.NoHalftoning"/></term>
        /// <description>No halftoning algorithm was used</description>
        /// </item>
        /// <item>
        /// <term>1 - <see cref="BmpOS2CompressionHalftoning.ErrorDiffusion"/></term>
        /// <description>Error Diffusion</description>
        /// </item>
        /// <item>
        /// <term>2 - <see cref="BmpOS2CompressionHalftoning.Panda"/></term>
        /// <description>Processing Algorithm for Noncoded Document Acquisition (PANDA)</description>
        /// </item>
        /// <item>
        /// <term>3 - <see cref="BmpOS2CompressionHalftoning.SuperCircle"/></term>
        /// <description>Super-Circle</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        public ushort Rendering;

        /// <summary>
        /// Reserved field used only by the halftoning algorithm.
        /// <para>If Error Diffusion halftoning is used, this is the error damping as a percentage in the range 0 through 100.
        /// A value of 100 percent indicates no damping, and a value of 0 percent indicates that any errors are not diffused.</para>
        /// <para>If PANDA or Super-Circle halftoning is specified, this is the X dimension of the pattern used in pixels.</para>
        /// </summary>
        public uint Size1;

        /// <summary>
        /// Reserved field used only by the halftoning algorithm.
        /// <para>If Error Diffusion halftoning is used, this field is not used by error diffusion halftoning.</para>
        /// <para>If PANDA or Super-Circle halftoning is specified, this is the Y dimension of the pattern used in pixels.</para>
        /// </summary>
        public uint Size2;

        /// <summary>
        /// Color model used to describe the bitmap data.
        /// <para>The only valid value is 0, indicating the RGB encoding scheme.</para>
        /// </summary>
        public uint ColorEncoding;

        /// <summary>
        /// Reserved for application use and may contain an application-specific value.
        /// <para>Normally is set to 0.</para>
        /// </summary>
        public uint Identifier;
    }
}
