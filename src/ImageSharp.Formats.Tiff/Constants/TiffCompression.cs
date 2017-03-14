// <copyright file="TiffCompression.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the compression formats defined by the Tiff file-format.
    /// </summary>
    internal enum TiffCompression
    {
        // TIFF baseline compression types

        None = 1,
        Ccitt1D = 2,
        PackBits = 32773,

        // TIFF Extension compression types

        CcittGroup3Fax = 3,
        CcittGroup4Fax = 4,
        Lzw = 5,
        OldJpeg = 6,

        // Technote 2

        Jpeg = 7,
        Deflate = 8,
        OldDeflate = 32946,

        // TIFF-F/FX Extension

        ItuTRecT82 = 9,
        ItuTRecT43 = 10
    }
}