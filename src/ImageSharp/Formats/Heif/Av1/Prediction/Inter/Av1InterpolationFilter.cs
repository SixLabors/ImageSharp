// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Identifies an AV1 interpolation filter used for translational inter prediction.
/// </summary>
internal enum Av1InterpolationFilter
{
    /// <summary>
    /// The regular interpolation-filter family.
    /// </summary>
    Regular,

    /// <summary>
    /// The smooth interpolation-filter family.
    /// </summary>
    Smooth,

    /// <summary>
    /// The sharp interpolation-filter family.
    /// </summary>
    Sharp,

    /// <summary>
    /// The bilinear interpolation-filter family.
    /// </summary>
    Bilinear,
}
