// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Enum indicating how the transparency should be handled on encoding.
    /// </summary>
    public enum PngTransparentColorMode
    {
        /// <summary>
        /// The transparency will be kept as is.
        /// </summary>
        Preserve = 0,

        /// <summary>
        /// Converts fully transparent pixels that may contain R, G, B values which are not 0,
        /// to transparent black, which can yield in better compression in some cases.
        /// </summary>
        Clear = 1,
    }
}
