namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This is the Windows 2000 and Windows 98 BMP v5 DIB (Device Independent Bitmap) information header.
    /// <para>Supported since Windows 2000 and Windows 98.</para>
    /// <para>Implemented on Windows BMP v5 format.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(BITMAPV5HEADER)</c> returns the size of 124 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>The colors (<seealso cref="RGBQUAD"></seealso>) in the palette table should appear in order of importance and must follow this structure.</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">this MSN link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 124)]
    internal struct BITMAPV5HEADER
    {
        // Fields for Microsoft Windows BMP v2 and IBM OS/2 BMP v1

        /// <summary>
        /// The size in bytes required to store this structure: Always 124.
        /// </summary>
        public uint Size;

        /// <summary>
        /// Specifies the width of the bitmap in pixels.
        /// <remarks>
        /// On Windows 98, Windows 2000 and above: If <c>BITMAPV5HEADER.Compression</c> is <c>BmpCompression.JPEG</c> or <c>BmpCompression.PNG</c>,
        /// the <c>BITMAPV5HEADER.Width</c> member specifies the width of the decompressed JPEG or PNG image file, respectively.
        /// </remarks>
        /// </summary>
        public int Width;

        /// <summary>
        /// Specifies the height of the bitmap in pixels.
        /// <para>If <c>BITMAPV5HEADER.Height</c> is positive, the bitmap is a bottom-up DIB and its origin is the lower-left corner.</para>
        /// <para>If <c>BITMAPV5HEADER.Height</c> is negative, the bitmap is a top-down DIB and its origin is the upper-left corner.</para>
        /// <para>Top-down DIBs cannot be compressed: <c>BITMAPV5HEADER.Compression</c> must be either <c>BmpCompression.None</c>,<c>BmpCompression.BitFields</c> or <c>BmpCompression.AlphaBitFields</c>.</para>
        /// <remarks>
        /// On Windows 98, Windows 2000 and above: If <c>BITMAPV5HEADER.Compression</c> is <c>BmpCompression.JPEG</c> or <c>BmpCompression.PNG</c>,
        /// the <c>BITMAPV5HEADER.Width</c> member specifies the width of the decompressed JPEG or PNG image file, respectively.
        /// </remarks>
        /// </summary>
        public int Height;

        /// <summary>
        /// The number of planes for the target device: Always 1.
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// The number of bits-per-pixel (bpp). This value must be one of: 1, 2, 4, 8, 24 or 32.
        /// <para>If <c>BITMAPV5HEADER.BitsPerPixel</c> is 0, <c>BITMAPV5HEADER.Compression</c> must be either <c>BmpCompression.JPEG</c> or <c>BmpCompression.PNG</c>.</para>
        /// <para>If <c>BITMAPV5HEADER.BitsPerPixel</c> is 2, the bitmap is Windows CE 1.0 and above specific.</para>
        /// </summary>
        /// <remarks>
        /// The color table (if present) must follow the <c>BITMAPV5HEADER</c> structure, and consist of
        /// <seealso cref="RGBQUAD"></seealso> structure vector (most important colors at top), up to the maximum palette size dictated by the bpp.
        /// </remarks>
        /// <seealso cref="Compression"></seealso>
        public ushort BitsPerPixel;

        // Fields added for Microsoft Windows BMP v3

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
        /// <term>0 - <seealso cref="BmpCompression.None"></seealso></term>
        /// <description>Uncompressed</description>
        /// </item>
        /// <item>
        /// <term>1 - <seealso cref="BmpCompression.RLE8"></seealso></term>
        /// <description>8-bit RLE (only valid for 8 bpp)</description>
        /// </item>
        /// <item>
        /// <term>2 - <seealso cref="BmpCompression.RLE4"></seealso></term>
        /// <description>4-bit RLE (only valid for 4 bpp)</description>
        /// </item>
        /// <item>
        /// <term>3 - <seealso cref="BmpCompression.BitFields"></seealso></term>
        /// <description>RGB (and optionaly RGBA) components size are given on bitfields mask (only valid for 16 and 32 bpp)</description>
        /// </item>
        /// <item>
        /// <term>4 - <seealso cref="BmpCompression.JPEG"></seealso></term>
        /// <description>Contains a JPEG file</description>
        /// </item>
        /// <item>
        /// <term>5 - <seealso cref="BmpCompression.PNG"></seealso></term>
        /// <description>Contains a PNG file</description>
        /// </item>
        /// <item>
        /// <term>6 - <seealso cref="BmpCompression.AlphaBitFields"></seealso></term>
        /// <description>RGBA components size are given on bitfields mask (only valid for 16 and 32 bpp on Windows CE .NET 4.0 and later)</description>
        /// </item>
        /// <item>
        /// <term>11 - <seealso cref="BmpCompression.CMYK_None"></seealso></term>
        /// <description>Uncompressed CMYK (only valid on Windows Metafile)</description>
        /// </item>
        /// <item>
        /// <term>12 - <seealso cref="BmpCompression.CMYK_RLE_8"></seealso></term>
        /// <description>8-bit RLE (only valid for 8 bpp on Windows Metafile)</description>
        /// </item>
        /// <item>
        /// <term>13 - <seealso cref="BmpCompression.CMYK_RLE_4"></seealso></term>
        /// <description>4-bit RLE (only valid for 4 bpp on Windows Metafile)</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <remarks>
        /// For Windows CE Mobile 5.0 and later, this value can be <c>OR</c> with <c>BI_SRCPREROTATE</c> (0x8000)
        /// to specify that the source DIB section has the same rotation angle as the destination.
        /// </remarks>
        /// <seealso cref="BitsPerPixel"></seealso>
        public uint Compression;

        /// <summary>
        /// Specifies the size, in bytes, of the image. This may be set to 0 for <seealso cref="BmpCompression.None"></seealso> bitmaps.
        /// </summary>
        public uint ImageSize;

        /// <summary>
        /// .
        /// </summary>
        public int PixelsPerMeterX;

        /// <summary>
        /// Specifies the horizontal resolution, in pixels-per-meter, of the target device for the bitmap.
        /// <para>
        /// An application can use this value to select a bitmap from a resource group that best matches the characteristics of the current device.
        /// </para>
        /// </summary>
        public int PixelsPerMeterY;

        /// <summary>
        /// Specifies the number of color indexes in the color palette used by the bitmap. Most important colors first.
        /// <para>
        /// If this value is 0, the bitmap uses the maximum number of colors corresponding to the value of the <c>BITMAPV5HEADER.BitsPerPixel</c>
        /// member for the compression mode specified by <c>BITMAPV5HEADER.Compression</c>.
        /// </para>
        /// <para>
        /// If is nonzero and the <c>BITMAPV5HEADER.BitsPerPixel</c> member is less than 16, the <c>BITMAPV5HEADER.PaletteSize</c> member
        /// specifies the actual number of colors the graphics engine or device driver accesses.
        /// </para>
        /// <para>
        /// If <c>BITMAPV5HEADER.BitsPerPixel</c> is 16 or greater, the <c>BITMAPV5HEADER.PaletteSize</c> member specifies the size of the
        /// color table used to optimize performance of the system color palettes.
        /// </para>
        /// <para>
        /// When the bitmap array immediately follows the <c>BITMAPV5HEADER</c> structure, it is a packed bitmap.
        /// Packed bitmaps are referenced by a single pointer.
        /// Packed bitmaps require that the <c>BITMAPV5HEADER.PaletteSize</c> member must be either 0 or the actual size of the color table.
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

        // Fields added for Microsoft Windows BMP v4

        /// <summary>
        /// .
        /// </summary>
        public uint RedMask;

        /// <summary>
        /// .
        /// </summary>
        public uint GreenMask;

        /// <summary>
        /// .
        /// </summary>
        public uint BlueMask;

        /// <summary>
        /// .
        /// </summary>
        public uint AlphaMask;

        /// <summary>
        /// .
        /// </summary>
        public uint ColorSpaceType;

        /// <summary>
        /// .
        /// </summary>
        public CIEXYZTRIPLE Endpoints;

        /// <summary>
        /// .
        /// </summary>
        public uint GammaRed;

        /// <summary>
        /// .
        /// </summary>
        public uint GammaGreen;

        /// <summary>
        /// .
        /// </summary>
        public uint GammaBlue;

        // Fields added for Microsoft Windows BMP v5

        /// <summary>
        /// .
        /// </summary>
        public uint Intent;

        /// <summary>
        /// .
        /// </summary>
        public uint ProfileData;

        /// <summary>
        /// .
        /// </summary>
        public uint ProfileSize;

        /// <summary>
        /// .
        /// </summary>
        public uint Reserved;
    }
}
