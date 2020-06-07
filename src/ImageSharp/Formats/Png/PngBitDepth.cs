// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// Note the value assignment, This will allow us to add 1, 2, and 4 bit encoding when we support it.
namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration for the available PNG bit depths.
    /// </summary>
    public enum PngBitDepth : byte
    {
        /// <summary>
        /// 1 bit per sample or per palette index (not per pixel).
        /// </summary>
        Bit1 = 1,

        /// <summary>
        /// 2 bits per sample or per palette index (not per pixel).
        /// </summary>
        Bit2 = 2,

        /// <summary>
        /// 4 bits per sample or per palette index (not per pixel).
        /// </summary>
        Bit4 = 4,

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
