// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Enumerates the channel layouts of a 16-bit bitmap.
/// </summary>
public enum BmpBit16Layout
{
    /// <summary>
    /// 5 bits each for red, green and blue. Readers ignore the highest bit.
    /// </summary>
    Rgb555 = 0,

    /// <summary>
    /// 5 bits for red, 6 bits for green and 5 bits for blue.
    /// </summary>
    Rgb565 = 1
}
