// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Enumerates the available bits per pixel the webp image uses.
    /// </summary>
    public enum WebPBitsPerPixel : short
    {
        /// <summary>
        /// 24 bits per pixel. Each pixel consists of 3 bytes.
        /// </summary>
        Pixel24 = 24,

        /// <summary>
        /// 32 bits per pixel. Each pixel consists of 4 bytes (an alpha channel is present).
        /// </summary>
        Pixel32 = 32
    }
}
