namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// .
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 124)]
    internal struct BITMAPV5HEADER
    {
        /// <summary>
        /// Specifies the size, in bytes, of the OS/2 bitmap header v2 structure (must be between 16 and 64 bytes).
        /// Applications should use this member to determine which bitmap information header structure fields are being used.
        /// </summary>
        public uint Size;

        /// <summary>
        /// Specifies the width of the bitmap in pixels.
        /// <para>
        /// Windows 98/ME, Windows 2000 or above: If <c>Compression</c> is <c>JPEG</c> or <c>PNG</c>,
        /// the Width member specifies the width of the decompressed JPEG or PNG image bmpFile, respectively.
        /// </para>
        /// </summary>
        public int Width;

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
        public int Height;

        /// <summary>
        /// .
        /// </summary>
        public ushort Planes;

        /// <summary>
        /// .
        /// </summary>
        public ushort BitCount;

        /// <summary>
        /// .
        /// </summary>
        public uint Compression;

        /// <summary>
        /// .
        /// </summary>
        public uint ImageSize;

        /// <summary>
        /// .
        /// </summary>
        public int XPelsPerMeter;

        /// <summary>
        /// .
        /// </summary>
        public int YPelsPerMeter;

        /// <summary>
        /// .
        /// </summary>
        public uint ColorsUsed;

        /// <summary>
        /// .
        /// </summary>
        public uint ColorsImportant;

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
