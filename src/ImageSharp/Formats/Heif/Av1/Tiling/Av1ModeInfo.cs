// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the decoded prediction, segmentation, skip, and transform state for an AV1 mode-information block.
/// </summary>
internal class Av1ModeInfo
{
    /// <summary>
    /// Gets or sets the macroblock mode information associated with this map entry.
    /// </summary>
    public required Av1MacroBlockModeInfo MacroBlockModeInfo { get; internal set; }
}
