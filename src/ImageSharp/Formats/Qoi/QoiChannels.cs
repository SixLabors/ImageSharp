// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Provides enumeration of available QOI color channels.
/// </summary>
public enum QoiChannels
{
    /// <summary>
    /// Each pixel is an R,G,B triple.
    /// </summary>
    Rgb = 3,

    /// <summary>
    /// Each pixel is an R,G,B triple, followed by an alpha sample.
    /// </summary>
    Rgba = 4
}
