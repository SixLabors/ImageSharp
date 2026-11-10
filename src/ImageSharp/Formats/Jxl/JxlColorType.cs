// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Describes the encoded JPEG XL color model.
/// </summary>
public enum JxlColorType : byte
{
    /// <summary>
    /// Three color components interpreted as RGB, or as CMY when a black extra channel is present.
    /// </summary>
    Rgb,

    /// <summary>
    /// One luminance component.
    /// </summary>
    Luminance,

    /// <summary>
    /// Three components encoded through the JPEG XL XYB transform.
    /// </summary>
    Xyb,

    /// <summary>
    /// A color model defined by the embedded ICC profile.
    /// </summary>
    /// <remarks>Encoding uses RGB or luminance when the profile declares that color model. CMYK uses RGB with a black extra channel.</remarks>
    Other
}
