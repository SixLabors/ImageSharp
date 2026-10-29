// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the filter selected for one AV1 loop-restoration unit.
/// </summary>
internal enum Av1RestorationFilterType
{
    /// <summary>
    /// Leaves the restoration-unit samples unchanged.
    /// </summary>
    None,

    /// <summary>
    /// Applies the separable Wiener restoration filter.
    /// </summary>
    Wiener,

    /// <summary>
    /// Applies self-guided restoration projection.
    /// </summary>
    SgrProjection,
}
