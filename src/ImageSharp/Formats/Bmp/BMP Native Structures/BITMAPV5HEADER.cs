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
        /// .
        /// </summary>
        public uint Size;

        /// <summary>
        /// .
        /// </summary>
        public int Width;

        /// <summary>
        /// .
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
