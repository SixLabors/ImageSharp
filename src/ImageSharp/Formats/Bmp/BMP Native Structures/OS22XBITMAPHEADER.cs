namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This is the OS/2 2.x BMP v2 DIB information header.
    /// <para>Make shore that sizeof(OS22XBITMAPHEADER) returns 64 (DWORD aligned, the default for VC++ 32 bits).</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// <para>The colors in the palette table should appear in order of importance.</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct OS22XBITMAPHEADER
    {
        // Fields for IBM OS/2 BMP v1

        /// <summary>
        /// Specifies the size, in bytes, of the OS/2 bitmap header v2 structure (must be between 16 and 64 bytes).
        /// Applications should use this member to determine which bitmap information header structure fields are being used.
        /// </summary>
        public uint HeaderSize;

        /// <summary>
        /// Specifies the width of the bitmap in pixels.
        /// <para>
        /// Windows 98/ME, Windows 2000 or above: If <c>Compression</c> is <c>JPEG</c> or <c>PNG</c>,
        /// the Width member specifies the width of the decompressed JPEG or PNG image bmpFile, respectively.
        /// </para>
        /// </summary>
        public uint Width;

        /// <summary>
        /// Specifies the height of the bitmap in pixels.
        /// <para>If <c>Height</c> is positive, the bitmap is a bottom-up DIB and its origin is the lower-left corner.</para>
        /// <para>If <c>Height</c> is negative, the bitmap is a top-down DIB and its origin is the upper-left corner.</para>
        /// <para>Top-down DIBs cannot be compressed: <c>Compression</c> must be either <c>None</c> or <c>BitFields</c>.</para>
        /// <para>
        /// Windows 98/ME, Windows 2000 or above: If <c>Compression</c> is <c>JPEG</c> or <c>PNG</c>,
        /// the Width member specifies the height of the decompressed JPEG or PNG image bmpFile, respectively.
        /// </para>
        /// </summary>
        public uint Height;

        /// <summary>
        /// Specifies the number of planes for the target device. This value must be 1.
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// Specifies the number of bits per pixel. This value must be: 1, 4, 8 or 24.
        /// </summary>
        /// <seealso cref="OS22XBITMAPHEADER.Compression"></seealso>
        public ushort BitsPerPixel;

        // Fields added for IBM OS/2 BMP v2

        /// <summary>
        /// Specifies the type of compression for a compressed bottom-up bitmap (top-down DIBs cannot be compressed).
        /// This value must be:
        ///  * 0 - indicates that the data is uncompressed;
        ///  * 1 - indicates that the 8-bit RLE algorithm was used;
        ///  * 2 - indicates that the 4-bit RLE algorithm was used;
        ///  * 3 - indicates that the 1-bit Huffman 1D algorithm was used;
        ///  * 4 - indicates that the 24-bit RLE algorithm was used.
        /// </summary>
        public uint Compression;

        /// <summary>
        /// Specifies the size, in bytes, of the image. This may be set to zero for <c>RGB</c> bitmaps.
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
        /// <para>If this value is 0, the bitmap uses the maximum number of colors corresponding to the value of the <c>BitsPerPixel</c> member for the compression mode specified by <c>Compression</c>.</para>
        /// <para>If is nonzero and the <c>BitsPerPixel</c> member is less than 16, the <c>PaletteColors</c> member specifies the actual number of colors the graphics engine or device driver accesses.</para>
        /// <para>If <c>BitsPerPixel</c> is 16 or greater, the <c>PaletteColors</c> member specifies the size of the color table used to optimize performance of the system color palettes.</para>
        /// <para>
        /// When the bitmap array immediately follows the BITMAPINFO structure, it is a packed bitmap.
        /// Packed bitmaps are referenced by a single pointer.
        /// Packed bitmaps require that the <c>PaletteColors</c> member must be either zero or the actual size of the color table.
        /// </para>
        /// <see>PaletteImportant</see>
        /// </summary>
        public uint PaletteColors;

        /// <summary>
        /// Specifies the number of important color indexes from the color palette for displaying the bitmap.
        /// <para>If this value is 0, all colors are required.</para>
        /// <see>PaletteColors</see>
        /// </summary>
        public uint PaletteImportant;

        /// <summary>
        /// Indicates the type of units used to interpret the values of the <c>PixelsPerUnitX</c> and <c>PixelsPerUnitY</c> fields.
        /// <para>The only valid value is 0, indicating pixels per meter.</para>
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
        /// <description><c>Size1</c> and <c>Size2</c> are reserved fields used only by the halftoning algorithm.
        /// If error diffusion halftoning is used, Size1 is the error damping as a percentage in the range 0 through 100.
        /// A value of 100 percent indicates no damping, and a value of 0 percent indicates that any errors are not diffused.
        /// Size2 is not used by error diffusion halftoning.
        /// If PANDA or super-circle halftoning is specified, <c>Size1</c> is the X dimension and <c>Size2</c>
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
