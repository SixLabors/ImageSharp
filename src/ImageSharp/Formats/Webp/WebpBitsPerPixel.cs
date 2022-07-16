// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Enumerates the available bits per pixel the webp image uses.
    /// </summary>
    public enum WebpBitsPerPixel : short
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
