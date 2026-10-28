// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the intra predictor blended with a single-reference inter predictor.
/// </summary>
internal enum Av1InterIntraMode : byte
{
    /// <summary>
    /// Uses a DC intra predictor.
    /// </summary>
    DC = 0,

    /// <summary>
    /// Uses a vertical intra predictor.
    /// </summary>
    Vertical = 1,

    /// <summary>
    /// Uses a horizontal intra predictor.
    /// </summary>
    Horizontal = 2,

    /// <summary>
    /// Uses a smooth intra predictor.
    /// </summary>
    Smooth = 3,
}
