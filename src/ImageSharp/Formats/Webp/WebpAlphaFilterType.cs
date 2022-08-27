// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Enum for the different alpha filter types.
    /// </summary>
    internal enum WebpAlphaFilterType
    {
        /// <summary>
        /// No filtering.
        /// </summary>
        None = 0,

        /// <summary>
        /// Horizontal filter.
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// Vertical filter.
        /// </summary>
        Vertical = 2,

        /// <summary>
        /// Gradient filter.
        /// </summary>
        Gradient = 3,
    }
}
