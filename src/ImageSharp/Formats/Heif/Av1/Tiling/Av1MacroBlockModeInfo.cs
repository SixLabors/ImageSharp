// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1MacroBlockModeInfo
{
    public required Av1EncoderBlockModeInfo Block { get; internal set; }

    public required Av1PaletteLumaModeInfo Palette { get; internal set; }

    public int CdefStrength { get; internal set; }
}
