// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        FullImage = 0,

        /// <summary>
        /// Reduced-resolution version of another image in this TIFF file.
        /// </summary>
        Preview = 1,

        /// <summary>
        /// A single page of a multi-page image.
        /// </summary>
        SinglePage = 2,

        /// <summary>
        /// A transparency mask for another image in this TIFF file.
        /// </summary>
        TransparencyMask = 4,

        /// <summary>
        /// Alternative reduced-resolution version of another image in this TIFF file (see DNG specification).
        /// </summary>
        AlternativePreview = 65536,

        /// <summary>
        /// Mixed raster content (see RFC2301).
        /// </summary>
        MixedRasterContent = 8
    }
}
