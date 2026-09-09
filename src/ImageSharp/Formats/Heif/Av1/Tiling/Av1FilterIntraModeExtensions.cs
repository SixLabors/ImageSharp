// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Provides validity checks for AV1 filter-intra modes.
/// </summary>
internal static class Av1FilterIntraModeExtensions
{
    /// <summary>
    /// Maps filter-intra syntax values to the directional mode used by the predictor.
    /// </summary>
    private static readonly Av1PredictionMode[] IntraDirection =
        [Av1PredictionMode.DC, Av1PredictionMode.Vertical, Av1PredictionMode.Horizontal, Av1PredictionMode.Directional157Degrees, Av1PredictionMode.DC];

    /// <summary>
    /// Gets the intra-prediction direction associated with the specified filter-intra mode.
    /// </summary>
    /// <param name="mode">The filter-intra mode.</param>
    /// <returns>The corresponding intra-prediction direction.</returns>
    public static Av1PredictionMode ToIntraDirection(this Av1FilterIntraMode mode)
        => IntraDirection[(int)mode];
}
