// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides enumeration of methods that control how ICC profiles are handled during decode.
/// </summary>
public enum ColorProfileHandling
{
    /// <summary>
    /// Leaves any embedded ICC color profiles intact.
    /// </summary>
    Preserve,

    /// <summary>
    /// Transforms the pixels of the image based on the conversion of any embedded ICC color profiles to sRGB V4 profile.
    /// The original profile is then replaced.
    /// </summary>
    Convert
}
