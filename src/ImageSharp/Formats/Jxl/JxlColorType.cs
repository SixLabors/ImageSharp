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
    /// Three components retained in a color model defined by the embedded ICC profile.
    /// </summary>
    Other
}
