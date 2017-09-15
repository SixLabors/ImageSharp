// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
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
