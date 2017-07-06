namespace ImageSharp.Formats
{
    /// <summary>
    /// This enumeration contains the sizes of the several native structures used by the BMP file format.
    /// </summary>
    internal enum BmpNativeStructuresSizes
    {
        // IBM OS/2 BMP Palette element

        /// <summary>
        /// The size in bytes of a color palette element for Windows BMP v2 and OS/2 BMP v1 file format.
        /// <para>
        /// Supported by OS/2 1.0x or later (same format as Windows 2.0x).
        /// </para>
        /// </summary>
        /// <see cref="http://www.fileformat.info/format/os2bmp/egff.htm"/>
        /// <seealso cref="RGBTRIPLE"/>
        OS21XPALETTEELEMENT = 3,

        /// <summary>
        /// The size in bytes of a color palette element for Windows BMP v3 and OS/2 BMP v2 file format.
        /// <para>
        /// Supported by OS/2 2.0x or later (same format as Windows 3.1x).
        /// </para>
        /// </summary>
        /// <see cref="http://www.fileformat.info/format/os2bmp/egff.htm"/>
        /// <seealso cref="RGBQUAD"/>
        OS22XPALETTEELEMENT = 4,

        // IBM OS/2 BMP DIB header

        /// <summary>
        /// The size in bytes for the bitmap information header of Windows BMP v2 and OS/2 BMP v1 file format.
        /// <para>
        /// Supported by OS/2 1.0x or later (same format as Windows 2.0x).
        /// </para>
        /// </summary>
        /// <see cref="http://www.fileformat.info/format/os2bmp/egff.htm"/>
        /// <seealso cref="BITMAPCOREHEADER"/>
        OS21XBITMAPHEADER = 12,

        /// <summary>
        /// The minimum size in bytes for the bitmap information header of OS/2 BMP v2 file format.
        /// <para>
        /// Supported by OS/2 2.0x or later.
        /// </para>
        /// </summary>
        /// <see cref="http://www.fileformat.info/format/os2bmp/egff.htm"/>
        OS22XBITMAPHEADER_MIN = 16,

        /// <summary>
        /// The maximum size in bytes for the bitmap information header of OS/2 BMP v2 file format.
        /// <para>
        /// Supported by OS/2 2.0x or later.
        /// </para>
        /// </summary>
        /// <see cref="http://www.fileformat.info/format/os2bmp/egff.htm"/>
        OS22XBITMAPHEADER = 64,

        // Microsoft Windows BMP Palette element

        /// <summary>
        /// The size in bytes of a color palette element for Windows BMP v2 and OS/2 BMP v1 file format.
        /// <para>
        /// Supported by Windows 2.0x or later (same format as OS/2 1.0x).
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd162939(v=vs.85).aspx"/>
        RGBTRIPLE = 3,

        /// <summary>
        /// The size in bytes of a color palette element for Windows BMP v3 and OS/2 BMP v2 file format.
        /// <para>
        /// Supported by Windows 3.0x or later (same format as OS/2 2.0x).
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx"/>
        RGBQUAD = 4,

        // Microsoft Windows BMP and IBM OS/2 file header

        /// <summary>
        /// The size in bytes for the file header of Windows BMP v2 and OS/2 BMP v1 file format.
        /// <para>
        /// Supported by Windows 2.0x and OS/2 1.0x or later.
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx"/>
        BITMAPFILEHEADER = 14,

        // Microsoft Windows BMP DIB header

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v2 and OS/2 BMP v1 file format.
        /// <para>
        /// Supported by Window 2.0x or later (same format as OS/2 1.0x).
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd183372(v=vs.85).aspx"/>
        BITMAPCOREHEADER = 12,

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v3 file format.
        /// <para>
        /// Supported by Window 3.0x, Windows NT 3.1 or later.
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd183376(v=vs.85).aspx"/>
        BITMAPINFOHEADER = 40,

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v3 file format width Windows NT 4 RGB bitfields extension.
        /// <para>
        /// Supported by Windows NT 3.1 or later.
        /// </para>
        /// </summary>
        /// <see cref="https://en.wikipedia.org/wiki/BMP_file_format"/>
        BITMAPINFOHEADER_NT = 52,

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v3 file format width Windows CE RGBA bitfields extension.
        /// <para>
        /// Supported by Windows CE 5.0 with .NET 4.0 or later.
        /// </para>
        /// </summary>
        /// <see cref="https://en.wikipedia.org/wiki/BMP_file_format"/>
        BITMAPINFOHEADER_CE = 56,

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v4 file format.
        /// <para>
        /// Supported by Window 95, Windows NT 4 or later.
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd183380(v=vs.85).aspx"/>
        BITMAPV4HEADER = 108,

        /// <summary>
        /// The size in bytes for the bitmap header of Windows BMP v5 file format.
        /// <para>
        /// Supported by Window 98, Windows 2000 or later.
        /// </para>
        /// </summary>
        /// <see cref="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx"/>
        BITMAPV5HEADER = 124
    }
}
