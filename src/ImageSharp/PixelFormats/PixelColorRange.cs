// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Describes the color range supported by a pixel format.
/// </summary>
public enum PixelColorRange
{
    /// <summary>
    /// The format stores colors within the standard dynamic range.
    /// Scaled color components are bounded to [0,1]. Native component units can differ.
    /// </summary>
    Sdr,

    /// <summary>
    /// The format can retain scaled color components below zero or above one.
    /// </summary>
    Extended
}
