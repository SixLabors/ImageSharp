// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Enumerates the available bits per pixel the tiff encoder supports.
    /// </summary>
    public enum TiffBitsPerPixel
    {
        /// <summary>
        /// 8 bits per pixel. Each pixel consists of 1 byte.
        /// </summary>
        Pixel8 = 8,

        /// <summary>
        /// 24 bits per pixel. Each pixel consists of 3 bytes.
        /// </summary>
        Pixel24 = 24,
    }
}
