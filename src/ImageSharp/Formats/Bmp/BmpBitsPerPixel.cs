// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Enumerates the available bits per pixel the bitmap encoder supports.
/// </summary>
public enum BmpBitsPerPixel : short
{
    /// <summary>
    /// 1 bit per pixel.
    /// </summary>
    Bit1 = 1,

    /// <summary>
    /// 2 bits per pixel.
    /// </summary>
    Bit2 = 2,

    /// <summary>
    /// 4 bits per pixel.
    /// </summary>
    Bit4 = 4,

    /// <summary>
    /// 8 bits per pixel. Each pixel consists of 1 byte.
    /// </summary>
    Bit8 = 8,

    /// <summary>
    /// 16 bits per pixel. Each pixel consists of 2 bytes.
    /// </summary>
    Bit16 = 16,

    /// <summary>
    /// 24 bits per pixel. Each pixel consists of 3 bytes.
    /// </summary>
    Bit24 = 24,

    /// <summary>
    /// 32 bits per pixel. Each pixel consists of 4 bytes.
    /// </summary>
    Bit32 = 32
}
