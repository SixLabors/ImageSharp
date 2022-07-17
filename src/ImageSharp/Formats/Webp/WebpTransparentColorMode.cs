// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Enum indicating how the transparency should be handled on encoding.
    /// </summary>
    public enum WebpTransparentColorMode
    {
        /// <summary>
        /// Discard the transparency information for better compression.
        /// </summary>
        Clear = 0,

        /// <summary>
        /// The transparency will be kept as is.
        /// </summary>
        Preserve = 1,
    }
}
