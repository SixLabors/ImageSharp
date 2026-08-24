// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the filter-intra predictor kernel selected for an AV1 block.
/// </summary>
internal enum Av1FilterIntraMode
{
    /// <summary>
    /// The filter-intra DC predictor.
    /// </summary>
    DC,

    /// <summary>
    /// The filter-intra vertical predictor.
    /// </summary>
    Vertical,

    /// <summary>
    /// The filter-intra horizontal predictor.
    /// </summary>
    Horizontal,

    /// <summary>
    /// The filter-intra 157-degree directional predictor.
    /// </summary>
    Directional157,

    /// <summary>
    /// The filter-intra Paeth predictor.
    /// </summary>
    Paeth,

    /// <summary>
    /// The number of filter-intra modes.
    /// </summary>
    AllFilterIntraModes,
}
