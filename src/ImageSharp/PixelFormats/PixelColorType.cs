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
    /// No color type.
    /// </summary>
    None = 0,

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
    BGR = Blue | Green | Red | (1 << 6),

    /// <summary>
    /// Represents the Luminance component in YCbCr.
    /// </summary>
    Luminance = 1 << 7,

    /// <summary>
    /// Represents the Chrominance Blue component in YCbCr.
    /// </summary>
    ChrominanceBlue = 1 << 8,

    /// <summary>
    /// Represents the Chrominance Red component in YCbCr.
    /// </summary>
    ChrominanceRed = 1 << 9,

    /// <summary>
    /// Indicates that the color is in YCbCr (Luminance, Chrominance Blue, Chrominance Red) format.
    /// </summary>
    YCbCr = Luminance | ChrominanceBlue | ChrominanceRed,

    /// <summary>
    /// Represents the Cyan component in CMYK.
    /// </summary>
    Cyan = 1 << 10,

    /// <summary>
    /// Represents the Magenta component in CMYK.
    /// </summary>
    Magenta = 1 << 11,

    /// <summary>
    /// Represents the Yellow component in CMYK.
    /// </summary>
    Yellow = 1 << 12,

    /// <summary>
    /// Represents the Key (black) component in CMYK and YCCK.
    /// </summary>
    Key = 1 << 13,

    /// <summary>
    /// Indicates that the color is in CMYK (Cyan, Magenta, Yellow, Key) format.
    /// </summary>
    CMYK = Cyan | Magenta | Yellow | Key,

    /// <summary>
    /// Indicates that the color is in YCCK (Luminance, Chrominance Blue, Chrominance Red, Key) format.
    /// </summary>
    YCCK = Luminance | ChrominanceBlue | ChrominanceRed | Key,

    /// <summary>
    /// Indicates that the color is indexed using a palette.
    /// </summary>
    Indexed = 1 << 14,

    /// <summary>
    /// Indicates that the color is of a type not specified in this enum.
    /// </summary>
    Other = 1 << 15
}
