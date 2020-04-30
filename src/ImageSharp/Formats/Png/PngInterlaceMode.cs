// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration of available PNG interlace modes.
    /// </summary>
    public enum PngInterlaceMode : byte
    {
        /// <summary>
        /// Non interlaced
        /// </summary>
        None = 0,

        /// <summary>
        /// Adam 7 interlacing.
        /// </summary>
        Adam7 = 1
    }
}