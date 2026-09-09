// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Provides transform, direction, angle, and neighbor metadata for AV1 intra-prediction modes.
/// </summary>
internal static class Av1PreditionModeExtensions
{
    /// <summary>
    /// Maps each luma intra-prediction mode to its default two-dimensional transform type.
    /// </summary>
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

    /// <summary>
    /// Maps each luma intra-prediction mode to the neighboring sample regions it consumes.
    /// </summary>
    private static readonly Av1NeighborNeed[] NeedsMap = [
        Av1NeighborNeed.Above | Av1NeighborNeed.Left, // DC
        Av1NeighborNeed.Above, // V
        Av1NeighborNeed.Left, // H
        Av1NeighborNeed.Above | Av1NeighborNeed.AboveRight, // D45
        Av1NeighborNeed.Left | Av1NeighborNeed.Above | Av1NeighborNeed.AboveLeft, // D135
        Av1NeighborNeed.Left | Av1NeighborNeed.Above | Av1NeighborNeed.AboveLeft, // D113
        Av1NeighborNeed.Left | Av1NeighborNeed.Above | Av1NeighborNeed.AboveLeft, // D157
        Av1NeighborNeed.Left | Av1NeighborNeed.BottomLeft, // D203
        Av1NeighborNeed.Above | Av1NeighborNeed.AboveRight, // D67
        Av1NeighborNeed.Left | Av1NeighborNeed.Above, // SMOOTH
        Av1NeighborNeed.Left | Av1NeighborNeed.Above, // SMOOTH_V
        Av1NeighborNeed.Left | Av1NeighborNeed.Above, // SMOOTH_H
        Av1NeighborNeed.Left | Av1NeighborNeed.Above | Av1NeighborNeed.AboveLeft, // PAETH
    ];

    /// <summary>
    /// Maps each luma intra-prediction mode to its base directional angle in degrees, or zero for non-directional modes.
    /// </summary>
    private static readonly int[] AngleMap = [
        0,
        90,
        180,
        45,
        135,
        113,
        157,
        203,
        67,
        0,
        0,
        0,
        0,
    ];

    /// <summary>
    /// Gets the default transform type associated with an intra-prediction mode.
    /// </summary>
    /// <param name="mode">The luma intra-prediction mode.</param>
    /// <returns>The default transform type.</returns>
    public static Av1TransformType ToTransformType(this Av1PredictionMode mode) => IntraPreditionMode2TransformType[(int)mode];

    /// <summary>
    /// Determines whether an intra-prediction mode projects samples along a coded angle.
    /// </summary>
    /// <param name="mode">The luma intra-prediction mode.</param>
    /// <returns><see langword="true"/> for a directional mode; otherwise, <see langword="false"/>.</returns>
    public static bool IsDirectional(this Av1PredictionMode mode)
        => mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees;

    /// <summary>
    /// Gets the neighboring sample regions required by an intra-prediction mode.
    /// </summary>
    /// <param name="mode">The luma intra-prediction mode.</param>
    /// <returns>The required neighboring sample flags.</returns>
    public static Av1NeighborNeed GetNeighborNeed(this Av1PredictionMode mode) => NeedsMap[(int)mode];

    /// <summary>
    /// Gets the base prediction angle for an intra-prediction mode.
    /// </summary>
    /// <param name="mode">The luma intra-prediction mode.</param>
    /// <returns>The prediction angle in degrees, or zero for a non-directional mode.</returns>
    public static int ToAngle(this Av1PredictionMode mode) => AngleMap[(int)mode];
}
