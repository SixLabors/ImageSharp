// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Enum for the different alpha filter types.
    /// </summary>
    internal enum WebPAlphaFilterType : int
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
