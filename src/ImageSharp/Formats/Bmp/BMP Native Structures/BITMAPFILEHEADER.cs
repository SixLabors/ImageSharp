namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This is the Windows BMP v2 and OS/2 BMP v1 (or later) file header that
    /// contains information about the type, size, and layout of the contained DIB (Device Independent Bitmap).
    /// <para>Supported since Windows 2.0x and OS/2 1.0x.</para>
    /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(BITMAPFILEHEADER)</c> returns the size of 12 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// <para>
    /// The DIB information header must follow the <c>BITMAPFILEHEADER</c> structure, and consist of
    /// <c>OS22XBITMAPHEADER</c>, <c>BITMAPCOREHEADER</c> or <c>BITMAPV5HEADER</c> structure.
    /// </para>
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd183374(v=vs.85).aspx">this MSN link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 14)]
    internal struct BITMAPFILEHEADER
    {
        /// <summary>
        /// Specifies the BMP file type "Magic ID", must be "BM" in ASCII or 4D42h in hexadecimal.
        /// <para>
        /// OS/2 allows also this "Magic IDs" for other image file formats:
        /// <list type="table">
        /// <listheader>
        /// <term>ASCII/Hexadecimal Value</term>
        /// <description>Type of Image File</description>
        /// </listheader>
        /// <item>
        /// <term>"BA"/4142h</term>
        /// <description>Bitmap Array</description>
        /// </item>
        /// <item>
        /// <term>"CI"/4943h</term>
        /// <description>Color Icon</description>
        /// </item>
        /// <item>
        /// <term>"CP"/5043h</term>
        /// <description>Color Pointer</description>
        /// </item>
        /// <item>
        /// <term>"IC"/4349h</term>
        /// <description>Icon</description>
        /// </item>
        /// <item>
        /// <term>"PT"/5450h</term>
        /// <description>Pointer</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        public ushort Magic;

        /// <summary>
        /// Specifies the size, in bytes, of the bitmap BMP file.
        /// </summary>
        public uint FileSize;

        /// <summary>
        /// Reserved; must be zero.
        /// <para>
        /// For OS/2 specific formats, this is the X coordinate of the central point of the hotspot for icons and pointers.
        /// </para>
        /// </summary>
        public ushort Reserved1;

        /// <summary>
        /// Reserved; must be zero.
        /// <para>
        /// For OS/2 specific formats, this is the Y coordinate of the central point of the hotspot for icons and pointers.
        /// </para>
        /// </summary>
        public ushort Reserved2;

        /// <summary>
        /// Specifies the offset, in bytes, from the beginning of the BITMAPFILEHEADER structure to the bitmap pixels color bits.
        /// <para>
        /// This value is obtained by this formula: Size_Of_BITMAP_FILE_HEADER + Size_Of_BITMAP_INFO_HEADER + Size_Of_COLOR_palette + Size_Of_Gap
        /// </para>
        /// </summary>
        public uint PixelsOffset;
    }
}
