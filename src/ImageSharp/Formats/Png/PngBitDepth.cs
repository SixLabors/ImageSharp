// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// Note the value assignment, This will allow us to add 1, 2, and 4 bit encoding when we support it.
namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration for the available PNG bit depths.
    /// </summary>
    public enum PngBitDepth
    {
        /// <summary>
        /// 8 bits per sample or per palette index (not per pixel).
        /// </summary>
        Bit8 = 8,

        /// <summary>
        /// 16 bits per sample or per palette index (not per pixel).
        /// </summary>
        Bit16 = 16
    }
}
