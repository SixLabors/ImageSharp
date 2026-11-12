// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Describes a JPEG XL channel stored in addition to the image color components.
/// </summary>
public enum JxlExtraChannelType : byte
{
    /// <summary>
    /// Alpha transparency.
    /// </summary>
    Alpha = 0,

    /// <summary>
    /// Depth information.
    /// </summary>
    Depth = 1,

    /// <summary>
    /// A spot-color contribution.
    /// </summary>
    SpotColor = 2,

    /// <summary>
    /// A selection mask.
    /// </summary>
    SelectionMask = 3,

    /// <summary>
    /// The black component of CMYK color.
    /// </summary>
    Black = 4,

    /// <summary>
    /// A color filter array component.
    /// </summary>
    ColorFilterArray = 5,

    /// <summary>
    /// Thermal information.
    /// </summary>
    Thermal = 6,

    /// <summary>
    /// An unknown required channel. Decoders cannot decode this channel type.
    /// </summary>
    Unknown = 15,

    /// <summary>
    /// A channel that a decoder can ignore when it does not understand its meaning.
    /// </summary>
    Optional = 16
}
