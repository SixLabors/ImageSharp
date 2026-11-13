// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Enumerates the color types of a bitmap: the channels of each pixel and their layout.
/// Each name gives the channels in storage order, from the first byte or the least significant bit.
/// </summary>
public enum BmpColorType
{
    /// <summary>
    /// Each pixel is an index into a color table. The bit depth is 1, 2, 4 or 8 bits per pixel.
    /// </summary>
    Palette = 0,

    /// <summary>
    /// Each pixel has blue, green and red, and no alpha. The bit depth is 16 bits per pixel, with 5 bits for each
    /// channel, or 24 or 32 bits per pixel, with 8 bits for each channel.
    /// </summary>
    Bgr = 1,

    /// <summary>
    /// Each pixel has blue, green, red and alpha, with 8 bits for each channel. The bit depth is 32 bits per pixel.
    /// </summary>
    Bgra = 2,

    /// <summary>
    /// Each pixel has 5 bits of blue, 6 bits of green and 5 bits of red. The bit depth is 16 bits per pixel.
    /// </summary>
    Bgr565 = 3,

    /// <summary>
    /// Each pixel has 5 bits each of blue, green and red, and 1 bit of alpha. The bit depth is 16 bits per pixel.
    /// </summary>
    Bgra5551 = 4
}
