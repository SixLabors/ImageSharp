// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal static class Av1FilterIntraModeExtensions
{
    private static readonly Av1PredictionMode[] IntraDirection =
        [Av1PredictionMode.DC, Av1PredictionMode.Vertical, Av1PredictionMode.Horizontal, Av1PredictionMode.Directional157Degrees, Av1PredictionMode.DC];

    public static Av1PredictionMode ToIntraDirection(this Av1FilterIntraMode mode)
        => IntraDirection[(int)mode];
}
