namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// .
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
    internal struct CIEXYZTRIPLE
    {
        /// <summary>
        /// .
        /// </summary>
        public CIEXYZ CiexyzRed;

        /// <summary>
        /// .
        /// </summary>
        public CIEXYZ CiexyzGreen;

        /// <summary>
        /// .
        /// </summary>
        public CIEXYZ CiexyzBlue;
    }
}
