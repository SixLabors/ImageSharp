// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the encoder's selected modes and references for an AV1 macroblock.
/// </summary>
internal class Av1MacroBlockModeInfo
{
    /// <summary>
    /// Gets or sets the prediction, transform, and segmentation decisions for the block.
    /// </summary>
    public required Av1EncoderBlockModeInfo Block { get; internal set; }

    /// <summary>
    /// Gets or sets the luma palette decisions for the block.
    /// </summary>
    public required Av1PaletteLumaModeInfo Palette { get; internal set; }

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter strength for the block.
    /// </summary>
    public int CdefStrength { get; internal set; }
}
