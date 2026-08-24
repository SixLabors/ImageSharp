// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores whether filter-intra prediction is active and which filter-intra mode is selected.
/// </summary>
internal class Av1IntraFilterModeInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether filter-intra prediction is enabled for the block.
    /// </summary>
    public bool UseFilterIntra { get; set; }

    /// <summary>
    /// Gets or sets the filter-intra mode selected for the block.
    /// </summary>
    public Av1FilterIntraMode Mode { get; set; }
}
