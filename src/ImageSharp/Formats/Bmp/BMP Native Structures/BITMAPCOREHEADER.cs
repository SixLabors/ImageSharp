namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This is the Windows BMP v2 and OS/2 BMP v1 (or later) bitmap information header.
    /// <para>Supported since Windows 2.0x and OS/2 1.0x.</para>
    /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(BITMAPCOREHEADER)</c> returns the size of 12 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>The colors (<c>RGBTRIPLE</c>) in the palette table should appear in order of importance and must follow this structure.</para>
    /// <para>Each scan line must be zero-padded to end on a DWORD (4 bytes) boundary.</para>
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd183372(v=vs.85).aspx">this MSN link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)]
    internal struct BITMAPCOREHEADER
    {
        /// <summary>
        /// The size in bytes required to store the structure: Always 12.
        /// </summary>
        public uint Size;

        /// <summary>
        /// The width of the bitmap, in pixels.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// The height of the bitmap, in pixels.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// The number of planes for the target device: Always 1.
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// The number of bits-per-pixel (bpp). This value must be one of: 1, 4, 8, or 24.
        /// </summary>
        /// <remarks>
        /// The color table must follow the <c>BITMAPCOREHEADER</c> structure, and consist of
        /// <c>RGBTRIPLE</c> structure vector, up to the maximum palette size dictated by the bpp.
        /// </remarks>
        public ushort BitsPerPixel;
    }
}
