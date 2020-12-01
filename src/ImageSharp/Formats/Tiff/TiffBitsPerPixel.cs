// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Enumerates the available bits per pixel the tiff encoder supports.
    /// </summary>
    public enum TiffBitsPerPixel
    {
        /// <summary>
        /// 1 bits per pixel, bi-color image. Each pixel consists of 1 bit.
        /// </summary>
        Pixel1 = 1,

        /// <summary>
        /// 8 bits per pixel, grayscale image. Each pixel consists of 1 byte.
        /// </summary>
        Pixel8 = 8,

        /// <summary>
        /// 24 bits per pixel. Each pixel consists of 3 bytes.
        /// </summary>
        Pixel24 = 24,
    }
}
