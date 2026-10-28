// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides enumeration of the various webp color types.
/// </summary>
public enum WebpColorType
{
    /// <summary>
    /// Y′CbCr (luma, blue-difference chroma, red-difference chroma) as defined in ITU-R BT.601.
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
