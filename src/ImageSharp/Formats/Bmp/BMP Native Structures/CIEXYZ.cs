// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
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
