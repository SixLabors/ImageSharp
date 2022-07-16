// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the possible uses of extra components in TIFF format files.
    /// </summary>
    internal enum TiffExtraSamples
    {
        /// <summary>
        /// Unspecified data.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Associated alpha data (with pre-multiplied color).
        /// </summary>
        AssociatedAlpha = 1,

        /// <summary>
        /// Unassociated alpha data.
        /// </summary>
        UnassociatedAlpha = 2
    }
}
