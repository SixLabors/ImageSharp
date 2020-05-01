// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Enum indicating how the transparency should be handled on encoding.
    /// </summary>
    public enum PngTransparentColorBehavior
    {
        /// <summary>
        /// Converts fully transparent pixels that may contain R, G, B values which are not 0,
        /// to transparent black, which can yield in better compression in some cases.
        /// </summary>
        Clear,

        /// <summary>
        /// The transparency will be kept as is.
        /// </summary>
        Preserve
    }
}
