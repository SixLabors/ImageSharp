// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Enum for the different rendering intent's.
/// </summary>
internal enum BmpRenderingIntent
{
    /// <summary>
    /// Invalid default value.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Maintains saturation. Used for business charts and other situations in which undithered colors are required.
    /// </summary>
    LCS_GM_BUSINESS = 1,  

    /// <summary>
    /// Maintains colorimetric match. Used for graphic designs and named colors.
    /// </summary>
    LCS_GM_GRAPHICS = 2,

    /// <summary>
    /// Maintains contrast. Used for photographs and natural images.
    /// </summary>
    LCS_GM_IMAGES = 4,

    /// <summary>
    /// Maintains the white point. Matches the colors to their nearest color in the destination gamut.
    /// </summary>
    LCS_GM_ABS_COLORIMETRIC = 8,
}
