// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides choices for ICC color conversion and profile retention during decoding and identification.
/// </summary>
public enum ColorProfileHandling
{
    /// <summary>
    /// Converts decoded pixels and identified colors to sRGB while retaining the original ICC profile.
    /// </summary>
    Preserve,

    /// <summary>
    /// Converts colors to sRGB when required and removes embedded canonical sRGB ICC profiles.
    /// </summary>
    Compact,

    /// <summary>
    /// Converts decoded pixels and identified colors to sRGB and removes the original ICC profile.
    /// </summary>
    Convert
}
