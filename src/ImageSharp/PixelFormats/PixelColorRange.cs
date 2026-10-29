// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Describes the normalized RGB component range supported by a pixel format's color conversions.
/// </summary>
/// <remarks>
/// This value describes numeric range. It does not specify a transfer function or indicate whether an image contains HDR content.
/// Alpha represents opacity and uses [0,1] for either range.
/// Native vector conversions use the pixel format's storage range and units.
/// </remarks>
public enum PixelColorRange
{
    /// <summary>
    /// Normalized RGB components are bounded to the standard dynamic range interval [0,1].
    /// </summary>
    Sdr,

    /// <summary>
    /// Normalized RGB components can extend below zero or above one.
    /// Color conversions use the finite working limits [-65504,65504].
    /// </summary>
    Extended
}
