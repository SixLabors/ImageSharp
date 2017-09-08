namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// .
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)]
    internal struct CIEXYZ
    {
        /// <summary>
        /// .
        /// </summary>
        public uint CiexyzX;

        /// <summary>
        /// .
        /// </summary>
        public uint CiexyzY;

        /// <summary>
        /// .
        /// </summary>
        public uint CiexyzZ;
    }
}
