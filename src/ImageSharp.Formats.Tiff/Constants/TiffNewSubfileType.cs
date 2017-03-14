// <copyright file="TiffNewSubfileType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;

    /// <summary>
    /// Enumeration representing the sub-file types defined by the Tiff file-format.
    /// </summary>
    [Flags]
    internal enum TiffNewSubfileType
    {
        // TIFF baseline subfile types

        FullImage = 0x0000,
        Preview = 0x0001,
        SinglePage = 0x0002,
        TransparencyMask = 0x0004,

        // DNG Specification subfile types

        AlternativePreview = 0x10000,

        // TIFF-F/FX Specification subfile types
        
        MixedRasterContent = 0x0008
    }
}