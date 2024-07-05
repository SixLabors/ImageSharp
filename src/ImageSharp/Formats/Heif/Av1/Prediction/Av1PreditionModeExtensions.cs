// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static class Av1PreditionModeExtensions
{
    private static readonly Av1TransformType[] IntraPreditionMode2TransformType = [
        Av1TransformType.DctDct, // DC
        Av1TransformType.AdstDct, // V
        Av1TransformType.DctAdst, // H
        Av1TransformType.DctDct, // D45
        Av1TransformType.AdstAdst, // D135
        Av1TransformType.AdstDct, // D117
        Av1TransformType.DctAdst, // D153
        Av1TransformType.DctAdst, // D207
        Av1TransformType.AdstDct, // D63
        Av1TransformType.AdstAdst, // SMOOTH
        Av1TransformType.AdstDct, // SMOOTH_V
        Av1TransformType.DctAdst, // SMOOTH_H
        Av1TransformType.AdstAdst, // PAETH
    ];

    public static Av1TransformType ToTransformType(this Av1PredictionMode mode) => IntraPreditionMode2TransformType[(int)mode];
}
