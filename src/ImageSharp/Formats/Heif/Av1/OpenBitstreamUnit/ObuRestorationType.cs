// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the loop-restoration mode encoded by the two-bit frame-restoration syntax.
/// </summary>
internal enum ObuRestorationType : uint
{
    /// <summary>
    /// Loop restoration is disabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Each restoration unit selects its filter type.
    /// </summary>
    Switchable = 1,

    /// <summary>
    /// Separable Wiener filtering is used.
    /// </summary>
    Wiener = 2,

    /// <summary>
    /// Self-guided restoration projection is used.
    /// </summary>
    SgrProj = 3,
}
