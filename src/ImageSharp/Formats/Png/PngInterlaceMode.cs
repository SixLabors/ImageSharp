// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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