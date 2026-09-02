// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the encoder's selected modes and references for an AV1 macroblock.
/// </summary>
internal struct Av1MacroBlockModeInfo
{
    private byte cdefStrength;

    /// <summary>
    /// Stores the prediction, transform, and segmentation decisions for the block.
    /// </summary>
    public Av1EncoderBlockModeInfo Block;

    /// <summary>
    /// Gets or sets the constrained directional enhancement filter strength for the block.
    /// </summary>
    public int CdefStrength
    {
        readonly get => this.cdefStrength;
        set => this.cdefStrength = (byte)value;
    }
}
