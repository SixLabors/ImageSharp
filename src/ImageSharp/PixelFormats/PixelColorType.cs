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
    /// Represents the Exponent component used in formats like R9G9B9E5.
    /// </summary>
    Exponent = 1 << 4,

    /// <summary>
    /// Represents the luminance component of the color.
    /// Its interpretation and scale depend on the color model. Grayscale is represented by this component alone.
    /// </summary>
    Luminance = 1 << 5,

    /// <summary>
    /// Indicates that the color is in binary (black and white) format.
    /// </summary>
    Binary = 1 << 6,

    /// <summary>
    /// Indicates that the color is indexed using a palette.
    /// </summary>
    Indexed = 1 << 7,

    /// <summary>
    /// Indicates that the color is in RGB (Red, Green, Blue) format.
    /// </summary>
    RGB = Red | Green | Blue | (1 << 8),

    /// <summary>
    /// Indicates that the color is in BGR (Blue, Green, Red) format.
    /// </summary>
    BGR = Blue | Green | Red | (1 << 9),

    /// <summary>
    /// Represents the Chrominance Blue component.
    /// </summary>
    ChrominanceBlue = 1 << 10,

    /// <summary>
    /// Represents the Chrominance Red component.
    /// </summary>
    ChrominanceRed = 1 << 11,

    /// <summary>
    /// Indicates that the color is in YCbCr (Luminance, Chrominance Blue, Chrominance Red) format.
    /// </summary>
    YCbCr = Luminance | ChrominanceBlue | ChrominanceRed | (1 << 12),

    /// <summary>
    /// Represents the Cyan component of the color.
    /// </summary>
    Cyan = 1 << 13,

    /// <summary>
    /// Represents the Magenta component of the color.
    /// </summary>
    Magenta = 1 << 14,

    /// <summary>
    /// Represents the Yellow component of the color.
    /// </summary>
    Yellow = 1 << 15,

    /// <summary>
    /// Represents the Key (black) component in CMYK and YCCK.
    /// </summary>
    Key = 1 << 16,

    /// <summary>
    /// Indicates that the color is in CMY (Cyan, Magenta, Yellow) format.
    /// </summary>
    CMY = Cyan | Magenta | Yellow,

    /// <summary>
    /// Indicates that the color is in CMYK (Cyan, Magenta, Yellow, Key) format.
    /// </summary>
    CMYK = Cyan | Magenta | Yellow | Key,

    /// <summary>
    /// Indicates that the color is in YCCK (Luminance, Chrominance Blue, Chrominance Red, Key) format.
    /// </summary>
    YCCK = Luminance | ChrominanceBlue | ChrominanceRed | Key,

    /// <summary>
    /// Indicates that the color is in CIE XYZ format.
    /// </summary>
    XYZ = Luminance | (1 << 17),

    /// <summary>
    /// Indicates that the color is in CIE Yxy format.
    /// </summary>
    Yxy = Luminance | (1 << 18),

    /// <summary>
    /// Indicates that the color is in CIE Lab format.
    /// </summary>
    Lab = Luminance | (1 << 19),

    /// <summary>
    /// Indicates that the color is in CIE Luv format.
    /// </summary>
    Luv = Luminance | (1 << 20),

    /// <summary>
    /// Represents the hue component of the color.
    /// </summary>
    Hue = 1 << 21,

    /// <summary>
    /// Represents the saturation component of the color.
    /// </summary>
    Saturation = 1 << 22,

    /// <summary>
    /// Indicates that the color is in HSV format.
    /// </summary>
    HSV = Hue | Saturation | Luminance | (1 << 23),

    /// <summary>
    /// Indicates that the color is in HLS format.
    /// </summary>
    HLS = Hue | Luminance | Saturation | (1 << 24),

    /// <summary>
    /// Indicates that the color is of a type not specified in this enum.
    /// </summary>
    Other = 1 << 25
}
