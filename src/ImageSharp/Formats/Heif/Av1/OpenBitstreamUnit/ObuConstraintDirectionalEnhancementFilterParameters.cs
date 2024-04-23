// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuConstraintDirectionalEnhancementFilterParameters
{
    public int BitCount { get; internal set; }

    public int[] YStrength { get; internal set; } = new int[5];

    public int[] UVStrength { get; internal set; } = new int[5];

    public int Damping { get; internal set; }
}
