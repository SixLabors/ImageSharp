// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides enumeration of methods that control how ICC profiles are handled during decode.
/// </summary>
public enum ColorProfileHandling
{
    /// <summary>
    /// Retains embedded ICC profiles without converting pixels.
    /// </summary>
    Preserve,

    /// <summary>
    /// Removes any embedded Standard sRGB ICC color profiles without transforming the pixels of the image.
    /// </summary>
    Compact,

    /// <summary>
    /// Transforms the pixels of the image based on the conversion of any embedded ICC color profiles to sRGB V4 profile.
    /// If a recovered profile cannot supply a transform, the decoder uses its default color conversion.
    /// The decoder removes the original profile.
    /// </summary>
    Convert
}
