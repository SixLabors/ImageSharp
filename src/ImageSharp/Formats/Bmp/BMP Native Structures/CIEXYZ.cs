// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
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
