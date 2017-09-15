// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This is the OS/2 2.x BMP v2 DIB (Device Independent Bitmap) information header.
    /// <para>Supported since OS/2 2.0.</para>
    /// <para>Implemented on OS/2 BMP v2 format.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(OS22XBITMAPHEADER)</c> returns the size of 64 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>The colors (<seealso cref="RGBQUAD"></seealso>) in the palette table should appear in order of importance and must follow this structure.</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// </remarks>
    /// See <a href="http://www.fileformat.info/format/os2bmp/egff.htm">this FileFormat link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct OS22XBITMAPHEADER
    {
        // Fields for IBM OS/2 BMP v1 and Microsoft Windows BMP v2

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
        /// </summary>
        /// <remarks>
        /// The color table (if present) must follow the <c>OS22XBITMAPHEADER</c> structure, and consist of
        /// <seealso cref="RGBQUAD"></seealso> structure vector (most important colors at top), up to the maximum palette size dictated by the bpp.
        /// </remarks>
        /// <seealso cref="Compression"></seealso>
        public ushort BitsPerPixel;

        // Fields added for IBM OS/2 BMP v2

        /// <summary>
        /// Specifies the type of compression for a compressed bottom-up bitmap (top-down DIBs cannot be compressed).
        /// <para>
        /// This value must be one of:
        /// <list type="table">
        /// <listheader>
        /// <term>Value</term>
        /// <description>Type of Compression</description>
        /// </listheader>
        /// <item>
        /// <term>0 - <seealso cref="BmpOS2Compression.None"></seealso></term>
        /// <description>Uncompressed</description>
        /// </item>
        /// <item>
        /// <term>1 - <seealso cref="BmpOS2Compression.RLE8"></seealso></term>
        /// <description>8-bit RLE (only valid for 8 bpp)</description>
        /// </item>
        /// <item>
        /// <term>2 - <seealso cref="BmpOS2Compression.RLE4"></seealso></term>
        /// <description>4-bit RLE (only valid for 4 bpp)</description>
        /// </item>
        /// <item>
        /// <term>3 - <seealso cref="BmpOS2Compression.Huffman1D"></seealso></term>
        /// <description>1-bit Huffman 1D (only valid for 1 bpp)</description>
        /// </item>
        /// <item>
        /// <term>4 - <seealso cref="BmpOS2Compression.RLE24"></seealso></term>
        /// <description>24-bit RLE (only valid for 24 bpp)</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <seealso cref="BitsPerPixel"></seealso>
        public uint Compression;

        /// <summary>
        /// Specifies the size, in bytes, of the image. This may be set to 0 for <seealso cref="BmpOS2Compression.None"></seealso> bitmaps.
        /// </summary>
        public uint ImageSize;

        /// <summary>
        /// Specifies the horizontal resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// <see>Units</see>
        /// </summary>
        public uint PixelsPerUnitX;

        /// <summary>
        /// Specifies the vertical resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// <see>Units</see>
        /// </summary>
        public uint PixelsPerUnitY;

        /// <summary>
        /// Specifies the number of color indexes in the color palette used by the bitmap. Most important colors first.
        /// <para>
        /// If this value is 0, the bitmap uses the maximum number of colors corresponding to the value of the <c>OS22XBITMAPHEADER.BitsPerPixel</c>
        /// member for the compression mode specified by <c>OS22XBITMAPHEADER.Compression</c>.
        /// </para>
        /// <para>
        /// If is nonzero and the <c>OS22XBITMAPHEADER.BitsPerPixel</c> member is less than 16, the <c>OS22XBITMAPHEADER.PaletteSize</c> member
        /// specifies the actual number of colors the graphics engine or device driver accesses.
        /// </para>
        /// <para>
        /// If <c>OS22XBITMAPHEADER.BitsPerPixel</c> is 16 or greater, the <c>OS22XBITMAPHEADER.PaletteSize</c> member specifies the size of the
        /// color table used to optimize performance of the system color palettes.
        /// </para>
        /// <para>
        /// When the bitmap array immediately follows the <c>OS22XBITMAPHEADER</c> structure, it is a packed bitmap.
        /// Packed bitmaps are referenced by a single pointer.
        /// Packed bitmaps require that the <c>OS22XBITMAPHEADER.PaletteSize</c> member must be either 0 or the actual size of the color table.
        /// </para>
        /// <see>PaletteImportant</see>
        /// </summary>
        public uint PaletteSize;

        /// <summary>
        /// Specifies the number of important color indexes from the color palette for displaying the bitmap.
        /// <para>If this value is 0, all colors are required.</para>
        /// <see>PaletteSize</see>
        /// </summary>
        public uint PaletteImportant;

        /// <summary>
        /// Indicates the type of units used to interpret the values of the <c>OS22XBITMAPHEADER.PixelsPerUnitX</c> and
        /// <c>OS22XBITMAPHEADER.PixelsPerUnitY</c> fields.
        /// <para>The only valid value is 0, indicating pixels-per-meter.</para>
        /// <see>PixelsPerUnitX</see>
        /// <see>PixelsPerUnitY</see>
        /// </summary>
        public ushort Units;

        /// <summary>
        /// <term>Reserved</term>
        /// <description>Unused and is always set to a value of zero.
        /// Pad structure to 4-byte boundary.</description>
        /// </summary>
        public ushort Reserved;

        /// <summary>
        /// <term>Recording</term>
        /// <description>Specifies how the bitmap scan lines are stored.
        /// The only valid value for this field is 0,
        /// indicating that the bitmap is stored from left to right and from the bottom up,
        /// with the origin being in the lower-left corner of the display.</description>
        /// </summary>
        public ushort Recording;

        /// <summary>
        /// <term>Rendering</term>
        /// <description>Specifies the halftoning algorithm used on the bitmap data.
        /// A value of 0 indicates that no halftoning algorithm was used;
        /// 1 indicates error diffusion halftoning;
        /// 2 indicates Processing Algorithm for Noncoded Document Acquisition (PANDA);
        /// and 3 indicates super-circle halftoning.</description>
        /// </summary>
        public ushort Rendering;

        /// <summary>
        /// <term>Size1</term>
        /// <description><c>OS22XBITMAPHEADER.Size1</c> and <c>OS22XBITMAPHEADER.Size2</c> are reserved fields used only by the halftoning algorithm.
        /// If error diffusion halftoning is used, Size1 is the error damping as a percentage in the range 0 through 100.
        /// A value of 100 percent indicates no damping, and a value of 0 percent indicates that any errors are not diffused.
        /// Size2 is not used by error diffusion halftoning.
        /// If PANDA or super-circle halftoning is specified, <c>OS22XBITMAPHEADER.Size1</c> is the X dimension and <c>OS22XBITMAPHEADER.Size2</c>
        /// is the Y dimension of the pattern used in pixels.</description>
        /// </summary>
        public uint Size1;

        /// <summary>
        /// <term>Size2</term>
        /// <description>See <c>Size1</c>.</description>
        /// </summary>
        public uint Size2;

        /// <summary>
        /// <term>ColorEncoding</term>
        /// <description>Color model used to describe the bitmap data.
        /// The only valid value is 0, indicating the None encoding scheme.</description>
        /// </summary>
        public uint ColorEncoding;

        /// <summary>
        /// <term>Identifier</term>
        /// <description>Reserved for application use and may
        /// contain an application-specific value. Normally is set to 0.</description>
        /// </summary>
        public uint Identifier;
    }
}
