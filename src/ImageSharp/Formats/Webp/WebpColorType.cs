// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides enumeration of the various webp color types.
/// </summary>
public enum WebpColorType
{
    /// <summary>
    /// Yuv (luminance, blue chroma, red chroma) as defined in the ITU-R Rec. BT.709 specification.
    /// </summary>
    Yuv,

    /// <summary>
    /// Rgb color space.
    /// </summary>
    Rgb,

    /// <summary>
    /// Rgba color space.
    /// </summary>
    Rgba
}
