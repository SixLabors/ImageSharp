// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the sub-file types defined by the Tiff file-format.
    /// </summary>
    [Flags]
    public enum TiffNewSubfileType : uint
    {
        /// <summary>
        /// A full-resolution image.
        /// </summary>
        FullImage = 0x0000,

        /// <summary>
        /// Reduced-resolution version of another image in this TIFF file.
        /// </summary>
        Preview = 0x0001,

        /// <summary>
        /// A single page of a multi-page image.
        /// </summary>
        SinglePage = 0x0002,

        /// <summary>
        /// A transparency mask for another image in this TIFF file.
        /// </summary>
        TransparencyMask = 0x0004,

        /// <summary>
        /// Alternative reduced-resolution version of another image in this TIFF file (see DNG specification).
        /// </summary>
        AlternativePreview = 0x10000,

        /// <summary>
        /// Mixed raster content (see RFC2301).
        /// </summary>
        MixedRasterContent = 0x0008
    }
}
