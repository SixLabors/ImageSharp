// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the blending method used to combine two AV1 inter predictors.
/// </summary>
internal enum Av1CompoundType : byte
{
    /// <summary>
    /// Averages both predictors with equal weights.
    /// </summary>
    Average = 0,

    /// <summary>
    /// Weights predictors from their relative display-order distances.
    /// </summary>
    DistanceWeighted = 1,

    /// <summary>
    /// Selects per-pixel weights from a signaled wedge mask.
    /// </summary>
    Wedge = 2,

    /// <summary>
    /// Derives per-pixel weights from the difference between both predictors.
    /// </summary>
    DifferenceWeighted = 3,
}
