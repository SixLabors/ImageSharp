// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuConstraintDirectionalEnhancementFilterParameters
{
    public bool BitCount { get; internal set; }

    public int[] YStrength { get; internal set; }

    public int[] UVStrength { get; internal set; }
}
