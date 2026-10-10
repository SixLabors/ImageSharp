// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Identifies an AV1 interpolation-filter family or the frame-level switchable selection.
/// </summary>
internal enum Av1InterpolationFilter : byte
{
    /// <summary>
    /// The regular interpolation-filter family.
    /// </summary>
    Regular = 0,

    /// <summary>
    /// The smooth interpolation-filter family.
    /// </summary>
    Smooth = 1,

    /// <summary>
    /// The sharp interpolation-filter family.
    /// </summary>
    Sharp = 2,

    /// <summary>
    /// The bilinear interpolation-filter family.
    /// </summary>
    Bilinear = 3,

    /// <summary>
    /// Indicates that each inter block selects its interpolation-filter family.
    /// </summary>
    Switchable = 4,
}
