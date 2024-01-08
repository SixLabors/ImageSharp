// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Represents the color type and format of a pixel.
/// </summary>
[Flags]
public enum PixelColorType
{
    /// <summary>
    /// Represents the Red component of the color.
    /// </summary>
    Red = 1 << 0,

    /// <summary>
    /// Represents the Green component of the color.
    /// </summary>
    Green = 1 << 1,

    /// <summary>
    /// Represents the Blue component of the color.
    /// </summary>
    Blue = 1 << 2,

    /// <summary>
    /// Represents the Alpha component of the color for transparency.
    /// </summary>
    Alpha = 1 << 3,

    /// <summary>
    /// Indicates that the color is in grayscale.
    /// </summary>
    Grayscale = 1 << 4,

    /// <summary>
    /// Indicates that the color is in RGB (Red, Green, Blue) format.
    /// </summary>
    RGB = Red | Green | Blue | (1 << 5),

    /// <summary>
    /// Indicates that the color is in BGR (Blue, Green, Red) format.
    /// </summary>
    BGR = Blue | Green | Red | (1 << 6)
}
