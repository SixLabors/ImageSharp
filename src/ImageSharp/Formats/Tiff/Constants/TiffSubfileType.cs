// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the sub-file types defined by the Tiff file-format.
    /// </summary>
    public enum TiffSubfileType : ushort
    {
        /// <summary>
        /// Full-resolution image data.
        /// </summary>
        FullImage = 1,

        /// <summary>
        /// Reduced-resolution image data.
        /// </summary>
        Preview = 2,

        /// <summary>
        /// A single page of a multi-page image.
        /// </summary>
        SinglePage = 3
    }
}
