// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Supported, JPEG XL-specific color space types.
/// </summary>
internal enum JxlColorSpace : byte
{
    /// <summary>
    /// Trichromatic color data. This also includes CMYK if Black
    /// ExtraChannelInfo is present.
    /// </summary>
    Rgb,

    /// <summary>
    /// Single-channel data.
    /// </summary>
    Gray,

    /// <summary>
    /// Like Rgb but fixed values for primaries.
    /// </summary>
    Xyb,

    /// <summary>
    /// Unknown color space
    /// </summary>
    Unknown
}
