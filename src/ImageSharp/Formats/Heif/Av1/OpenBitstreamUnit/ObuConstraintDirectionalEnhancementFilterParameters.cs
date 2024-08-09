// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuConstraintDirectionalEnhancementFilterParameters
{
    public int BitCount { get; internal set; }

    public int Damping { get; internal set; }

    public int[] YStrength { get; set; } = new int[16];

    public int[] UvStrength { get; set; } = new int[16];
}
